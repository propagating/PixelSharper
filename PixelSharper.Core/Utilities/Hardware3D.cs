using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Hardware3D;

/// <summary>A 3D mesh container whose fields match what the engine's HW3D_DrawObject consumes directly (pos = float[x,y,z,w], uv = float[u,v], per-vertex Pixel colour).</summary>
/// <remarks>Pair with <see cref="Vector3d"/>/<see cref="Matrix4x4"/> and a <see cref="Camera3D"/> for the transforms; build instances via <see cref="Hw3d"/>.</remarks>
/// <seealso cref="Hw3d"/>
// Port of olc::utils::hw3d — a 3D mesh container plus factory/ray helpers. The mesh fields match
// the shape the engine's HW3D_DrawObject consumes directly (pos = float[x,y,z,w], uv = float[u,v],
// per-vertex Pixel colour). Pair with Vector3d/Matrix4x4 and a Camera3D for the transforms.
public class Mesh
{
    /// <summary>Per-vertex positions as float[x,y,z,w].</summary>
    public List<float[]> Pos = new();
    /// <summary>Per-vertex normals as float[x,y,z,w].</summary>
    public List<float[]> Norm = new();
    /// <summary>Per-vertex texture coordinates as float[u,v].</summary>
    public List<float[]> Uv = new();
    /// <summary>Per-vertex colours.</summary>
    public List<Pixel> Col = new();
    /// <summary>Primitive layout the vertices form (List/Strip/Fan).</summary>
    public DecalStructure Layout = DecalStructure.List;
}

/// <summary>Static factory and ray-test helpers for hw3d meshes (cube builders, OBJ loader, ray/triangle/plane/mesh intersections).</summary>
public static class Hw3d
{
    /// <summary>Machine epsilon for float (numeric_limits.float.epsilon), NOT C#'s float.Epsilon (denormal).</summary>
    // Machine epsilon for float (numeric_limits<float>::epsilon), NOT C#'s float.Epsilon (denormal).
    private const float Epsilon = 1.1920929e-7f;

    /// <summary>Appends one vertex (position, normal, UV, white colour) to the mesh.</summary>
    /// <param name="m">Mesh to append to.</param>
    /// <param name="p">Vertex position (w is set to 1).</param>
    /// <param name="nx">Normal X component.</param>
    /// <param name="ny">Normal Y component.</param>
    /// <param name="nz">Normal Z component.</param>
    /// <param name="u">Texture U coordinate.</param>
    /// <param name="v">Texture V coordinate.</param>
    private static void Push(Mesh m, Vector3d p, float nx, float ny, float nz, float u, float v)
    {
        m.Pos.Add(new[] { p.X, p.Y, p.Z, 1f });
        m.Norm.Add(new[] { nx, ny, nz, 0f });
        m.Uv.Add(new[] { u, v });
        m.Col.Add(Pixel.WHITE);
    }

