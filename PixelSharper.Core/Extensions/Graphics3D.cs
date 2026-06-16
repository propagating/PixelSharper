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

/// <summary>2D texture coordinate carrying a perspective W in Z (olc's vec2d).</summary>
/// <remarks>
/// <para>Part of the deprecated software-3D <see cref="Gfx3d"/> path; the modern hardware path uses
/// <see cref="PixelSharper.Core.Types.Vector3d"/> / <see cref="PixelSharper.Core.Utilities.Hardware3D.Hw3d"/>.</para>
/// </remarks>
public struct Vec2d
{
    /// <summary>U, V, and perspective term.</summary>
    public float X, Y, Z;
}

/// <summary>Homogeneous 3D vector (olc's vec3d).</summary>
/// <remarks>
/// <para>Part of the deprecated software-3D <see cref="Gfx3d"/> path; the modern hardware path uses
/// <see cref="PixelSharper.Core.Types.Vector3d"/>.</para>
/// </remarks>
public struct Vec3d
{
    /// <summary>Component values plus the homogeneous W.</summary>
    public float X, Y, Z, W;
}

/// <summary>A triangle (reference type — copied and mutated heavily during clipping; a struct with array fields would alias).</summary>
public class Tri
{
    /// <summary>The three homogeneous vertex positions.</summary>
    public Vec3d[] P = new Vec3d[3];
    /// <summary>The three texture coordinates.</summary>
    public Vec2d[] T = new Vec2d[3];
    /// <summary>The three per-vertex colours.</summary>
    public Pixel[] Col = { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE };

    /// <summary>Deep copy of position/tex/colour arrays — the independent copies the clipper needs.</summary>
    /// <returns>A new <see cref="Tri"/> whose <see cref="P"/>, <see cref="T"/>, and <see cref="Col"/> arrays are independent clones of this triangle's.</returns>
    public Tri Clone() => new() { P = (Vec3d[])P.Clone(), T = (Vec2d[])T.Clone(), Col = (Pixel[])Col.Clone() };
}

/// <summary>Column-of-rows 4x4 matrix (indexed [row, col] to mirror olc's m[r][c]).</summary>
public class Mat
{
    /// <summary>Row-major 4x4 element store.</summary>
    public readonly float[,] M = new float[4, 4];
}

/// <summary>Per-triangle render options (wireframe, flat/textured fill, culling, depth, lighting).</summary>
[Flags]
public enum RenderFlags
{
    /// <summary>Draw triangle outlines only.</summary>
    Wire = 0x01,
    /// <summary>Flat-shaded fill.</summary>
    Flat = 0x02,
    /// <summary>Sample the bound texture.</summary>
    Textured = 0x04,
    /// <summary>Cull clockwise-facing triangles.</summary>
    CullCw = 0x08,
    /// <summary>Cull counter-clockwise-facing triangles.</summary>
    CullCcw = 0x10,
    /// <summary>Depth-test against the depth buffer.</summary>
    Depth = 0x20,
    /// <summary>Apply the configured light sources.</summary>
    Lights = 0x40
}

/// <summary>Kinds of light source the pipeline supports.</summary>
public enum LightType {
    /// <summary>Inactive slot.</summary>
    Disabled,
    /// <summary>Uniform base illumination.</summary>
    Ambient,
    /// <summary>Parallel light from a direction.</summary>
    Directional,
    /// <summary>Positional light (unused in olc's port).</summary>
    Point }

/// <summary>Static vector/matrix math + plane clipping for the software 3D pipeline.</summary>
public static class Gfx3dMath
{
    /// <summary>Transforms a homogeneous vector by a matrix (row-vector convention).</summary>
    /// <param name="m">The 4x4 transform matrix.</param>
    /// <param name="i">The input homogeneous vector (row vector).</param>
    /// <returns>The transformed homogeneous vector <c>i * m</c>.</returns>
    public static Vec3d MatMultiplyVector(Mat m, Vec3d i) => new()
    {
        X = i.X * m.M[0, 0] + i.Y * m.M[1, 0] + i.Z * m.M[2, 0] + i.W * m.M[3, 0],
        Y = i.X * m.M[0, 1] + i.Y * m.M[1, 1] + i.Z * m.M[2, 1] + i.W * m.M[3, 1],
        Z = i.X * m.M[0, 2] + i.Y * m.M[1, 2] + i.Z * m.M[2, 2] + i.W * m.M[3, 2],
        W = i.X * m.M[0, 3] + i.Y * m.M[1, 3] + i.Z * m.M[2, 3] + i.W * m.M[3, 3]
    };

