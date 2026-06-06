using System;
using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.Rcw;

// Port of olcPGEX_RayCastWorld (olc::rcw) — a Wolfenstein-style software ray-cast world. Subclass
// Engine, implement the abstract scenery/object samplers + IsLocationSolid, then call Update() and
// Render() each frame. Rendering and collisions all happen on the CPU in tile/world space.

// An entity that lives in the world (the camera is usually one of these, kept invisible).
public class RcwObject
{
    public uint GenericId;
    public Vector2d<float> Pos;
    public Vector2d<float> Vel;
    public float Speed;
    public float Heading;
    public float Radius = 0.5f;
    public bool Visible = true;
    public bool Remove;
    public bool CollideWithScenery = true;
    public bool NotifySceneryCollision;
    public bool CollideWithObjects;
    public bool NotifyObjectCollision;
    public bool CanBeMoved = true;
    public bool IsActive = true;

    private const float Pi = 3.14159f;

    public void Walk(float walkSpeed)
    {
        Speed = walkSpeed;
        Vel = new Vector2d<float>(MathF.Cos(Heading) * Speed, MathF.Sin(Heading) * Speed);
    }

    public void Strafe(float strafeSpeed)
    {
        Speed = strafeSpeed;
        // Perpendicular of (cos, sin) is (-sin, cos).
        Vel = new Vector2d<float>(-MathF.Sin(Heading) * Speed, MathF.Cos(Heading) * Speed);
    }

    public void Turn(float turnSpeed)
    {
        Heading += turnSpeed;
        if (Heading < -Pi) Heading += 2f * Pi;
        if (Heading > Pi) Heading -= 2f * Pi;
    }

    public void Stop()
    {
        Speed = 0;
        Vel = new Vector2d<float>(0, 0);
    }
}

public abstract class Engine : PGEX
{
    public enum CellSide { North, East, South, West, Top, Bottom }

    // All the info about a ray hitting a tile.
    public struct TileHit
    {
        public Vector2d<int> TilePos;
        public Vector2d<float> HitPos;
        public float Length;
        public float SampleX;
        public CellSide Side;
    }

    private const float Pi = 3.14159f;

    public Dictionary<uint, RcwObject> MapObjects = new();

    private readonly Vector2d<int> _screenSize;
    private readonly Vector2d<float> _floatScreenSize;
    private readonly float[] _depthBuffer;
    private float _fieldOfView;
    private Vector2d<float> _cameraPos = new(5, 5);
    private float _cameraHeading;

    protected Engine(int screenW, int screenH, float fov) : base(false)
    {
        _screenSize = new Vector2d<int>(screenW, screenH);
        _floatScreenSize = new Vector2d<float>(screenW, screenH);
        _fieldOfView = fov;
        _depthBuffer = new float[screenW * screenH];
    }

    // ABSTRACT — the user supplies the world: scenery colours, solidity, object sizes + sprites.
    protected abstract Pixel SelectSceneryPixel(int tileX, int tileY, CellSide side, float sampleX, float sampleY, float distance);
    protected abstract bool IsLocationSolid(float tileX, float tileY);
    protected abstract float GetObjectWidth(uint id);
    protected abstract float GetObjectHeight(uint id);
    protected abstract Pixel SelectObjectPixel(uint id, float sampleX, float sampleY, float distance, float angle);

    // OPTIONAL — collision-response notifications.
    protected virtual void HandleObjectVsScenery(RcwObject obj, int tileX, int tileY, CellSide side, float offsetX, float offsetY) { }
    protected virtual void HandleObjectVsObject(RcwObject obj1, RcwObject obj2) { }

    public void SetCamera(Vector2d<float> pos, float heading) { _cameraPos = pos; _cameraHeading = heading; }

