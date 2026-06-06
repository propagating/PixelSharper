using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PixelSharper.Core.Components;

namespace PixelSharper.Core.Extensions.Gfx3d;

// Port of olcPGEX_Graphics3D (olc::GFX3D) — the deprecated software 3D engine (superseded by the
// Hardware3D util, but ported for completeness). Its own vec/mat/triangle types + a Math helper +
// a PipeLine (transform -> cull -> light -> near-clip -> project -> screen-edge-clip -> raster) +
// a gouraud/textured scanline rasterizer with a depth buffer. Uses the engine's pixel drawing.

public struct Vec2d { public float X, Y, Z; }

public struct Vec3d { public float X, Y, Z, W; }

// A triangle is a reference type (it's copied + mutated heavily during clipping; a struct with
// array fields would alias). Clone() makes the independent copies the clipper needs.
public class Tri
{
    public Vec3d[] P = new Vec3d[3];
    public Vec2d[] T = new Vec2d[3];
    public Pixel[] Col = { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE };

    public Tri Clone() => new() { P = (Vec3d[])P.Clone(), T = (Vec2d[])T.Clone(), Col = (Pixel[])Col.Clone() };
}

// Column-of-rows 4x4 (indexed [row, col] to mirror olc's m[r][c]).
public class Mat
{
    public readonly float[,] M = new float[4, 4];
}

[Flags]
public enum RenderFlags
{
    Wire = 0x01, Flat = 0x02, Textured = 0x04, CullCw = 0x08, CullCcw = 0x10, Depth = 0x20, Lights = 0x40
}

public enum LightType { Disabled, Ambient, Directional, Point }

public static class Gfx3dMath
{
    public static Vec3d MatMultiplyVector(Mat m, Vec3d i) => new()
    {
        X = i.X * m.M[0, 0] + i.Y * m.M[1, 0] + i.Z * m.M[2, 0] + i.W * m.M[3, 0],
        Y = i.X * m.M[0, 1] + i.Y * m.M[1, 1] + i.Z * m.M[2, 1] + i.W * m.M[3, 1],
        Z = i.X * m.M[0, 2] + i.Y * m.M[1, 2] + i.Z * m.M[2, 2] + i.W * m.M[3, 2],
        W = i.X * m.M[0, 3] + i.Y * m.M[1, 3] + i.Z * m.M[2, 3] + i.W * m.M[3, 3]
    };

    public static Mat MatMakeIdentity()
    {
        var m = new Mat();
        m.M[0, 0] = m.M[1, 1] = m.M[2, 2] = m.M[3, 3] = 1f;
        return m;
    }

    public static Mat MatMakeRotationX(float a)
    {
        var m = new Mat();
        m.M[0, 0] = 1; m.M[1, 1] = MathF.Cos(a); m.M[1, 2] = MathF.Sin(a); m.M[2, 1] = -MathF.Sin(a); m.M[2, 2] = MathF.Cos(a); m.M[3, 3] = 1;
        return m;
    }

    public static Mat MatMakeRotationY(float a)
    {
        var m = new Mat();
        m.M[0, 0] = MathF.Cos(a); m.M[0, 2] = MathF.Sin(a); m.M[2, 0] = -MathF.Sin(a); m.M[1, 1] = 1; m.M[2, 2] = MathF.Cos(a); m.M[3, 3] = 1;
        return m;
    }

    public static Mat MatMakeRotationZ(float a)
    {
        var m = new Mat();
        m.M[0, 0] = MathF.Cos(a); m.M[0, 1] = MathF.Sin(a); m.M[1, 0] = -MathF.Sin(a); m.M[1, 1] = MathF.Cos(a); m.M[2, 2] = 1; m.M[3, 3] = 1;
        return m;
    }

    public static Mat MatMakeScale(float x, float y, float z)
    {
        var m = new Mat();
        m.M[0, 0] = x; m.M[1, 1] = y; m.M[2, 2] = z; m.M[3, 3] = 1;
        return m;
    }

    public static Mat MatMakeTranslation(float x, float y, float z)
    {
        var m = MatMakeIdentity();
        m.M[3, 0] = x; m.M[3, 1] = y; m.M[3, 2] = z;
        return m;
    }

