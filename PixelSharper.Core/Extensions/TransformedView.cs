using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions;

// Port of olcPGEX_TransformedView — a pan/zoom "camera" that maps between world and screen space
// and offers World* draw wrappers over the engine's drawing API. Not auto-hooked; the user calls
// Initialise() then HandlePanAndZoom() / the World draws each frame.
public class TransformedView : PGEX
{
    protected Vector2d<float> WorldOffset = new(0, 0);
    protected Vector2d<float> WorldScale = new(1, 1);
    protected Vector2d<float> RecipPixel = new(1, 1);
    protected Vector2d<float> PixelScale = new(1, 1);
    protected bool Panning;
    protected Vector2d<float> StartPan = new(0, 0);
    protected Vector2d<int> ViewArea;
    protected bool ZoomClamp;
    protected Vector2d<float> MaxScale = new(0, 0);
    protected Vector2d<float> MinScale = new(0, 0);

    public PixelGameEngine GetPGE() => Pge;

    public virtual void Initialise(Vector2d<int> viewArea, Vector2d<float>? pixelScale = null)
    {
        var ps = pixelScale ?? new Vector2d<float>(1, 1);
        SetViewArea(viewArea);
        SetWorldScale(ps);
        PixelScale = ps;
        RecipPixel = new Vector2d<float>(1.0f / ps.X, 1.0f / ps.Y);
    }

    // O--- Camera state ---O
    public void SetWorldOffset(Vector2d<float> offset) => WorldOffset = offset;
    public void MoveWorldOffset(Vector2d<float> delta) => WorldOffset += delta;
    public void SetViewArea(Vector2d<int> viewArea) => ViewArea = viewArea;
    public Vector2d<float> GetWorldOffset() => WorldOffset;
    public Vector2d<float> GetWorldScale() => WorldScale;
    public void SetScaleExtents(Vector2d<float> min, Vector2d<float> max) { MinScale = min; MaxScale = max; }
    public void EnableScaleClamp(bool enable) => ZoomClamp = enable;

    public void SetWorldScale(Vector2d<float> scale)
    {
        WorldScale = scale;
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
    }

    public Vector2d<float> GetWorldTL() => ScreenToWorld(new Vector2d<float>(0, 0));
    public Vector2d<float> GetWorldBR() => ScreenToWorld(ViewArea.As<float>());
    public Vector2d<float> GetWorldVisibleArea() => GetWorldBR() - GetWorldTL();

    public void ZoomAtScreenPos(float deltaZoom, Vector2d<int> pos)
    {
        var before = ScreenToWorld(pos.As<float>());
        WorldScale = new Vector2d<float>(WorldScale.X * deltaZoom, WorldScale.Y * deltaZoom);
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
        var after = ScreenToWorld(pos.As<float>());
        WorldOffset += before - after;
    }

    public void SetZoom(float zoom, Vector2d<float> pos)
    {
        var before = ScreenToWorld(pos);
        WorldScale = new Vector2d<float>(zoom, zoom);
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
        var after = ScreenToWorld(pos);
        WorldOffset += before - after;
    }

    public void StartPanning(Vector2d<int> pos) { Panning = true; StartPan = pos.As<float>(); }
    public void UpdatePan(Vector2d<int> pos)
    {
        if (!Panning) return;
        WorldOffset -= (pos.As<float>() - StartPan) / WorldScale;
        StartPan = pos.As<float>();
    }
    public void EndPan(Vector2d<int> pos) { UpdatePan(pos); Panning = false; }

    // O--- Transforms ---O
    public virtual Vector2d<float> WorldToScreen(Vector2d<float> worldPos) => (worldPos - WorldOffset) * WorldScale;
    public virtual Vector2d<float> ScreenToWorld(Vector2d<float> screenPos) => screenPos / WorldScale + WorldOffset;
    public virtual Vector2d<float> ScaleToWorld(Vector2d<float> screenSize) => screenSize / WorldScale;
    public virtual Vector2d<float> ScaleToScreen(Vector2d<float> worldSize) => worldSize * WorldScale;

    public virtual bool IsPointVisible(Vector2d<float> pos)
    {
        var s = WorldToScreen(pos).As<int>();
        return s.X >= 0 && s.X < ViewArea.X && s.Y >= 0 && s.Y < ViewArea.Y;
    }