    // Advances object positions and statically resolves collisions against scenery + other objects.
    public virtual void Update(float elapsedTime)
    {
        foreach (var entry in MapObjects)
        {
            var obj = entry.Value;
            if (!obj.IsActive) continue;

            // Take more sub-steps when the object would travel further than its own radius.
            var steps = 1;
            var delta = elapsedTime;
            var travelX = obj.Vel.X * elapsedTime;
            var travelY = obj.Vel.Y * elapsedTime;
            var totalTravel = travelX * travelX + travelY * travelY;
            var totalRadius = obj.Radius * obj.Radius;
            if (totalTravel >= totalRadius)
            {
                var fSteps = MathF.Ceiling(totalTravel / totalRadius);
                steps = (int)fSteps;
                delta = elapsedTime / fSteps;
            }

            for (var step = 0; step < steps; step++)
            {
                var potX = obj.Pos.X + obj.Vel.X * delta;
                var potY = obj.Pos.Y + obj.Vel.Y * delta;

                if (obj.CollideWithObjects)
                {
                    foreach (var entry2 in MapObjects)
                    {
                        var target = entry2.Value;
                        if (!target.CollideWithObjects || ReferenceEquals(target, obj)) continue;

                        var dx = target.Pos.X - obj.Pos.X;
                        var dy = target.Pos.Y - obj.Pos.Y;
                        var sumR = target.Radius + obj.Radius;
                        if (dx * dx + dy * dy <= sumR * sumR)
                        {
                            var dist = MathF.Sqrt(dx * dx + dy * dy);
                            if (dist != 0)
                            {
                                var overlap = dist - obj.Radius - target.Radius;
                                potX -= (obj.Pos.X - target.Pos.X) / dist * overlap;
                                potY -= (obj.Pos.Y - target.Pos.Y) / dist * overlap;
                                if (target.CanBeMoved)
                                    target.Pos = new Vector2d<float>(
                                        target.Pos.X + (obj.Pos.X - target.Pos.X) / dist * overlap,
                                        target.Pos.Y + (obj.Pos.Y - target.Pos.Y) / dist * overlap);
                            }
                            if (obj.NotifyObjectCollision) HandleObjectVsObject(obj, target);
                        }
                    }
                }

                if (obj.CollideWithScenery)
                {
                    var tlX = Math.Min((int)obj.Pos.X, (int)potX) - 1;
                    var tlY = Math.Min((int)obj.Pos.Y, (int)potY) - 1;
                    var brX = Math.Max((int)obj.Pos.X, (int)potX) + 1;
                    var brY = Math.Max((int)obj.Pos.Y, (int)potY) + 1;

                    for (var cy = tlY; cy <= brY; cy++)
                    {
                        for (var cx = tlX; cx <= brX; cx++)
                        {
                            if (!IsLocationSolid(cx + 0.5f, cy + 0.5f)) continue;

                            // Nearest point on the cell rectangle to the object's future position.
                            var nearX = MathF.Max(cx, MathF.Min(potX, cx + 1));
                            var nearY = MathF.Max(cy, MathF.Min(potY, cy + 1));
                            var rayX = nearX - potX;
                            var rayY = nearY - potY;
                            var len = MathF.Sqrt(rayX * rayX + rayY * rayY);
                            var overlap = obj.Radius - len;
                            if (float.IsNaN(overlap)) overlap = 0;

                            if (overlap > 0)
                            {
                                if (len != 0)
                                {
                                    potX -= rayX / len * overlap;
                                    potY -= rayY / len * overlap;
                                }
                                if (obj.NotifySceneryCollision)
                                {
                                    var side = CellSide.Bottom;
                                    if (nearX == cx) side = CellSide.West;
                                    if (nearX == cx + 1) side = CellSide.East;
                                    if (nearY == cy) side = CellSide.North;
                                    if (nearY == cy + 1) side = CellSide.South;
                                    HandleObjectVsScenery(obj, cx, cy, side, nearX - cx, nearY - cy);
                                }
                            }
                        }
                    }
                }

                obj.Pos = new Vector2d<float>(potX, potY);
            }
        }
    }