    public static Mat MatMakeProjection(float fovDegrees, float aspect, float near, float far)
    {
        var fovRad = 1f / MathF.Tan(fovDegrees * 0.5f / 180f * 3.14159f);
        var m = new Mat();
        m.M[0, 0] = aspect * fovRad;
        m.M[1, 1] = fovRad;
        m.M[2, 2] = far / (far - near);
        m.M[3, 2] = -far * near / (far - near);
        m.M[2, 3] = 1f;
        m.M[3, 3] = 0f;
        return m;
    }

    public static Mat MatMultiplyMatrix(Mat a, Mat b)
    {
        var m = new Mat();
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                m.M[r, c] = a.M[r, 0] * b.M[0, c] + a.M[r, 1] * b.M[1, c] + a.M[r, 2] * b.M[2, c] + a.M[r, 3] * b.M[3, c];
        return m;
    }

    public static Mat MatPointAt(Vec3d pos, Vec3d target, Vec3d up)
    {
        var fwd = VecNormalise(VecSub(target, pos));
        var a = VecMul(fwd, VecDotProduct(up, fwd));
        var newUp = VecNormalise(VecSub(up, a));
        var right = VecCrossProduct(newUp, fwd);
        var m = new Mat();
        m.M[0, 0] = right.X; m.M[0, 1] = right.Y; m.M[0, 2] = right.Z; m.M[0, 3] = 0;
        m.M[1, 0] = newUp.X; m.M[1, 1] = newUp.Y; m.M[1, 2] = newUp.Z; m.M[1, 3] = 0;
        m.M[2, 0] = fwd.X; m.M[2, 1] = fwd.Y; m.M[2, 2] = fwd.Z; m.M[2, 3] = 0;
        m.M[3, 0] = pos.X; m.M[3, 1] = pos.Y; m.M[3, 2] = pos.Z; m.M[3, 3] = 1;
        return m;
    }

    public static Mat MatQuickInverse(Mat m)
    {
        var o = new Mat();
        o.M[0, 0] = m.M[0, 0]; o.M[0, 1] = m.M[1, 0]; o.M[0, 2] = m.M[2, 0]; o.M[0, 3] = 0;
        o.M[1, 0] = m.M[0, 1]; o.M[1, 1] = m.M[1, 1]; o.M[1, 2] = m.M[2, 1]; o.M[1, 3] = 0;
        o.M[2, 0] = m.M[0, 2]; o.M[2, 1] = m.M[1, 2]; o.M[2, 2] = m.M[2, 2]; o.M[2, 3] = 0;
        o.M[3, 0] = -(m.M[3, 0] * o.M[0, 0] + m.M[3, 1] * o.M[1, 0] + m.M[3, 2] * o.M[2, 0]);
        o.M[3, 1] = -(m.M[3, 0] * o.M[0, 1] + m.M[3, 1] * o.M[1, 1] + m.M[3, 2] * o.M[2, 1]);
        o.M[3, 2] = -(m.M[3, 0] * o.M[0, 2] + m.M[3, 1] * o.M[1, 2] + m.M[3, 2] * o.M[2, 2]);
        o.M[3, 3] = 1;
        return o;
    }