    /// <summary>Returns the 4x4 identity matrix.</summary>
    /// <returns>A new identity <see cref="Mat"/>.</returns>
    public static Mat MatMakeIdentity()
    {
        var m = new Mat();
        m.M[0, 0] = m.M[1, 1] = m.M[2, 2] = m.M[3, 3] = 1f;
        return m;
    }

    /// <summary>Rotation about the X axis by angle a (radians).</summary>
    /// <param name="a">Rotation angle in radians.</param>
    /// <returns>A 4x4 rotation matrix about the X axis.</returns>
    public static Mat MatMakeRotationX(float a)
    {
        var m = new Mat();
        m.M[0, 0] = 1; m.M[1, 1] = MathF.Cos(a); m.M[1, 2] = MathF.Sin(a); m.M[2, 1] = -MathF.Sin(a); m.M[2, 2] = MathF.Cos(a); m.M[3, 3] = 1;
        return m;
    }

    /// <summary>Rotation about the Y axis by angle a (radians).</summary>
    /// <param name="a">Rotation angle in radians.</param>
    /// <returns>A 4x4 rotation matrix about the Y axis.</returns>
    public static Mat MatMakeRotationY(float a)
    {
        var m = new Mat();
        m.M[0, 0] = MathF.Cos(a); m.M[0, 2] = MathF.Sin(a); m.M[2, 0] = -MathF.Sin(a); m.M[1, 1] = 1; m.M[2, 2] = MathF.Cos(a); m.M[3, 3] = 1;
        return m;
    }

    /// <summary>Rotation about the Z axis by angle a (radians).</summary>
    /// <param name="a">Rotation angle in radians.</param>
    /// <returns>A 4x4 rotation matrix about the Z axis.</returns>
    public static Mat MatMakeRotationZ(float a)
    {
        var m = new Mat();
        m.M[0, 0] = MathF.Cos(a); m.M[0, 1] = MathF.Sin(a); m.M[1, 0] = -MathF.Sin(a); m.M[1, 1] = MathF.Cos(a); m.M[2, 2] = 1; m.M[3, 3] = 1;
        return m;
    }

    /// <summary>Non-uniform scale matrix.</summary>
    /// <param name="x">Scale factor along X.</param>
    /// <param name="y">Scale factor along Y.</param>
    /// <param name="z">Scale factor along Z.</param>
    /// <returns>A 4x4 scale matrix.</returns>
    public static Mat MatMakeScale(float x, float y, float z)
    {
        var m = new Mat();
        m.M[0, 0] = x; m.M[1, 1] = y; m.M[2, 2] = z; m.M[3, 3] = 1;
        return m;
    }

    /// <summary>Translation matrix.</summary>
    /// <param name="x">Translation along X.</param>
    /// <param name="y">Translation along Y.</param>
    /// <param name="z">Translation along Z.</param>
    /// <returns>A 4x4 translation matrix.</returns>
    public static Mat MatMakeTranslation(float x, float y, float z)
    {
        var m = MatMakeIdentity();
        m.M[3, 0] = x; m.M[3, 1] = y; m.M[3, 2] = z;
        return m;
    }

    /// <summary>Perspective projection matrix from FOV/aspect/near/far.</summary>
    /// <param name="fovDegrees">Vertical field of view in degrees.</param>
    /// <param name="aspect">Aspect ratio (height / width, per olc's convention).</param>
    /// <param name="near">Near clip-plane distance.</param>
    /// <param name="far">Far clip-plane distance.</param>
    /// <returns>A 4x4 perspective projection matrix.</returns>
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