    public virtual bool IsRectVisible(Vector2d<float> pos, Vector2d<float> size)
    {
        var sp = WorldToScreen(pos).As<int>();
        var ss = (size * WorldScale).As<int>();
        return sp.X < ViewArea.X && sp.X + ss.X > 0 && sp.Y < ViewArea.Y && sp.Y + ss.Y > 0;
    }

    public virtual void HandlePanAndZoom(int mouseButton = 2, float zoomRate = 0.1f, bool pan = true, bool zoom = true)
    {
        var mouse = Pge.GetMousePos();
        if (pan)
        {
            if (Pge.GetMouse(mouseButton).Pressed) StartPanning(mouse);
            if (Pge.GetMouse(mouseButton).Held) UpdatePan(mouse);
            if (Pge.GetMouse(mouseButton).Released) EndPan(mouse);
        }
        if (zoom)
        {
            if (Pge.GetMouseWheel() > 0) ZoomAtScreenPos(1.0f + zoomRate, mouse);
            if (Pge.GetMouseWheel() < 0) ZoomAtScreenPos(1.0f - zoomRate, mouse);
        }
    }

    // O--- World draw wrappers (software primitives) ---O
    public bool Draw(float x, float y, Pixel? p = null) => Draw(new Vector2d<float>(x, y), p);
    public virtual bool Draw(Vector2d<float> pos, Pixel? p = null) => Pge.Draw(WorldToScreen(pos).As<int>(), p ?? Pixel.WHITE);

    public void DrawLine(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null, uint pattern = 0xFFFFFFFF)
        => Pge.DrawLine(WorldToScreen(pos1).As<int>(), WorldToScreen(pos2).As<int>(), p ?? Pixel.WHITE, pattern);

    public void DrawCircle(Vector2d<float> pos, float radius, Pixel? p = null, byte mask = 0xFF)
        => Pge.DrawCircle(WorldToScreen(pos).As<int>(), (int)(radius * WorldScale.X), p ?? Pixel.WHITE, mask);

    public void FillCircle(Vector2d<float> pos, float radius, Pixel? p = null)
        => Pge.FillCircle(WorldToScreen(pos).As<int>(), (int)(radius * WorldScale.X), p ?? Pixel.WHITE);

    public void DrawRect(Vector2d<float> pos, Vector2d<float> size, Pixel? p = null)
    {
        var sz = new Vector2d<int>(
            (int)MathF.Floor(size.X * WorldScale.X + 0.5f),
            (int)MathF.Floor(size.Y * WorldScale.Y + 0.5f));
        Pge.DrawRect(WorldToScreen(pos).As<int>(), sz, p ?? Pixel.WHITE);
    }

    public void FillRect(Vector2d<float> pos, Vector2d<float> size, Pixel? p = null)
        => Pge.FillRect(WorldToScreen(pos).As<int>(), (size * WorldScale).As<int>(), p ?? Pixel.WHITE);

    public void DrawTriangle(Vector2d<float> p1, Vector2d<float> p2, Vector2d<float> p3, Pixel? p = null)
        => Pge.DrawTriangle(WorldToScreen(p1).As<int>(), WorldToScreen(p2).As<int>(), WorldToScreen(p3).As<int>(), p ?? Pixel.WHITE);

    public void FillTriangle(Vector2d<float> p1, Vector2d<float> p2, Vector2d<float> p3, Pixel? p = null)
        => Pge.FillTriangle(WorldToScreen(p1).As<int>(), WorldToScreen(p2).As<int>(), WorldToScreen(p3).As<int>(), p ?? Pixel.WHITE);