    public static Vec3d VecAdd(Vec3d a, Vec3d b) => new() { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z, W = 1 };
    public static Vec3d VecSub(Vec3d a, Vec3d b) => new() { X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z, W = 1 };
    public static Vec3d VecMul(Vec3d a, float k) => new() { X = a.X * k, Y = a.Y * k, Z = a.Z * k, W = 1 };
    public static Vec3d VecDiv(Vec3d a, float k) => new() { X = a.X / k, Y = a.Y / k, Z = a.Z / k, W = 1 };
    public static float VecDotProduct(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static float VecLength(Vec3d v) => MathF.Sqrt(VecDotProduct(v, v));
    public static Vec3d VecNormalise(Vec3d v) { var l = VecLength(v); return new Vec3d { X = v.X / l, Y = v.Y / l, Z = v.Z / l, W = 1 }; }

    public static Vec3d VecCrossProduct(Vec3d a, Vec3d b) => new()
    {
        X = a.Y * b.Z - a.Z * b.Y,
        Y = a.Z * b.X - a.X * b.Z,
        Z = a.X * b.Y - a.Y * b.X,
        W = 1
    };

    public static Vec3d VecIntersectPlane(Vec3d planeP, Vec3d planeN, Vec3d lineStart, Vec3d lineEnd, out float t)
    {
        planeN = VecNormalise(planeN);
        var planeD = -VecDotProduct(planeN, planeP);
        var ad = VecDotProduct(lineStart, planeN);
        var bd = VecDotProduct(lineEnd, planeN);
        t = (-planeD - ad) / (bd - ad);
        return VecAdd(lineStart, VecMul(VecSub(lineEnd, lineStart), t));
    }

    // Clips inTri against a plane, yielding 0, 1, or 2 triangles (interpolating tex coords).
    public static int TriangleClipAgainstPlane(Vec3d planeP, Vec3d planeN, Tri inTri, out Tri outTri1, out Tri outTri2)
    {
        planeN = VecNormalise(planeN);
        outTri1 = new Tri();
        outTri2 = new Tri();

        float Dist(Vec3d p) => planeN.X * p.X + planeN.Y * p.Y + planeN.Z * p.Z - VecDotProduct(planeN, planeP);

        Vec3d[] inP = new Vec3d[3], outP = new Vec3d[3];
        Vec2d[] inT = new Vec2d[3], outT = new Vec2d[3];
        int nIn = 0, nOut = 0;

        for (var i = 0; i < 3; i++)
        {
            if (Dist(inTri.P[i]) >= 0) { inP[nIn] = inTri.P[i]; inT[nIn] = inTri.T[i]; nIn++; }
            else { outP[nOut] = inTri.P[i]; outT[nOut] = inTri.T[i]; nOut++; }
        }

        if (nIn == 0) return 0;
        if (nIn == 3) { outTri1 = inTri.Clone(); return 1; }

        static Vec2d LerpT(Vec2d from, Vec2d to, float t) => new()
        {
            X = t * (to.X - from.X) + from.X,
            Y = t * (to.Y - from.Y) + from.Y,
            Z = t * (to.Z - from.Z) + from.Z
        };

        if (nIn == 1 && nOut == 2)
        {
            for (var i = 0; i < 3; i++) outTri1.Col[i] = inTri.Col[i];
            outTri1.P[0] = inP[0]; outTri1.T[0] = inT[0];
            outTri1.P[1] = VecIntersectPlane(planeP, planeN, inP[0], outP[0], out var t1); outTri1.T[1] = LerpT(inT[0], outT[0], t1);
            outTri1.P[2] = VecIntersectPlane(planeP, planeN, inP[0], outP[1], out var t2); outTri1.T[2] = LerpT(inT[0], outT[1], t2);
            return 1;
        }

        // nIn == 2 && nOut == 1 -> a quad, expressed as two triangles.
        for (var i = 0; i < 3; i++) { outTri1.Col[i] = inTri.Col[i]; outTri2.Col[i] = inTri.Col[i]; }
        outTri1.P[0] = inP[0]; outTri1.T[0] = inT[0];
        outTri1.P[1] = inP[1]; outTri1.T[1] = inT[1];
        outTri1.P[2] = VecIntersectPlane(planeP, planeN, inP[0], outP[0], out var ta); outTri1.T[2] = LerpT(inT[0], outT[0], ta);
        outTri2.P[1] = inP[1]; outTri2.T[1] = inT[1];
        outTri2.P[0] = outTri1.P[2]; outTri2.T[0] = outTri1.T[2];
        outTri2.P[2] = VecIntersectPlane(planeP, planeN, inP[1], outP[0], out var tb); outTri2.T[2] = LerpT(inT[1], outT[0], tb);
        return 2;
    }
}

public class Mesh
{
    public readonly List<Tri> Tris = new();