    /// <summary>Matrix product a * b.</summary>
    /// <param name="a">Left-hand matrix.</param>
    /// <param name="b">Right-hand matrix.</param>
    /// <returns>The matrix product <c>a * b</c>.</returns>
    public static Mat MatMultiplyMatrix(Mat a, Mat b)
    {
        var m = new Mat();
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                m.M[r, c] = a.M[r, 0] * b.M[0, c] + a.M[r, 1] * b.M[1, c] + a.M[r, 2] * b.M[2, c] + a.M[r, 3] * b.M[3, c];
        return m;
    }

    /// <summary>Builds a look-at (camera) matrix from position, target, and up vector.</summary>
    /// <param name="pos">Camera world position.</param>
    /// <param name="target">World point the camera looks at.</param>
    /// <param name="up">Approximate world up direction (re-orthogonalised internally).</param>
    /// <returns>A point-at (camera-to-world) matrix; invert with <see cref="MatQuickInverse"/> for a view matrix.</returns>
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

    /// <summary>Fast inverse valid only for rotation+translation matrices (transpose + negated translation).</summary>
    /// <param name="m">A rotation-plus-translation matrix (e.g. the output of <see cref="MatPointAt"/>).</param>
    /// <returns>The inverse matrix.</returns>
    /// <remarks>
    /// <para>Only correct for matrices composed of rotation and translation; not a general inverse.</para>
    /// </remarks>
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