    // Draws the world: per-column ray-cast walls + Mode-7 floors/ceilings, then billboard objects.
    public void Render()
    {
        void DepthDraw(int x, int y, float z, Pixel p)
        {
            if (z <= _depthBuffer[y * _screenSize.X + x])
            {
                Pge.Draw(x, y, p);
                _depthBuffer[y * _screenSize.X + x] = z;
            }
        }

        for (var i = 0; i < _screenSize.X * _screenSize.Y; i++) _depthBuffer[i] = float.PositiveInfinity;

        for (var x = 0; x < _screenSize.X; x++)
        {
            var rayAngle = _cameraHeading - _fieldOfView / 2f + (float)x / _floatScreenSize.X * _fieldOfView;
            var rayDirX = MathF.Cos(rayAngle);
            var rayDirY = MathF.Sin(rayAngle);

            var rayLength = float.PositiveInfinity;
            if (CastRayDDA(_cameraPos, new Vector2d<float>(rayDirX, rayDirY), out var hit))
            {
                var rx = hit.HitPos.X - _cameraPos.X;
                var ry = hit.HitPos.Y - _cameraPos.Y;
                // Cosine-correct to remove the fisheye distortion.
                rayLength = MathF.Sqrt(rx * rx + ry * ry) * MathF.Cos(rayAngle - _cameraHeading);
            }

            var ceiling = _floatScreenSize.Y / 2f - _floatScreenSize.Y / rayLength;
            var floor = _floatScreenSize.Y - ceiling;
            var wallHeight = floor - ceiling;

            for (var y = 0; y < _screenSize.Y; y++)
            {
                if (y <= (int)ceiling)
                {
                    DrawPlane(x, y, rayAngle, rayDirX, rayDirY, _floatScreenSize.Y / 2f - y, CellSide.Top);
                }
                else if (y <= (int)floor)
                {
                    var sampleY = (y - ceiling) / wallHeight;
                    DepthDraw(x, y, rayLength,
                        SelectSceneryPixel(hit.TilePos.X, hit.TilePos.Y, hit.Side, hit.SampleX, sampleY, rayLength));
                }
                else
                {
                    DrawPlane(x, y, rayAngle, rayDirX, rayDirY, y - _floatScreenSize.Y / 2f, CellSide.Bottom);
                }
            }
        }

        // Objects (no sort needed for binary-transparent sprites; the depth buffer handles it).
        foreach (var entry in MapObjects)
        {
            var obj = entry.Value;
            if (!obj.Visible) continue;

            var ox = obj.Pos.X - _cameraPos.X;
            var oy = obj.Pos.Y - _cameraPos.Y;
            var distance = MathF.Sqrt(ox * ox + oy * oy);

            var angle = MathF.Atan2(oy, ox) - _cameraHeading;
            if (angle < -Pi) angle += 2f * Pi;
            if (angle > Pi) angle -= 2f * Pi;

            var inFov = MathF.Abs(angle) < (_fieldOfView + 1f / distance) / 2f;
            if (!inFov || distance < 0.5f) continue;

            var floorX = (0.5f * (angle / (_fieldOfView * 0.5f)) + 0.5f) * _floatScreenSize.X;
            var floorY = _floatScreenSize.Y / 2f + _floatScreenSize.Y / distance / MathF.Cos(angle / 2f);
            var sizeX = GetObjectWidth(obj.GenericId) * 2f * _floatScreenSize.Y / distance;
            var sizeY = GetObjectHeight(obj.GenericId) * 2f * _floatScreenSize.Y / distance;
            var topLeftX = floorX - sizeX / 2f;
            var topLeftY = floorY - sizeY;

            var niceAngle = _cameraHeading - obj.Heading + Pi / 4f;
            if (niceAngle < 0) niceAngle += 2f * Pi;
            if (niceAngle > 2f * Pi) niceAngle -= 2f * Pi;

            for (var sy = 0f; sy < sizeY; sy++)
            {
                for (var sx = 0f; sx < sizeX; sx++)
                {
                    var p = SelectObjectPixel(obj.GenericId, sx / sizeX, sy / sizeY, distance, niceAngle);
                    var ax = (int)(topLeftX + sx);
                    var ay = (int)(topLeftY + sy);
                    if (ax >= 0 && ax < _screenSize.X && ay >= 0 && ay < _screenSize.Y && p.Alpha == 255)
                        DepthDraw(ax, ay, distance, p);
                }
            }
        }
    }

    // Mode-7-style floor/ceiling projection for one pixel (no depth buffer needed).
    private void DrawPlane(int x, int y, float rayAngle, float rayDirX, float rayDirY, float denom, CellSide side)
    {
        var planeZ = _floatScreenSize.Y / 2f / denom;
        var factor = planeZ * 2f / MathF.Cos(rayAngle - _cameraHeading);
        var px = _cameraPos.X + rayDirX * factor;
        var py = _cameraPos.Y + rayDirY * factor;
        var tx = (int)px;
        var ty = (int)py;
        Pge.Draw(x, y, SelectSceneryPixel(tx, ty, side, px - tx, py - ty, planeZ));
    }