    public bool LoadObjFile(string filename, bool hasTexture = false)
    {
        if (!File.Exists(filename)) return false;
        var verts = new List<Vec3d>();
        var texs = new List<Vec2d>();
        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        foreach (var line in File.ReadLines(filename))
        {
            if (line.Length < 2) continue;
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (line[0] == 'v' && line[1] == 't') texs.Add(new Vec2d { X = F(parts[1]), Y = 1f - F(parts[2]) });
            else if (line[0] == 'v' && line[1] == 'n') { /* normals not used */ }
            else if (line[0] == 'v') verts.Add(new Vec3d { X = F(parts[1]), Y = F(parts[2]), Z = F(parts[3]), W = 1 });
            else if (line[0] == 'f')
            {
                var tri = new Tri();
                for (var i = 0; i < 3; i++)
                {
                    var toks = parts[i + 1].Split('/');
                    tri.P[i] = verts[int.Parse(toks[0], CultureInfo.InvariantCulture) - 1];
                    tri.T[i] = texs.Count > 0 && toks.Length > 1 && toks[1].Length > 0
                        ? texs[int.Parse(toks[1], CultureInfo.InvariantCulture) - 1]
                        : new Vec2d();
                    tri.Col[i] = Pixel.WHITE;
                }
                Tris.Add(tri);
            }
        }
        return true;
    }
}

public class PipeLine
{
    private struct Light { public LightType Type; public Vec3d Pos, Dir; public Pixel Col; public float Param; }

    private Mat _matProj = Gfx3dMath.MatMakeIdentity();
    private Mat _matView = Gfx3dMath.MatMakeIdentity();
    private Mat _matWorld = Gfx3dMath.MatMakeIdentity();
    private Sprite _texture;
    private float _viewX, _viewY, _viewW, _viewH;
    private readonly Light[] _lights = new Light[4];

    public void SetProjection(float fovDegrees, float aspect, float near, float far, float left, float top, float width, float height)
    {
        _matProj = Gfx3dMath.MatMakeProjection(fovDegrees, aspect, near, far);
        _viewX = left; _viewY = top; _viewW = width; _viewH = height;
    }

    public void SetCamera(Vec3d pos, Vec3d lookAt, Vec3d up)
        => _matView = Gfx3dMath.MatQuickInverse(Gfx3dMath.MatPointAt(pos, lookAt, up));

    public void SetTransform(Mat transform) => _matWorld = transform;
    public void SetTexture(Sprite texture) => _texture = texture;

    public void SetLightSource(int slot, LightType type, Pixel col, Vec3d pos, Vec3d dir = default, float param = 0f)
    {
        if (slot is >= 0 and < 4) _lights[slot] = new Light { Type = type, Pos = pos, Dir = dir, Col = col, Param = param };
    }

    public int Render(List<Tri> triangles, RenderFlags flags = RenderFlags.CullCw | RenderFlags.Textured | RenderFlags.Depth)
        => Render(triangles, flags, 0, triangles.Count);