    /// <summary>Component-wise vector addition.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>The sum <c>a + b</c> with W reset to 1.</returns>
    public static Vec3d VecAdd(Vec3d a, Vec3d b) => new() { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z, W = 1 };
    /// <summary>Component-wise vector subtraction.</summary>
    /// <param name="a">Minuend.</param>
    /// <param name="b">Subtrahend.</param>
    /// <returns>The difference <c>a - b</c> with W reset to 1.</returns>
    public static Vec3d VecSub(Vec3d a, Vec3d b) => new() { X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z, W = 1 };
    /// <summary>Scalar multiply.</summary>
    /// <param name="a">Vector operand.</param>
    /// <param name="k">Scalar factor.</param>
    /// <returns>The scaled vector <c>a * k</c> with W reset to 1.</returns>
    public static Vec3d VecMul(Vec3d a, float k) => new() { X = a.X * k, Y = a.Y * k, Z = a.Z * k, W = 1 };
    /// <summary>Scalar divide.</summary>
    /// <param name="a">Vector operand.</param>
    /// <param name="k">Scalar divisor.</param>
    /// <returns>The scaled vector <c>a / k</c> with W reset to 1.</returns>
    public static Vec3d VecDiv(Vec3d a, float k) => new() { X = a.X / k, Y = a.Y / k, Z = a.Z / k, W = 1 };
    /// <summary>3-component dot product.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>The dot product of the X/Y/Z components.</returns>
    public static float VecDotProduct(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    /// <summary>Euclidean length.</summary>
    /// <param name="v">The vector to measure.</param>
    /// <returns>The magnitude of the X/Y/Z components.</returns>
    public static float VecLength(Vec3d v) => MathF.Sqrt(VecDotProduct(v, v));
    /// <summary>Returns the unit-length vector.</summary>
    /// <param name="v">The vector to normalise.</param>
    /// <returns>The unit vector in the direction of <paramref name="v"/> with W reset to 1.</returns>
    public static Vec3d VecNormalise(Vec3d v) { var l = VecLength(v); return new Vec3d { X = v.X / l, Y = v.Y / l, Z = v.Z / l, W = 1 }; }

    /// <summary>3D cross product.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>The cross product <c>a × b</c> with W reset to 1.</returns>
    public static Vec3d VecCrossProduct(Vec3d a, Vec3d b) => new()
    {
        X = a.Y * b.Z - a.Z * b.Y,
        Y = a.Z * b.X - a.X * b.Z,
        Z = a.X * b.Y - a.Y * b.X,
        W = 1
    };

    /// <summary>Intersection point of segment lineStart..lineEnd with a plane; <paramref name="t"/> is the interpolation factor.</summary>
    /// <param name="planeP">A point on the plane.</param>
    /// <param name="planeN">The plane normal (normalised internally).</param>
    /// <param name="lineStart">Segment start point.</param>
    /// <param name="lineEnd">Segment end point.</param>
    /// <param name="t">On return, the interpolation factor along the segment of the intersection (0 at <paramref name="lineStart"/>, 1 at <paramref name="lineEnd"/>).</param>
    /// <returns>The point where the segment crosses the plane.</returns>
    public static Vec3d VecIntersectPlane(Vec3d planeP, Vec3d planeN, Vec3d lineStart, Vec3d lineEnd, out float t)
    {
        planeN = VecNormalise(planeN);
        var planeD = -VecDotProduct(planeN, planeP);
        var ad = VecDotProduct(lineStart, planeN);
        var bd = VecDotProduct(lineEnd, planeN);
        t = (-planeD - ad) / (bd - ad);
        return VecAdd(lineStart, VecMul(VecSub(lineEnd, lineStart), t));
    }

    /// <summary>Clips inTri against a plane, yielding 0, 1, or 2 triangles (interpolating tex coords).</summary>
    /// <param name="planeP">A point on the clip plane.</param>
    /// <param name="planeN">The clip-plane normal (the kept half-space is the side it points toward; normalised internally).</param>
    /// <param name="inTri">The triangle to clip.</param>
    /// <param name="outTri1">On return, the first output triangle (valid when the result is at least 1).</param>
    /// <param name="outTri2">On return, the second output triangle (valid only when the result is 2).</param>
    /// <returns>The number of triangles produced: <c>0</c> (fully clipped), <c>1</c>, or <c>2</c>.</returns>
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

/// <summary>A list of triangles, loadable from a Wavefront OBJ file.</summary>
public class Mesh
{
    /// <summary>The mesh's triangles.</summary>
    public readonly List<Tri> Tris = new();

    /// <summary>Loads triangles (and optional tex coords) from an OBJ file; returns false if the file is missing.</summary>
    /// <param name="filename">Path to the Wavefront OBJ file.</param>
    /// <param name="hasTexture">Reserved for parity with olc; texture coordinates are loaded whenever the OBJ provides them.</param>
    /// <returns><c>true</c> if the file existed and was parsed into <see cref="Tris"/>; <c>false</c> if the file was missing.</returns>
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

/// <summary>The software render pipeline: transform -&gt; cull -&gt; light -&gt; near-clip -&gt; project -&gt; screen-edge-clip -&gt; raster.</summary>
/// <remarks>
/// <para>Part of the deprecated software-3D <see cref="Gfx3d"/> path (olc::GFX3D), superseded by the hardware
/// path (<see cref="PixelSharper.Core.Utilities.Hardware3D.Hw3d"/>). Provided for completeness.</para>
/// </remarks>
public class PipeLine
{
    /// <summary>A configured light source.</summary>
    private struct Light
    {
        /// <summary>Light kind, position/direction, colour, and an extra parameter.</summary>
        public LightType Type; public Vec3d Pos, Dir; public Pixel Col; public float Param;
    }

    /// <summary>Projection matrix.</summary>
    private Mat _matProj = Gfx3dMath.MatMakeIdentity();
    /// <summary>View (camera) matrix.</summary>
    private Mat _matView = Gfx3dMath.MatMakeIdentity();
    /// <summary>World (model) transform.</summary>
    private Mat _matWorld = Gfx3dMath.MatMakeIdentity();
    /// <summary>Bound texture for textured rendering.</summary>
    private Sprite _texture;
    /// <summary>Viewport origin and size in screen pixels.</summary>
    private float _viewX, _viewY, _viewW, _viewH;
    /// <summary>The four light slots.</summary>
    private readonly Light[] _lights = new Light[4];

    /// <summary>Sets the projection matrix and the screen-space viewport rectangle.</summary>
    /// <param name="fovDegrees">Vertical field of view in degrees.</param>
    /// <param name="aspect">Aspect ratio (height / width).</param>
    /// <param name="near">Near clip-plane distance.</param>
    /// <param name="far">Far clip-plane distance.</param>
    /// <param name="left">Viewport left edge in screen pixels.</param>
    /// <param name="top">Viewport top edge in screen pixels.</param>
    /// <param name="width">Viewport width in screen pixels.</param>
    /// <param name="height">Viewport height in screen pixels.</param>
    public void SetProjection(float fovDegrees, float aspect, float near, float far, float left, float top, float width, float height)
    {
        _matProj = Gfx3dMath.MatMakeProjection(fovDegrees, aspect, near, far);
        _viewX = left; _viewY = top; _viewW = width; _viewH = height;
    }

    /// <summary>Sets the view matrix from a look-at camera (inverse of the point-at transform).</summary>
    /// <param name="pos">Camera world position.</param>
    /// <param name="lookAt">World point the camera looks at.</param>
    /// <param name="up">Approximate world up direction.</param>
    public void SetCamera(Vec3d pos, Vec3d lookAt, Vec3d up)
        => _matView = Gfx3dMath.MatQuickInverse(Gfx3dMath.MatPointAt(pos, lookAt, up));

    /// <summary>Sets the world (model) transform.</summary>
    /// <param name="transform">The world/model matrix applied to triangles before the view transform.</param>
    public void SetTransform(Mat transform) => _matWorld = transform;
    /// <summary>Binds the texture used for textured rendering.</summary>
    /// <param name="texture">The sprite sampled when <see cref="RenderFlags.Textured"/> is set.</param>
    public void SetTexture(Sprite texture) => _texture = texture;

    /// <summary>Configures one of the four light slots.</summary>
    /// <param name="slot">Light slot index (0..3); out-of-range values are ignored.</param>
    /// <param name="type">The kind of light (see <see cref="LightType"/>).</param>
    /// <param name="col">Light colour.</param>
    /// <param name="pos">Light world position (used by positional lights).</param>
    /// <param name="dir">Light direction (used by directional lights).</param>
    /// <param name="param">Extra per-light parameter (unused by the ambient/directional paths).</param>
    public void SetLightSource(int slot, LightType type, Pixel col, Vec3d pos, Vec3d dir = default, float param = 0f)
    {
        if (slot is >= 0 and < 4) _lights[slot] = new Light { Type = type, Pos = pos, Dir = dir, Col = col, Param = param };
    }

    /// <summary>Renders every triangle in the list; returns the count actually rasterised.</summary>
    /// <param name="triangles">The triangles to render.</param>
    /// <param name="flags">Render options (cull/fill/depth/lighting); see <see cref="RenderFlags"/>.</param>
    /// <returns>The number of triangles actually rasterised (after culling and clipping).</returns>
    public int Render(List<Tri> triangles, RenderFlags flags = RenderFlags.CullCw | RenderFlags.Textured | RenderFlags.Depth)
        => Render(triangles, flags, 0, triangles.Count);

    /// <summary>Renders a sub-range of triangles through the full pipeline; returns the count rasterised.</summary>
    /// <param name="triangles">The triangle list to render from.</param>
    /// <param name="flags">Render options (cull/fill/depth/lighting); see <see cref="RenderFlags"/>.</param>
    /// <param name="offset">Index of the first triangle to render.</param>
    /// <param name="count">Number of triangles to render starting at <paramref name="offset"/>.</param>
    /// <returns>The number of triangles actually rasterised (after culling and clipping).</returns>
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

    /// <summary>Modulates a triangle's vertex colours by the configured ambient/directional lights.</summary>
    /// <param name="t">The triangle whose vertex colours are modulated in place.</param>
    /// <param name="normal">The triangle's (view-space) surface normal.</param>
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

    /// <summary>Draws a world-space line (transform -&gt; project -&gt; viewport, then draw).</summary>
    /// <param name="p1">World-space start point.</param>
    /// <param name="p2">World-space end point.</param>
    /// <param name="col">Line colour.</param>
    public void RenderLine(Vec3d p1, Vec3d p2, Pixel col)
    {
        var t1 = Project(p1);
        var t2 = Project(p2);
        Gfx3d.Pge.DrawLine((int)t1.X, (int)t1.Y, (int)t2.X, (int)t2.Y, col);
    }

    /// <summary>Draws a filled world-space circle, sized by projecting a radius along X.</summary>
    /// <param name="p1">World-space centre point.</param>
    /// <param name="r">World-space radius (projected along the X axis to a pixel radius).</param>
    /// <param name="col">Fill colour.</param>
    public void RenderCircleXZ(Vec3d p1, float r, Pixel col)
    {
        var t1 = Project(p1);
        var t2 = Project(new Vec3d { X = p1.X + r, Y = p1.Y, Z = p1.Z, W = 1 });
        Gfx3d.Pge.FillCircle((int)t1.X, (int)t1.Y, (int)MathF.Abs(t2.X - t1.X), col);
    }

    /// <summary>Projects a world-space point to screen space via view/projection and the viewport.</summary>
    /// <param name="p">The world-space point to project.</param>
    /// <returns>The point in screen-pixel coordinates.</returns>
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

/// <summary>Static drawing surface + depth buffer + scanline rasteriser (uses the engine via PGEX.Pge).</summary>
/// <remarks>
/// <para>The deprecated software-3D path (olc::GFX3D), superseded by the hardware path
/// (<see cref="PixelSharper.Core.Utilities.Hardware3D.Hw3d"/>). Provided for completeness.</para>
/// <para>Driven by <see cref="PipeLine"/>; call <see cref="ConfigureDisplay"/> once and
/// <see cref="ClearDepth"/> per frame before rasterising.</para>
/// </remarks>
public static class Gfx3d
{
    /// <summary>Per-pixel depth (1/w) buffer, sized to the screen.</summary>
    private static float[] _depthBuffer;

    /// <summary>The active engine (set when a PixelGameEngine is constructed); exposed for the PipeLine.</summary>
    /// <value>The current <see cref="PixelGameEngine"/> instance, forwarded from <see cref="PGEX.Pge"/>.</value>
    internal static PixelGameEngine Pge => PGEX.Pge;

    /// <summary>Allocates the depth buffer for the current screen size.</summary>
    public static void ConfigureDisplay() => _depthBuffer = new float[Pge.ScreenWidth() * Pge.ScreenHeight()];

    /// <summary>Zeroes the depth buffer (call once per frame).</summary>
    public static void ClearDepth() => Array.Clear(_depthBuffer, 0, _depthBuffer.Length);

    /// <summary>Fills a triangle flat-shaded with its first vertex colour.</summary>
    /// <param name="tri">The screen-space triangle to fill; <c>tri.Col[0]</c> is the fill colour.</param>
    public static void DrawTriangleFlat(Tri tri)
        => Pge.FillTriangle((int)tri.P[0].X, (int)tri.P[0].Y, (int)tri.P[1].X, (int)tri.P[1].Y, (int)tri.P[2].X, (int)tri.P[2].Y, tri.Col[0]);

    /// <summary>Draws a triangle's outline.</summary>
    /// <param name="tri">The screen-space triangle to outline.</param>
    /// <param name="col">Outline colour; defaults to <see cref="Pixel.WHITE"/> when <c>null</c>.</param>
    public static void DrawTriangleWire(Tri tri, Pixel? col = null)
        => Pge.DrawTriangle((int)tri.P[0].X, (int)tri.P[0].Y, (int)tri.P[1].X, (int)tri.P[1].Y, (int)tri.P[2].X, (int)tri.P[2].Y, col ?? Pixel.WHITE);

    /// <summary>Gouraud + (optionally) textured + (optionally) depth-tested scanline triangle fill.</summary>
    /// <param name="x1">First vertex X (screen pixels).</param>
    /// <param name="y1">First vertex Y (screen pixels).</param>
    /// <param name="u1">First vertex U texture coordinate (perspective-divided).</param>
    /// <param name="v1">First vertex V texture coordinate (perspective-divided).</param>
    /// <param name="w1">First vertex perspective term (1/w), also the depth value.</param>
    /// <param name="c1">First vertex colour.</param>
    /// <param name="x2">Second vertex X (screen pixels).</param>
    /// <param name="y2">Second vertex Y (screen pixels).</param>
    /// <param name="u2">Second vertex U texture coordinate.</param>
    /// <param name="v2">Second vertex V texture coordinate.</param>
    /// <param name="w2">Second vertex perspective term (1/w).</param>
    /// <param name="c2">Second vertex colour.</param>
    /// <param name="x3">Third vertex X (screen pixels).</param>
    /// <param name="y3">Third vertex Y (screen pixels).</param>
    /// <param name="u3">Third vertex U texture coordinate.</param>
    /// <param name="v3">Third vertex V texture coordinate.</param>
    /// <param name="w3">Third vertex perspective term (1/w).</param>
    /// <param name="c3">Third vertex colour.</param>
    /// <param name="spr">Texture sampled when <see cref="RenderFlags.Textured"/> is set; may be <c>null</c> for untextured fill.</param>
    /// <param name="flags">Render options controlling texturing and depth testing; see <see cref="RenderFlags"/>.</param>
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

    /// <summary>Rasterises one half of the triangle (rows yStart..yEnd); the "a" edge starts at (axBase,axY), the "b" edge at (bxBase,bxY), with per-row deltas passed in.</summary>
    /// <param name="yStart">First scanline row (inclusive).</param>
    /// <param name="yEnd">Last scanline row (inclusive).</param>
    /// <param name="axBase">Base X of the short ("a") edge.</param>
    /// <param name="axY">Reference Y for the short ("a") edge interpolation origin.</param>
    /// <param name="bxBase">Base X of the long ("b") edge.</param>
    /// <param name="bxY">Reference Y for the long ("b") edge interpolation origin.</param>
    /// <param name="aU">Short-edge starting U.</param>
    /// <param name="aV">Short-edge starting V.</param>
    /// <param name="aW">Short-edge starting perspective term (1/w).</param>
    /// <param name="aC">Short-edge starting colour.</param>
    /// <param name="bU">Long-edge starting U.</param>
    /// <param name="bV">Long-edge starting V.</param>
    /// <param name="bW">Long-edge starting perspective term (1/w).</param>
    /// <param name="bC">Long-edge starting colour.</param>
    /// <param name="dxa">Short-edge total X span over this half.</param>
    /// <param name="dua">Short-edge total U span.</param>
    /// <param name="dva">Short-edge total V span.</param>
    /// <param name="dwa">Short-edge total perspective-term span.</param>
    /// <param name="dcra">Short-edge total red span.</param>
    /// <param name="dcga">Short-edge total green span.</param>
    /// <param name="dcba">Short-edge total blue span.</param>
    /// <param name="dcaa">Short-edge total alpha span.</param>
    /// <param name="dbx">Long-edge per-row X step.</param>
    /// <param name="du2s">Long-edge per-row U step.</param>
    /// <param name="dv2s">Long-edge per-row V step.</param>
    /// <param name="dw2s">Long-edge per-row perspective-term step.</param>
    /// <param name="dcr2s">Long-edge per-row red step.</param>
    /// <param name="dcg2s">Long-edge per-row green step.</param>
    /// <param name="dcb2s">Long-edge per-row blue step.</param>
    /// <param name="dca2s">Long-edge per-row alpha step.</param>
    /// <param name="spr">Texture sampled when <see cref="RenderFlags.Textured"/> is set; may be <c>null</c>.</param>
    /// <param name="flags">Render options controlling texturing and depth testing; see <see cref="RenderFlags"/>.</param>
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

    /// <summary>Swaps two values by reference.</summary>
    /// <typeparam name="T">The type of the values being swapped.</typeparam>
    /// <param name="a">First value; receives <paramref name="b"/>'s value.</param>
    /// <param name="b">Second value; receives <paramref name="a"/>'s value.</param>
    private static void S<T>(ref T a, ref T b) => (a, b) = (b, a);
}