    // DDA traversal into the tile grid; fills `hit` and returns whether a solid tile was found.
    public bool CastRayDDA(Vector2d<float> origin, Vector2d<float> direction, out TileHit hit)
    {
        hit = new TileHit { Side = CellSide.North };
        var deltaX = MathF.Sqrt(1 + direction.Y / direction.X * (direction.Y / direction.X));
        var deltaY = MathF.Sqrt(1 + direction.X / direction.Y * (direction.X / direction.Y));

        var mapX = (int)origin.X;
        var mapY = (int)origin.Y;
        float sideDistX, sideDistY;
        int stepX, stepY;

        if (direction.X < 0) { stepX = -1; sideDistX = (origin.X - mapX) * deltaX; }
        else { stepX = 1; sideDistX = (mapX + 1f - origin.X) * deltaX; }
        if (direction.Y < 0) { stepY = -1; sideDistY = (origin.Y - mapY) * deltaY; }
        else { stepY = 1; sideDistY = (mapY + 1f - origin.Y) * deltaY; }

        const float maxDistance = 100f;
        var distance = 0f;
        var found = false;
        float interX = 0, interY = 0;

        while (!found && distance < maxDistance)
        {
            if (sideDistX < sideDistY) { sideDistX += deltaX; mapX += stepX; }
            else { sideDistY += deltaY; mapY += stepY; }

            var rdX = mapX - origin.X;
            var rdY = mapY - origin.Y;
            distance = MathF.Sqrt(rdX * rdX + rdY * rdY);

            if (!IsLocationSolid(mapX, mapY)) continue;

            found = true;
            hit.TilePos = new Vector2d<int>(mapX, mapY);
            var m = direction.Y / direction.X;

            if (origin.Y <= mapY)
            {
                if (origin.X <= mapX) { hit.Side = CellSide.West; interY = m * (mapX - origin.X) + origin.Y; interX = mapX; hit.SampleX = interY - MathF.Floor(interY); }
                else if (origin.X >= mapX + 1) { hit.Side = CellSide.East; interY = m * (mapX + 1 - origin.X) + origin.Y; interX = mapX + 1; hit.SampleX = interY - MathF.Floor(interY); }
                else { hit.Side = CellSide.North; interY = mapY; interX = (mapY - origin.Y) / m + origin.X; hit.SampleX = interX - MathF.Floor(interX); }

                if (interY < mapY) { hit.Side = CellSide.North; interY = mapY; interX = (mapY - origin.Y) / m + origin.X; hit.SampleX = interX - MathF.Floor(interX); }
            }
            else if (origin.Y >= mapY + 1)
            {
                if (origin.X <= mapX) { hit.Side = CellSide.West; interY = m * (mapX - origin.X) + origin.Y; interX = mapX; hit.SampleX = interY - MathF.Floor(interY); }
                else if (origin.X >= mapX + 1) { hit.Side = CellSide.East; interY = m * (mapX + 1 - origin.X) + origin.Y; interX = mapX + 1; hit.SampleX = interY - MathF.Floor(interY); }
                else { hit.Side = CellSide.South; interY = mapY + 1; interX = (mapY + 1 - origin.Y) / m + origin.X; hit.SampleX = interX - MathF.Floor(interX); }

                if (interY > mapY + 1) { hit.Side = CellSide.South; interY = mapY + 1; interX = (mapY + 1 - origin.Y) / m + origin.X; hit.SampleX = interX - MathF.Floor(interX); }
            }
            else
            {
                if (origin.X <= mapX) { hit.Side = CellSide.West; interY = m * (mapX - origin.X) + origin.Y; interX = mapX; hit.SampleX = interY - MathF.Floor(interY); }
                else if (origin.X >= mapX + 1) { hit.Side = CellSide.East; interY = m * (mapX + 1 - origin.X) + origin.Y; interX = mapX + 1; hit.SampleX = interY - MathF.Floor(interY); }
            }

            hit.HitPos = new Vector2d<float>(interX, interY);
        }

        return found;
    }
}