    public int Render(List<Tri> triangles, RenderFlags flags, int offset, int count)
    {
        var matWorldView = Gfx3dMath.MatMultiplyMatrix(_matWorld, _matView);
        var drawn = 0;

        for (var tx = offset; tx < offset + count; tx++)
        {
            var tri = triangles[tx];
            var t = new Tri();
            for (var i = 0; i < 3; i++) { t.T[i] = tri.T[i]; t.Col[i] = tri.Col[i]; t.P[i] = Gfx3dMath.MatMultiplyVector(matWorldView, tri.P[i]); }

            var line1 = Gfx3dMath.VecSub(t.P[1], t.P[0]);
            var line2 = Gfx3dMath.VecSub(t.P[2], t.P[0]);
            var normal = Gfx3dMath.VecNormalise(Gfx3dMath.VecCrossProduct(line1, line2));

            if ((flags & RenderFlags.CullCw) != 0 && Gfx3dMath.VecDotProduct(normal, t.P[0]) > 0f) continue;
            if ((flags & RenderFlags.CullCcw) != 0 && Gfx3dMath.VecDotProduct(normal, t.P[0]) < 0f) continue;

            if ((flags & RenderFlags.Lights) != 0) ApplyLighting(t, normal);

            // Clip against the near plane.
            var n = Gfx3dMath.TriangleClipAgainstPlane(new Vec3d { Z = 0.1f }, new Vec3d { Z = 1f }, t, out var c0, out var c1);
            var clipped = new[] { c0, c1 };

            for (var k = 0; k < n; k++)
            {
                var proj = clipped[k];
                for (var i = 0; i < 3; i++)
                {
                    var p = Gfx3dMath.MatMultiplyVector(_matProj, clipped[k].P[i]);
                    var w = p.W;
                    proj.P[i] = new Vec3d { X = p.X / w, Y = p.Y / w, Z = p.Z / w, W = w };
                    proj.T[i] = new Vec2d { X = clipped[k].T[i].X / w, Y = clipped[k].T[i].Y / w, Z = 1f / w };
                }

                // Clip against the four screen edges (queue of triangles).
                var queue = new List<Tri> { proj };
                var newTris = 1;
                for (var plane = 0; plane < 4; plane++)
                {
                    while (newTris > 0)
                    {
                        var test = queue[0];
                        queue.RemoveAt(0);
                        newTris--;
                        var add = plane switch
                        {
                            0 => Gfx3dMath.TriangleClipAgainstPlane(new Vec3d { Y = -1 }, new Vec3d { Y = 1 }, test, out c0, out c1),
                            1 => Gfx3dMath.TriangleClipAgainstPlane(new Vec3d { Y = 1 }, new Vec3d { Y = -1 }, test, out c0, out c1),
                            2 => Gfx3dMath.TriangleClipAgainstPlane(new Vec3d { X = -1 }, new Vec3d { X = 1 }, test, out c0, out c1),
                            _ => Gfx3dMath.TriangleClipAgainstPlane(new Vec3d { X = 1 }, new Vec3d { X = -1 }, test, out c0, out c1)
                        };
                        if (add >= 1) queue.Add(c0);
                        if (add >= 2) queue.Add(c1);
                    }
                    newTris = queue.Count;
                }

                foreach (var raster in queue)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var p = Gfx3dMath.VecAdd(raster.P[i], new Vec3d { X = 1, Y = 1 });
                        p.X *= 0.5f * _viewW; p.Y *= 0.5f * _viewH;
                        p = Gfx3dMath.VecAdd(p, new Vec3d { X = _viewX, Y = _viewY });
                        raster.P[i] = p;
                    }

                    if ((flags & RenderFlags.Wire) != 0)
                    {
                        Gfx3d.DrawTriangleWire(raster, Pixel.RED);
                    }
                    else
                    {
                        Gfx3d.RasterTriangle(
                            (int)raster.P[0].X, (int)raster.P[0].Y, raster.T[0].X, raster.T[0].Y, raster.T[0].Z, raster.Col[0],
                            (int)raster.P[1].X, (int)raster.P[1].Y, raster.T[1].X, raster.T[1].Y, raster.T[1].Z, raster.Col[1],
                            (int)raster.P[2].X, (int)raster.P[2].Y, raster.T[2].X, raster.T[2].Y, raster.T[2].Z, raster.Col[2],
                            _texture, flags);
                    }
                    drawn++;
                }
            }
        }

        return drawn;
    }

    private void ApplyLighting(Tri t, Vec3d normal)
    {
        Pixel ambient = new(0, 0, 0);
        float r = 0, g = 0, b = 0;
        foreach (var light in _lights)
        {
            switch (light.Type)
            {
                case LightType.Ambient: ambient = light.Col; break;
                case LightType.Directional:
                    var dir = Gfx3dMath.VecNormalise(light.Dir);
                    var amount = MathF.Max(Gfx3dMath.VecDotProduct(dir, normal), 0f);
                    r += amount * (light.Col.Red / 255f);
                    g += amount * (light.Col.Green / 255f);
                    b += amount * (light.Col.Blue / 255f);
                    break;
            }
        }
        r = MathF.Max(r, ambient.Red / 255f);
        g = MathF.Max(g, ambient.Green / 255f);
        b = MathF.Max(b, ambient.Blue / 255f);
        for (var i = 0; i < 3; i++)
            t.Col[i] = new Pixel((byte)(r * t.Col[i].Red), (byte)(g * t.Col[i].Green), (byte)(b * t.Col[i].Blue));
    }

    // World-space helpers (transform -> project -> viewport, then draw).
    public void RenderLine(Vec3d p1, Vec3d p2, Pixel col)
    {
        var t1 = Project(p1);
        var t2 = Project(p2);
        Gfx3d.Pge.DrawLine((int)t1.X, (int)t1.Y, (int)t2.X, (int)t2.Y, col);
    }

    public void RenderCircleXZ(Vec3d p1, float r, Pixel col)
    {
        var t1 = Project(p1);
        var t2 = Project(new Vec3d { X = p1.X + r, Y = p1.Y, Z = p1.Z, W = 1 });
        Gfx3d.Pge.FillCircle((int)t1.X, (int)t1.Y, (int)MathF.Abs(t2.X - t1.X), col);
    }

    private Vec3d Project(Vec3d p)
    {
        var t = Gfx3dMath.MatMultiplyVector(_matView, p);
        t = Gfx3dMath.MatMultiplyVector(_matProj, t);
        t = new Vec3d { X = t.X / t.W, Y = t.Y / t.W, Z = t.Z / t.W, W = 1 };
        t = Gfx3dMath.VecAdd(t, new Vec3d { X = 1, Y = 1 });
        t.X *= 0.5f * _viewW; t.Y *= 0.5f * _viewH;
        return Gfx3dMath.VecAdd(t, new Vec3d { X = _viewX, Y = _viewY });
    }
}