    public void DrawSprite(Vector2d<float> pos, Sprite sprite, Vector2d<float>? scale = null, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var spriteSize = new Vector2d<float>(sprite.Width, sprite.Height);
        if (!IsRectVisible(pos, spriteSize * s)) return;

        var scaledSize = spriteSize * RecipPixel * WorldScale * s;
        var pixelStart = WorldToScreen(pos).As<int>();
        var pixelEnd = WorldToScreen(spriteSize * s + pos).As<int>();
        var screenStart = Vector2d<int>.Max(pixelStart, new Vector2d<int>(0, 0));
        var screenEnd = Vector2d<int>.Min(pixelEnd, new Vector2d<int>(Pge.ScreenWidth(), Pge.ScreenHeight()));
        var step = new Vector2d<float>(1.0f / scaledSize.X, 1.0f / scaledSize.Y);

        for (var y = screenStart.Y; y < screenEnd.Y; y++)
            for (var x = screenStart.X; x < screenEnd.X; x++)
            {
                var sample = (new Vector2d<int>(x, y) - pixelStart).As<float>() * step;
                Pge.Draw(new Vector2d<int>(x, y), sprite.SamplePixel(sample.X, sample.Y));
            }
    }

    public void DrawPartialSprite(Vector2d<float> pos, Sprite sprite, Vector2d<int> sourcePos, Vector2d<int> size,
        Vector2d<float>? scale = null, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var sizeF = size.As<float>();
        if (!IsRectVisible(pos, sizeF * s)) return;

        var scaledSize = sizeF * RecipPixel * WorldScale * s;
        var spritePixelStep = new Vector2d<float>(1.0f / sprite.Width, 1.0f / sprite.Height);
        var start = WorldToScreen(pos).As<int>();
        var end = scaledSize.As<int>() + start;
        var screenStep = new Vector2d<float>(1.0f / scaledSize.X, 1.0f / scaledSize.Y);

        for (var y = start.Y; y < end.Y; y++)
            for (var x = start.X; x < end.X; x++)
            {
                var sample = (new Vector2d<int>(x, y) - start).As<float>() * screenStep * sizeF * spritePixelStep
                             + sourcePos.As<float>() * spritePixelStep;
                Pge.Draw(new Vector2d<int>(x, y), sprite.SamplePixel(sample.X, sample.Y));
            }
    }

    public void DrawString(Vector2d<float> pos, string text, Pixel col, Vector2d<float> scale)
    {
        float offX = 0, offY = 0;
        var m = Pge.GetPixelMode();
        Pge.SetPixelMode((x, y, source, dest) => source.Red > 1 ? col : dest);
        foreach (var c in text)
        {
            if (c == '\n') { offX = 0; offY += 8.0f * RecipPixel.Y * scale.Y; }
            else
            {
                var ox = (c - 32) % 16 * 8;
                var oy = (c - 32) / 16 * 8;
                DrawPartialSprite(new Vector2d<float>(pos.X + offX, pos.Y + offY), Pge.GetFontSprite(),
                    new Vector2d<int>(ox, oy), new Vector2d<int>(8, 8), scale);
                offX += 8.0f * RecipPixel.X * scale.X;
            }
        }
        Pge.SetPixelMode(m);
    }

    // O--- World draw wrappers (GPU decals) ---O
    private Vector2d<float> DecalScale(Vector2d<float> scale) => scale * WorldScale * RecipPixel;