    /// <summary>Builds a unit cube whose UVs index a 4x3 cube-net texture (faces laid out cross-style).</summary>
    /// <returns>A <see cref="DecalStructure.List"/>-layout unit cube mesh (36 vertices, 12 triangles).</returns>
    /// <seealso cref="CreateCube"/>
    // A unit cube whose UVs index a 4x3 cube-net texture (faces laid out cross-style).
    public static Mesh CreateSanityCube()
    {
        var m = new Mesh();
        // South
        Push(m, new Vector3d(0, 0, 0), 0, 0, -1, 0.25f, 0.5f);
        Push(m, new Vector3d(1, 0, 0), 0, 0, -1, 0.5f, 0.5f);
        Push(m, new Vector3d(1, 1, 0), 0, 0, -1, 0.5f, 0.25f);
        Push(m, new Vector3d(0, 0, 0), 0, 0, -1, 0.25f, 0.5f);
        Push(m, new Vector3d(1, 1, 0), 0, 0, -1, 0.5f, 0.25f);
        Push(m, new Vector3d(0, 1, 0), 0, 0, -1, 0.25f, 0.25f);
        // East
        Push(m, new Vector3d(1, 0, 0), 1, 0, 0, 0.5f, 0.5f);
        Push(m, new Vector3d(1, 0, 1), 1, 0, 0, 0.75f, 0.5f);
        Push(m, new Vector3d(1, 1, 1), 1, 0, 0, 0.75f, 0.25f);
        Push(m, new Vector3d(1, 0, 0), 1, 0, 0, 0.5f, 0.5f);
        Push(m, new Vector3d(1, 1, 1), 1, 0, 0, 0.75f, 0.25f);
        Push(m, new Vector3d(1, 1, 0), 1, 0, 0, 0.5f, 0.25f);
        // North
        Push(m, new Vector3d(1, 0, 1), 0, 0, 1, 0.75f, 0.5f);
        Push(m, new Vector3d(0, 0, 1), 0, 0, 1, 1.0f, 0.5f);
        Push(m, new Vector3d(0, 1, 1), 0, 0, 1, 1.0f, 0.25f);
        Push(m, new Vector3d(1, 0, 1), 0, 0, 1, 0.75f, 0.5f);
        Push(m, new Vector3d(0, 1, 1), 0, 0, 1, 1.0f, 0.25f);
        Push(m, new Vector3d(1, 1, 1), 0, 0, 1, 0.75f, 0.25f);
        // West
        Push(m, new Vector3d(0, 0, 1), -1, 0, 0, 0.0f, 0.5f);
        Push(m, new Vector3d(0, 0, 0), -1, 0, 0, 0.25f, 0.5f);
        Push(m, new Vector3d(0, 1, 0), -1, 0, 0, 0.25f, 0.25f);
        Push(m, new Vector3d(0, 0, 1), -1, 0, 0, 0.0f, 0.5f);
        Push(m, new Vector3d(0, 1, 0), -1, 0, 0, 0.25f, 0.25f);
        Push(m, new Vector3d(0, 1, 1), -1, 0, 0, 0.0f, 0.25f);
        // Top
        Push(m, new Vector3d(0, 1, 0), 0, 1, 0, 0.25f, 0.25f);
        Push(m, new Vector3d(1, 1, 0), 0, 1, 0, 0.5f, 0.25f);
        Push(m, new Vector3d(1, 1, 1), 0, 1, 0, 0.5f, 0.0f);
        Push(m, new Vector3d(0, 1, 0), 0, 1, 0, 0.25f, 0.25f);
        Push(m, new Vector3d(1, 1, 1), 0, 1, 0, 0.5f, 0.0f);
        Push(m, new Vector3d(0, 1, 1), 0, 1, 0, 0.25f, 0.0f);
        // Bottom
        Push(m, new Vector3d(0, 0, 1), 0, -1, 0, 0.25f, 0.75f);
        Push(m, new Vector3d(1, 0, 1), 0, -1, 0, 0.5f, 0.75f);
        Push(m, new Vector3d(1, 0, 0), 0, -1, 0, 0.5f, 0.5f);
        Push(m, new Vector3d(0, 0, 1), 0, -1, 0, 0.25f, 0.75f);
        Push(m, new Vector3d(1, 0, 0), 0, -1, 0, 0.5f, 0.5f);
        Push(m, new Vector3d(0, 0, 0), 0, -1, 0, 0.25f, 0.5f);
        return m;
    }

    /// <summary>Builds a box of the given size (with optional offset), each face UV-mapped to the full [0,1] square.</summary>
    /// <param name="size">Box dimensions along X, Y and Z.</param>
    /// <param name="offset">World-space offset added to every corner; defaults to the origin.</param>
    /// <returns>A <see cref="DecalStructure.List"/>-layout box mesh (36 vertices, 12 triangles).</returns>
    /// <seealso cref="CreateSanityCube"/>
    // A box of the given size (with optional offset), each face UV-mapped to the full [0,1] square.
    public static Mesh CreateCube(Vector3d size, Vector3d offset = default)
    {
        var m = new Mesh();
        var v = new[]
        {
            new Vector3d(0, 0, 0) + offset,
            new Vector3d(0, size.Y, 0) + offset,
            new Vector3d(size.X, size.Y, 0) + offset,
            new Vector3d(size.X, 0, 0) + offset,
            new Vector3d(0, 0, size.Z) + offset,
            new Vector3d(0, size.Y, size.Z) + offset,
            new Vector3d(size.X, size.Y, size.Z) + offset,
            new Vector3d(size.X, 0, size.Z) + offset,
        };
        void Face(int a, int b, int c, int d, float nx, float ny, float nz)
        {
            Push(m, v[a], nx, ny, nz, 0, 1);
            Push(m, v[b], nx, ny, nz, 0, 0);
            Push(m, v[c], nx, ny, nz, 1, 0);
            Push(m, v[a], nx, ny, nz, 0, 1);
            Push(m, v[c], nx, ny, nz, 1, 0);
            Push(m, v[d], nx, ny, nz, 1, 1);
        }
        Face(0, 1, 2, 3, 0, 0, -1); // South
        Face(3, 2, 6, 7, 1, 0, 0);  // East
        Face(7, 6, 5, 4, 0, 0, 1);  // North
        Face(4, 5, 1, 0, -1, 0, 0); // West
        Face(1, 5, 6, 2, 0, 1, 0);  // Top
        Face(7, 4, 0, 3, 0, -1, 0); // Bottom
        return m;
    }