// Static drawing surface + depth buffer (uses the engine via PGEX.Pge).
public static class Gfx3d
{
    private static float[] _depthBuffer;

    // The active engine (set when a PixelGameEngine is constructed). Exposed for the PipeLine.
    internal static PixelGameEngine Pge => PGEX.Pge;

    public static void ConfigureDisplay() => _depthBuffer = new float[Pge.ScreenWidth() * Pge.ScreenHeight()];

    public static void ClearDepth() => Array.Clear(_depthBuffer, 0, _depthBuffer.Length);

    public static void DrawTriangleFlat(Tri tri)
        => Pge.FillTriangle((int)tri.P[0].X, (int)tri.P[0].Y, (int)tri.P[1].X, (int)tri.P[1].Y, (int)tri.P[2].X, (int)tri.P[2].Y, tri.Col[0]);

    public static void DrawTriangleWire(Tri tri, Pixel? col = null)
        => Pge.DrawTriangle((int)tri.P[0].X, (int)tri.P[0].Y, (int)tri.P[1].X, (int)tri.P[1].Y, (int)tri.P[2].X, (int)tri.P[2].Y, col ?? Pixel.WHITE);

    // Gouraud + (optionally) textured + (optionally) depth-tested scanline fill.
    public static void RasterTriangle(
        int x1, int y1, float u1, float v1, float w1, Pixel c1,
        int x2, int y2, float u2, float v2, float w2, Pixel c2,
        int x3, int y3, float u3, float v3, float w3, Pixel c3,
        Sprite spr, RenderFlags flags)
    {
        // Sort vertices by ascending y.
        if (y2 < y1) { S(ref y1, ref y2); S(ref x1, ref x2); S(ref u1, ref u2); S(ref v1, ref v2); S(ref w1, ref w2); S(ref c1, ref c2); }
        if (y3 < y1) { S(ref y1, ref y3); S(ref x1, ref x3); S(ref u1, ref u3); S(ref v1, ref v3); S(ref w1, ref w3); S(ref c1, ref c3); }
        if (y3 < y2) { S(ref y2, ref y3); S(ref x2, ref x3); S(ref u2, ref u3); S(ref v2, ref v3); S(ref w2, ref w3); S(ref c2, ref c3); }

        // Long edge 1->3 ("b").
        int dy2 = y3 - y1, dx2 = x3 - x1;
        float du2 = u3 - u1, dv2 = v3 - v1, dw2 = w3 - w1;
        int dcr2 = c3.Red - c1.Red, dcg2 = c3.Green - c1.Green, dcb2 = c3.Blue - c1.Blue, dca2 = c3.Alpha - c1.Alpha;
        float dbx = 0, du2s = 0, dv2s = 0, dw2s = 0, dcr2s = 0, dcg2s = 0, dcb2s = 0, dca2s = 0;
        if (dy2 != 0)
        {
            var a = MathF.Abs(dy2);
            dbx = dx2 / a; du2s = du2 / a; dv2s = dv2 / a; dw2s = dw2 / a;
            dcr2s = dcr2 / a; dcg2s = dcg2 / a; dcb2s = dcb2 / a; dca2s = dca2 / a;
        }

        // Upper half: short edge 1->2 ("a").
        Half(y1, y2, x1, y1, x1, y1, u1, v1, w1, c1, u1, v1, w1, c1,
            x2 - x1, u2 - u1, v2 - v1, w2 - w1, c2.Red - c1.Red, c2.Green - c1.Green, c2.Blue - c1.Blue, c2.Alpha - c1.Alpha,
            dbx, du2s, dv2s, dw2s, dcr2s, dcg2s, dcb2s, dca2s, spr, flags);

        // Lower half: short edge 2->3 ("a").
        Half(y2, y3, x2, y2, x1, y1, u2, v2, w2, c2, u1, v1, w1, c1,
            x3 - x2, u3 - u2, v3 - v2, w3 - w2, c3.Red - c2.Red, c3.Green - c2.Green, c3.Blue - c2.Blue, c3.Alpha - c2.Alpha,
            dbx, du2s, dv2s, dw2s, dcr2s, dcg2s, dcb2s, dca2s, spr, flags);
    }