    public void DrawDecal(Vector2d<float> pos, Decal decal, Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawDecal(WorldToScreen(pos), decal, DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    public void DrawPartialDecal(Vector2d<float> pos, Decal decal, Vector2d<float> sourcePos, Vector2d<float> sourceSize,
        Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawPartialDecal(WorldToScreen(pos), decal, sourcePos, sourceSize, DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    public void DrawPartialDecal(Vector2d<float> pos, Vector2d<float> size, Decal decal, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
        => Pge.DrawPartialDecal(WorldToScreen(pos), size * WorldScale * RecipPixel, decal, sourcePos, sourceSize, tint ?? Pixel.WHITE);

    public void DrawExplicitDecal(Decal decal, Vector2d<float>[] pos, Vector2d<float>[] uv, Pixel[] col, int elements = 4)
    {
        var transformed = new Vector2d<float>[elements];
        for (var n = 0; n < elements; n++) transformed[n] = WorldToScreen(pos[n]);
        Pge.DrawExplicitDecal(decal, transformed, uv, col, elements);
    }

    public void DrawWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Pixel? tint = null)
    {
        var t = new[] { WorldToScreen(pos[0]), WorldToScreen(pos[1]), WorldToScreen(pos[2]), WorldToScreen(pos[3]) };
        Pge.DrawWarpedDecal(decal, t, tint ?? Pixel.WHITE);
    }

    public void DrawPartialWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
    {
        var t = new[] { WorldToScreen(pos[0]), WorldToScreen(pos[1]), WorldToScreen(pos[2]), WorldToScreen(pos[3]) };
        Pge.DrawPartialWarpedDecal(decal, t, sourcePos, sourceSize, tint ?? Pixel.WHITE);
    }

    public void DrawRotatedDecal(Vector2d<float> pos, Decal decal, float angle, Vector2d<float>? center = null,
        Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawRotatedDecal(WorldToScreen(pos), decal, angle, center ?? new Vector2d<float>(0, 0),
            DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    public void DrawPartialRotatedDecal(Vector2d<float> pos, Decal decal, float angle, Vector2d<float> center,
        Vector2d<float> sourcePos, Vector2d<float> sourceSize, Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawPartialRotatedDecal(WorldToScreen(pos), decal, angle, center, sourcePos, sourceSize,
            DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    public void DrawStringDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
        => Pge.DrawStringDecal(WorldToScreen(pos), text, col ?? Pixel.WHITE, DecalScale(scale ?? new Vector2d<float>(1, 1)));

    public void DrawStringPropDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
        => Pge.DrawStringPropDecal(WorldToScreen(pos), text, col ?? Pixel.WHITE, DecalScale(scale ?? new Vector2d<float>(1, 1)));

    public void FillRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
        => Pge.FillRectDecal(WorldToScreen(pos), Ceil(size * WorldScale), col ?? Pixel.WHITE);

    public void DrawRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
        => Pge.DrawRectDecal(WorldToScreen(pos), Ceil(size * WorldScale), col ?? Pixel.WHITE);

    public void DrawLineDecal(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null)
        => Pge.DrawLineDecal(WorldToScreen(pos1), WorldToScreen(pos2), p ?? Pixel.WHITE);

    public void GradientFillRectDecal(Vector2d<float> pos, Vector2d<float> size,
        Pixel colTL, Pixel colBL, Pixel colBR, Pixel colTR)
        => Pge.GradientFillRectDecal(WorldToScreen(pos), size * WorldScale, colTL, colBL, colBR, colTR);

    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, Pixel? tint = null)
        => Pge.DrawPolygonDecal(decal, Transform(pos), uv, tint ?? Pixel.WHITE);

    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> tints)
        => Pge.DrawPolygonDecal(decal, Transform(pos), uv, tints);

    private Vector2d<float>[] Transform(IReadOnlyList<Vector2d<float>> pos)
    {
        var t = new Vector2d<float>[pos.Count];
        for (var n = 0; n < pos.Count; n++) t[n] = WorldToScreen(pos[n]);
        return t;
    }

    private static Vector2d<float> Ceil(Vector2d<float> v) => new(MathF.Ceiling(v.X), MathF.Ceiling(v.Y));
}

// Tile-oriented variant: world units are tiles. Initialise with a tile size and query visible tiles.
public class TileTransformedView : TransformedView
{
    public TileTransformedView() { }
    public TileTransformedView(Vector2d<int> viewArea, Vector2d<int> tileSize) => Initialise(viewArea, tileSize.As<float>());

    public Vector2d<int> GetTopLeftTile() => Floor(ScreenToWorld(new Vector2d<float>(0, 0)));
    public Vector2d<int> GetBottomRightTile() => Ceiling(ScreenToWorld(ViewArea.As<float>()));
    public Vector2d<int> GetVisibleTiles() => GetBottomRightTile() - GetTopLeftTile();
    public Vector2d<int> GetTileUnderScreenPos(Vector2d<int> pos) => Floor(ScreenToWorld(pos.As<float>()));

    public Vector2d<int> GetTileOffset() => new(
        (int)((WorldOffset.X - MathF.Floor(WorldOffset.X)) * WorldScale.X),
        (int)((WorldOffset.Y - MathF.Floor(WorldOffset.Y)) * WorldScale.Y));

    private static Vector2d<int> Floor(Vector2d<float> v) => new((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y));
    private static Vector2d<int> Ceiling(Vector2d<float> v) => new((int)MathF.Ceiling(v.X), (int)MathF.Ceiling(v.Y));
}
