using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Hardware3D;

// Port of olc::utils::hw3d — a 3D mesh container plus factory/ray helpers. The mesh fields match
// the shape the engine's HW3D_DrawObject consumes directly (pos = float[x,y,z,w], uv = float[u,v],
// per-vertex Pixel colour). Pair with Vector3d/Matrix4x4 and a Camera3D for the transforms.
public class Mesh
{
    public List<float[]> Pos = new();
    public List<float[]> Norm = new();
    public List<float[]> Uv = new();
    public List<Pixel> Col = new();
    public DecalStructure Layout = DecalStructure.List;
}

public static class Hw3d
{
    // Machine epsilon for float (numeric_limits<float>::epsilon), NOT C#'s float.Epsilon (denormal).
    private const float Epsilon = 1.1920929e-7f;

    private static void Push(Mesh m, Vector3d p, float nx, float ny, float nz, float u, float v)
    {
        m.Pos.Add(new[] { p.X, p.Y, p.Z, 1f });
        m.Norm.Add(new[] { nx, ny, nz, 0f });
        m.Uv.Add(new[] { u, v });
        m.Col.Add(Pixel.WHITE);
    }

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

    private static float[] Arr4(Vector3d v) => new[] { v.X, v.Y, v.Z, v.W };

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