    // Rasterises one half of the triangle (rows yStart..yEnd). The "a" edge starts at (axBase,axY)
    // and the "b" edge at (bxBase,bxY); per-row deltas are passed in.
    private static void Half(
        int yStart, int yEnd, int axBase, int axY, int bxBase, int bxY,
        float aU, float aV, float aW, Pixel aC, float bU, float bV, float bW, Pixel bC,
        int dxa, float dua, float dva, float dwa, int dcra, int dcga, int dcba, int dcaa,
        float dbx, float du2s, float dv2s, float dw2s, float dcr2s, float dcg2s, float dcb2s, float dca2s,
        Sprite spr, RenderFlags flags)
    {
        var dy = yEnd - yStart;
        if (dy == 0) return;
        var adyAbs = MathF.Abs(dy);
        float daxStep = dxa / adyAbs, duas = dua / adyAbs, dvas = dva / adyAbs, dwas = dwa / adyAbs;
        float dcras = dcra / adyAbs, dcgas = dcga / adyAbs, dcbas = dcba / adyAbs, dcaas = dcaa / adyAbs;
        var w = Pge.ScreenWidth();

        for (var i = yStart; i <= yEnd; i++)
        {
            var ax = (int)(axBase + (i - axY) * daxStep);
            var bx = (int)(bxBase + (i - bxY) * dbx);

            float su = aU + (i - axY) * duas, sv = aV + (i - axY) * dvas, sw = aW + (i - axY) * dwas;
            float eu = bU + (i - bxY) * du2s, ev = bV + (i - bxY) * dv2s, ew = bW + (i - bxY) * dw2s;
            float sr = aC.Red + (i - axY) * dcras, sg = aC.Green + (i - axY) * dcgas, sb = aC.Blue + (i - axY) * dcbas, sa = aC.Alpha + (i - axY) * dcaas;
            float er = bC.Red + (i - bxY) * dcr2s, eg = bC.Green + (i - bxY) * dcg2s, eb = bC.Blue + (i - bxY) * dcb2s, ea = bC.Alpha + (i - bxY) * dca2s;

            if (ax > bx)
            {
                S(ref ax, ref bx); S(ref su, ref eu); S(ref sv, ref ev); S(ref sw, ref ew);
                S(ref sr, ref er); S(ref sg, ref eg); S(ref sb, ref eb); S(ref sa, ref ea);
            }

            var tStep = 1f / (bx - ax);
            var t = 0f;
            for (var j = ax; j < bx; j++)
            {
                var tu = (1 - t) * su + t * eu;
                var tv = (1 - t) * sv + t * ev;
                var tw = (1 - t) * sw + t * ew;
                float pr = (1 - t) * sr + t * er, pg = (1 - t) * sg + t * eg, pb = (1 - t) * sb + t * eb, pa = (1 - t) * sa + t * ea;

                if ((flags & RenderFlags.Textured) != 0 && spr != null)
                {
                    var s = spr.SamplePixel(tu / tw, tv / tw);
                    pr *= s.Red / 255f; pg *= s.Green / 255f; pb *= s.Blue / 255f; pa *= s.Alpha / 255f;
                }

                var px = new Pixel((byte)pr, (byte)pg, (byte)pb, (byte)pa);
                if ((flags & RenderFlags.Depth) != 0)
                {
                    if (tw > _depthBuffer[i * w + j] && Pge.Draw(j, i, px)) _depthBuffer[i * w + j] = tw;
                }
                else
                {
                    Pge.Draw(j, i, px);
                }
                t += tStep;
            }
        }
    }

    private static void S<T>(ref T a, ref T b) => (a, b) = (b, a);
}