    /// <summary>Loads a Wavefront .obj (expects v/vt/vn triangle faces; X is negated to match olc's handedness). Returns null if the file can't be opened.</summary>
    /// <param name="path">Filesystem path to the .obj file.</param>
    /// <returns>A <see cref="DecalStructure.List"/>-layout mesh of the triangle faces, or <c>null</c> if <paramref name="path"/> does not exist.</returns>
    /// <remarks>Only triangle (3-vertex) faces are emitted; quads and larger polygons are skipped. OBJ indices are 1-based.</remarks>
    // Loads a Wavefront .obj (expects v/vt/vn faces; x is negated to match olc's handedness).
    // Returns null if the file can't be opened.
    public static Mesh LoadObj(string path)
    {
        if (!File.Exists(path)) return null;

        var m = new Mesh();
        var verts = new List<Vector3d>();
        var norms = new List<Vector3d>();
        var texs = new List<Vector2d<float>>();
        var faces = new List<int[][]>();

        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        foreach (var line in File.ReadLines(path))
        {
            if (line.Length < 2) continue;
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (line[0] == 'v')
            {
                if (line[1] == 't') texs.Add(new Vector2d<float>(F(parts[1]), 1f - F(parts[2])));
                else if (line[1] == 'n') norms.Add(new Vector3d(-F(parts[1]), F(parts[2]), F(parts[3])));
                else if (line[1] == ' ') verts.Add(new Vector3d(-F(parts[1]), F(parts[2]), F(parts[3])));
            }
            else if (line[0] == 'f')
            {
                var face = new List<int[]>();
                for (var i = 1; i < parts.Length; i++)
                {
                    var idx = new List<int>();
                    foreach (var tk in parts[i].Split('/'))
                        if (tk.Length > 0) idx.Add(int.Parse(tk, CultureInfo.InvariantCulture));
                    face.Add(idx.ToArray());
                }
                faces.Add(face.ToArray());
            }
        }

        foreach (var face in faces)
        {
            if (face.Length != 3) continue; // only triangles
            foreach (var index in face)
            {
                m.Pos.Add(verts.Count > 0 && index.Length > 0 ? Arr4(verts[index[0] - 1]) : new float[] { 0, 0, 0, 0 });
                m.Uv.Add(texs.Count > 0 && index.Length > 1 ? new[] { texs[index[1] - 1].X, texs[index[1] - 1].Y } : new float[] { 0, 0 });
                m.Norm.Add(norms.Count > 0 && index.Length > 2 ? Arr4(norms[index[2] - 1]) : new float[] { 0, 0, 0, 0 });
                m.Col.Add(Pixel.WHITE);
            }
        }
        return m;
    }

    /// <summary>Packs a Vector3d into a float[x,y,z,w] array.</summary>
    /// <param name="v">Vector to pack.</param>
    /// <returns>A 4-element array <c>{ v.X, v.Y, v.Z, v.W }</c>.</returns>
    private static float[] Arr4(Vector3d v) => new[] { v.X, v.Y, v.Z, v.W };

    /// <summary>Moller-Trumbore ray/triangle test. Returns the hit point and distance t, or null on a miss.</summary>
    /// <param name="origin">Ray origin in world space.</param>
    /// <param name="dir">Ray direction in world space.</param>
    /// <param name="ta">First triangle vertex.</param>
    /// <param name="tb">Second triangle vertex.</param>
    /// <param name="tc">Third triangle vertex.</param>
    /// <returns>A tuple of the world-space hit point and the ray parameter <c>T</c> at the hit, or <c>null</c> if the ray misses or is parallel to the triangle.</returns>
    /// <remarks>
    /// <para>Implements the Moller-Trumbore algorithm: barycentric coordinates are solved via scalar triple products, rejecting hits outside the triangle or behind the origin.</para>
    /// <para>Tolerances use the machine <see cref="Epsilon"/>, not C#'s denormal <c>float.Epsilon</c>.</para>
    /// </remarks>
    // Möller–Trumbore ray/triangle test. Returns the hit point and distance t, or null.
    public static (Vector3d Point, float T)? RayVsTriangle(Vector3d origin, Vector3d dir, Vector3d ta, Vector3d tb, Vector3d tc)
    {
        var edge1 = tb - ta;
        var edge2 = tc - ta;
        var rayCrossE2 = dir.Cross(edge2);
        var det = edge1.Dot(rayCrossE2);
        if (det > -Epsilon && det < Epsilon) return null;

        var invDet = 1f / det;
        var s = origin - ta;
        var u = invDet * s.Dot(rayCrossE2);
        if ((u < 0 && MathF.Abs(u) > Epsilon) || (u > 1 && MathF.Abs(u - 1) > Epsilon)) return null;

        var sCrossE1 = s.Cross(edge1);
        var v = invDet * dir.Dot(sCrossE1);
        if ((v < 0 && MathF.Abs(v) > Epsilon) || (u + v > 1 && MathF.Abs(u + v - 1) > Epsilon)) return null;

        var t = invDet * edge2.Dot(sCrossE1);
        return t > Epsilon ? (origin + dir * t, t) : null;
    }

    /// <summary>Intersects a ray with an infinite plane. Returns the point, or null if parallel.</summary>
    /// <param name="origin">Ray origin in world space.</param>
    /// <param name="dir">Ray direction in world space.</param>
    /// <param name="planeP">A point lying on the plane.</param>
    /// <param name="planeN">The plane normal.</param>
    /// <returns>The world-space intersection point, or <c>null</c> if the ray is parallel to the plane.</returns>
    // Intersection of a ray with an (infinite) plane. Returns the point, or null if parallel.
    public static Vector3d? RayVsPlane(Vector3d origin, Vector3d dir, Vector3d planeP, Vector3d planeN)
    {
        var planeD = -planeN.Dot(planeP);
        var ad = origin.Dot(planeN);
        var bd = (origin + dir).Dot(planeN);
        var det = bd - ad;
        if (det > -Epsilon && det < Epsilon) return null;
        var t = (-planeD - ad) / det;
        return origin + dir * t;
    }

    /// <summary>Tests a ray against every triangle of a LIST-layout mesh; returns (distance, vertex indices), sorted nearest-first by default.</summary>
    /// <param name="origin">Ray origin in world space.</param>
    /// <param name="dir">Ray direction in world space.</param>
    /// <param name="m">Mesh to test; only a <see cref="DecalStructure.List"/> layout is supported.</param>
    /// <param name="sort">When <c>true</c>, the hits are sorted nearest-first by distance.</param>
    /// <returns>A list of tuples each holding the hit distance and the three vertex indices of the struck triangle; empty if the ray misses or the mesh is not <see cref="DecalStructure.List"/>.</returns>
    /// <remarks>Strip and fan layouts are not yet implemented (olc leaves these as TODO). Each triangle is tested via <see cref="RayVsTriangle"/>.</remarks>
    // Tests a ray against every triangle of a LIST-layout mesh; returns (distance, vertex indices),
    // sorted nearest-first by default.
    public static List<(float Dist, int I0, int I1, int I2)> RayVsMesh(Vector3d origin, Vector3d dir, Mesh m, bool sort = true)
    {
        var hits = new List<(float, int, int, int)>();
        if (m.Layout == DecalStructure.List)
        {
            for (var i = 0; i + 2 < m.Pos.Count; i += 3)
            {
                var p0 = m.Pos[i]; var p1 = m.Pos[i + 1]; var p2 = m.Pos[i + 2];
                var hit = RayVsTriangle(origin, dir,
                    new Vector3d(p0[0], p0[1], p0[2]),
                    new Vector3d(p1[0], p1[1], p1[2]),
                    new Vector3d(p2[0], p2[1], p2[2]));
                if (hit != null) hits.Add((hit.Value.T, i, i + 1, i + 2));
            }
        }
        // FAN / STRIP layouts: not yet implemented (olc leaves these as TODO too).
        if (sort) hits.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return hits;
    }
}
