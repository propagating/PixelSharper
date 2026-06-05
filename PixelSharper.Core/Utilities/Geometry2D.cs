using System;
using System.Numerics;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Geometry;

// Port of a useful slice of olcUTIL_Geometry2D (olc::utils::geom2d): the shape types and the
// common Closest / Contains / Overlaps relations. olc's relations mix T and double arithmetic
// freely; here the math runs in double internally (via Geom2D helpers) and results convert back
// to T. The full intersects() matrix, polygon/ray relations, etc. are not yet ported.

// Type-constraint shorthand isn't possible in C#, so the shapes/relations repeat the same
// numeric constraint Vector2d<T> uses.
public struct Line<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public Vector2d<T> Start;
    public Vector2d<T> End;
    public Line(Vector2d<T> start = default, Vector2d<T> end = default) { Start = start; End = end; }

    public Vector2d<T> Vector() => End - Start;
    public T Length() => Vector().Magnitude();
    public T Length2() => Vector().MagnitudeSquared();
    public Vector2d<T> RPoint(T distance) => Start + Vector().Normalize() * distance;
    public Vector2d<T> UPoint(T distance) => Start + Vector() * distance;

    // Which side of the (directed) line a point lies on: -1, 0 (on), or +1.
    public int Side(Vector2d<T> point)
    {
        var d = Convert.ToDouble(Vector().CrossProduct<T, T>(point - Start));
        return d < 0 ? -1 : d > 0 ? 1 : 0;
    }
}

public struct Ray<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public Vector2d<T> Origin;
    public Vector2d<T> Direction;
    public Ray(Vector2d<T> origin = default, Vector2d<T> direction = default) { Origin = origin; Direction = direction; }
}

public struct Rect<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public Vector2d<T> Pos;
    public Vector2d<T> Size;
    public Rect(Vector2d<T> pos = default, Vector2d<T> size = default) { Pos = pos; Size = size; }

    public Vector2d<T> Middle() => Pos + Size / T.CreateChecked(2);
    public Line<T> Top() => new(Pos, new Vector2d<T>(Pos.X + Size.X, Pos.Y));
    public Line<T> Bottom() => new(new Vector2d<T>(Pos.X, Pos.Y + Size.Y), Pos + Size);
    public Line<T> Left() => new(Pos, new Vector2d<T>(Pos.X, Pos.Y + Size.Y));
    public Line<T> Right() => new(new Vector2d<T>(Pos.X + Size.X, Pos.Y), Pos + Size);
    public Line<T> Side(int i) => (i & 0b11) switch { 0 => Top(), 1 => Right(), 2 => Bottom(), _ => Left() };
    public T Area() => Size.X * Size.Y;
    public T Perimeter() => T.CreateChecked(2) * (Size.X + Size.Y);
    public int SideCount() => 4;
}

public struct Circle<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public Vector2d<T> Pos;
    public T Radius;
    public Circle(Vector2d<T> pos = default, T radius = default) { Pos = pos; Radius = radius; }

    public T Area() => T.CreateChecked(Math.PI) * Radius * Radius;
    public T Perimeter() => T.CreateChecked(2.0 * Math.PI) * Radius;
    public T Circumference() => Perimeter();
}

public struct Triangle<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public Vector2d<T> P0, P1, P2;
    public Triangle(Vector2d<T> p0 = default, Vector2d<T> p1 = default, Vector2d<T> p2 = default) { P0 = p0; P1 = p1; P2 = p2; }

    public Vector2d<T> this[int i] => i % 3 == 0 ? P0 : i % 3 == 1 ? P1 : P2;
    public Line<T> Side(int i) => new(this[i % 3], this[(i + 1) % 3]);
    public int SideCount() => 3;

    public T Area()
    {
        var a = 0.5 * Math.Abs(
            Convert.ToDouble(P0.X) * (Convert.ToDouble(P1.Y) - Convert.ToDouble(P2.Y)) +
            Convert.ToDouble(P1.X) * (Convert.ToDouble(P2.Y) - Convert.ToDouble(P0.Y)) +
            Convert.ToDouble(P2.X) * (Convert.ToDouble(P0.Y) - Convert.ToDouble(P1.Y)));
        return T.CreateChecked(a);
    }

    public T Perimeter() =>
        new Line<T>(P0, P1).Length() + new Line<T>(P1, P2).Length() + new Line<T>(P2, P0).Length();
}

// Static relations over the shape types.
public static class Geom2D
{
    public const double Epsilon = 0.001;
    public const double Pi = Math.PI;

    private static double Dx<T>(Vector2d<T> v) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Convert.ToDouble(v.X);
    private static double Dy<T>(Vector2d<T> v) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Convert.ToDouble(v.Y);

    private static double Dist2<T>(Vector2d<T> a, Vector2d<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var dx = Dx(a) - Dx(b);
        var dy = Dy(a) - Dy(b);
        return dx * dx + dy * dy;
    }

    // O--- Closest point on [shape] to a point ---O
    public static Vector2d<T> Closest<T>(Line<T> l, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var dot = dx * (Dx(p) - Dx(l.Start)) + dy * (Dy(p) - Dy(l.Start));
        var u = mag2 == 0 ? 0 : Math.Clamp(dot / mag2, 0.0, 1.0);
        return new Vector2d<T>(T.CreateChecked(Dx(l.Start) + u * dx), T.CreateChecked(Dy(l.Start) + u * dy));
    }

    public static Vector2d<T> Closest<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double dx = Dx(p) - Dx(c.Pos), dy = Dy(p) - Dy(c.Pos);
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len == 0) return c.Pos;
        var r = Convert.ToDouble(c.Radius);
        return new Vector2d<T>(T.CreateChecked(Dx(c.Pos) + dx / len * r), T.CreateChecked(Dy(c.Pos) + dy / len * r));
    }

    public static Vector2d<T> Closest<T>(Rect<T> r, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var c1 = Closest(r.Top(), p);
        var c2 = Closest(r.Bottom(), p);
        var c3 = Closest(r.Left(), p);
        var c4 = Closest(r.Right(), p);
        var best = c1; var bestD = Dist2(c1, p);
        if (Dist2(c2, p) < bestD) { best = c2; bestD = Dist2(c2, p); }
        if (Dist2(c3, p) < bestD) { best = c3; bestD = Dist2(c3, p); }
        if (Dist2(c4, p) < bestD) { best = c4; }
        return best;
    }

    public static Vector2d<T> Closest<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var p0 = Closest(new Line<T>(t.P0, t.P1), p); var d0 = Dist2(p0, p);
        var p1 = Closest(new Line<T>(t.P0, t.P2), p); var d1 = Dist2(p1, p);
        var p2 = Closest(new Line<T>(t.P1, t.P2), p); var d2 = Dist2(p2, p);
        if (d0 <= d1 && d0 <= d2) return p0;
        return d1 <= d0 && d1 <= d2 ? p1 : p2;
    }

    // O--- Contains ---O
    public static bool Contains<T>(Vector2d<T> a, Vector2d<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(a, b) < Epsilon;

    public static bool Contains<T>(Line<T> l, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var d = (Dx(p) - Dx(l.Start)) * (Dy(l.End) - Dy(l.Start)) - (Dy(p) - Dy(l.Start)) * (Dx(l.End) - Dx(l.Start));
        if (Math.Abs(d) >= Epsilon) return false;
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var u = mag2 == 0 ? 0 : (dx * (Dx(p) - Dx(l.Start)) + dy * (Dy(p) - Dy(l.Start))) / mag2;
        return u >= 0.0 && u <= 1.0;
    }

    public static bool Contains<T>(Rect<T> r, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => !(Dx(p) < Dx(r.Pos) || Dy(p) < Dy(r.Pos) ||
             Dx(p) > Dx(r.Pos) + Dx(r.Size) || Dy(p) > Dy(r.Pos) + Dy(r.Size));

    public static bool Contains<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(c.Pos, p) <= Convert.ToDouble(c.Radius) * Convert.ToDouble(c.Radius);

    public static bool Contains<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var a = 0.5 * (-Dy(t.P1) * Dx(t.P2) + Dy(t.P0) * (-Dx(t.P1) + Dx(t.P2)) + Dx(t.P0) * (Dy(t.P1) - Dy(t.P2)) + Dx(t.P1) * Dy(t.P2));
        var sign = a < 0 ? -1 : 1;
        var s = (Dy(t.P0) * Dx(t.P2) - Dx(t.P0) * Dy(t.P2) + (Dy(t.P2) - Dy(t.P0)) * Dx(p) + (Dx(t.P0) - Dx(t.P2)) * Dy(p)) * sign;
        var v = (Dx(t.P0) * Dy(t.P1) - Dy(t.P0) * Dx(t.P1) + (Dy(t.P0) - Dy(t.P1)) * Dx(p) + (Dx(t.P1) - Dx(t.P0)) * Dy(p)) * sign;
        return s >= 0 && v >= 0 && s + v <= 2 * a * sign;
    }

    public static bool Contains<T>(Rect<T> r, Rect<T> inner) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(r, inner.Pos) && Contains(r, inner.Pos + inner.Size);

    public static bool Contains<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var rad = Convert.ToDouble(c.Radius);
        return Dx(r.Pos) + rad <= Dx(c.Pos) && Dx(c.Pos) <= Dx(r.Pos) + Dx(r.Size) - rad
            && Dy(r.Pos) + rad <= Dy(c.Pos) && Dy(c.Pos) <= Dy(r.Pos) + Dy(r.Size) - rad;
    }

    public static bool Contains<T>(Circle<T> outer, Circle<T> inner) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Math.Sqrt(Dist2(inner.Pos, outer.Pos)) + Convert.ToDouble(inner.Radius) <= Convert.ToDouble(outer.Radius);

    // O--- Overlaps ---O
    public static bool Overlaps<T>(Rect<T> r, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(r, p);
    public static bool Overlaps<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(c, p);
    public static bool Overlaps<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(t, p);

    public static bool Overlaps<T>(Rect<T> a, Rect<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dx(a.Pos) <= Dx(b.Pos) + Dx(b.Size) && Dx(a.Pos) + Dx(a.Size) >= Dx(b.Pos)
        && Dy(a.Pos) <= Dy(b.Pos) + Dy(b.Size) && Dy(a.Pos) + Dy(a.Size) >= Dy(b.Pos);

    public static bool Overlaps<T>(Circle<T> a, Circle<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var r = Convert.ToDouble(a.Radius) + Convert.ToDouble(b.Radius);
        return Dist2(a.Pos, b.Pos) <= r * r;
    }

    public static bool Overlaps<T>(Circle<T> c, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var cx = Math.Clamp(Dx(c.Pos), Dx(r.Pos), Dx(r.Pos) + Dx(r.Size));
        var cy = Math.Clamp(Dy(c.Pos), Dy(r.Pos), Dy(r.Pos) + Dy(r.Size));
        var dx = cx - Dx(c.Pos); var dy = cy - Dy(c.Pos);
        return dx * dx + dy * dy - Convert.ToDouble(c.Radius) * Convert.ToDouble(c.Radius) < 0;
    }

    public static bool Overlaps<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(c, r);

    // O--- Overlaps: line/triangle pairs ---O
    public static bool Overlaps<T>(Vector2d<T> p, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(l, p);

    public static bool Overlaps<T>(Line<T> a, Line<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var dd = (Dy(b.End) - Dy(b.Start)) * (Dx(a.End) - Dx(a.Start)) - (Dx(b.End) - Dx(b.Start)) * (Dy(a.End) - Dy(a.Start));
        if (dd == 0) return false; // parallel
        var uA = ((Dx(b.End) - Dx(b.Start)) * (Dy(a.Start) - Dy(b.Start)) - (Dy(b.End) - Dy(b.Start)) * (Dx(a.Start) - Dx(b.Start))) / dd;
        var uB = ((Dx(a.End) - Dx(a.Start)) * (Dy(a.Start) - Dy(b.Start)) - (Dy(a.End) - Dy(a.Start)) * (Dx(a.Start) - Dx(b.Start))) / dd;
        return uA >= 0 && uA <= 1 && uB >= 0 && uB <= 1;
    }

    public static bool Overlaps<T>(Rect<T> r, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(r, l.Start) || Overlaps(r.Top(), l) || Overlaps(r.Bottom(), l) || Overlaps(r.Left(), l) || Overlaps(r.Right(), l);
    public static bool Overlaps<T>(Line<T> l, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(r, l);

    public static bool Overlaps<T>(Circle<T> c, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(c.Pos, Closest(l, c.Pos)) <= Convert.ToDouble(c.Radius) * Convert.ToDouble(c.Radius);
    public static bool Overlaps<T>(Line<T> l, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(c, l);

    public static bool Overlaps<T>(Triangle<T> t, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(t, l.Start) || Overlaps(t.Side(0), l) || Overlaps(t.Side(1), l) || Overlaps(t.Side(2), l);
    public static bool Overlaps<T>(Line<T> l, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, l);

    public static bool Overlaps<T>(Triangle<T> t, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(t, r.Top()) || Overlaps(t, r.Bottom()) || Overlaps(t, r.Left()) || Overlaps(t, r.Right()) || Contains(r, t.P0);
    public static bool Overlaps<T>(Rect<T> r, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, r);

    public static bool Overlaps<T>(Triangle<T> t, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(t, c.Pos) || Dist2(c.Pos, Closest(t, c.Pos)) <= Convert.ToDouble(c.Radius) * Convert.ToDouble(c.Radius);
    public static bool Overlaps<T>(Circle<T> c, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, c);

    public static bool Overlaps<T>(Triangle<T> a, Triangle<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(a, b.Side(0)) || Overlaps(a, b.Side(1)) || Overlaps(a, b.Side(2)) || Overlaps(b, a.P0);

    // O--- Intersects: returns the set of intersection points ---O
    private static List<Vector2d<T>> FilterDuplicatePoints<T>(List<Vector2d<T>> points) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var filtered = new List<Vector2d<T>>();
        foreach (var p in points)
        {
            var dup = false;
            foreach (var f in filtered)
                if (Math.Abs(Dx(p) - Dx(f)) < Epsilon && Math.Abs(Dy(p) - Dy(f)) < Epsilon) { dup = true; break; }
            if (!dup) filtered.Add(p);
        }
        return filtered;
    }

    public static List<Vector2d<T>> Intersects<T>(Line<T> l1, Line<T> l2, bool infinite = false) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double v1x = Dx(l1.End) - Dx(l1.Start), v1y = Dy(l1.End) - Dy(l1.Start);
        double v2x = Dx(l2.End) - Dx(l2.Start), v2y = Dy(l2.End) - Dy(l2.Start);
        var rd = v1x * v2y - v1y * v2x;
        if (rd == 0) return new List<Vector2d<T>>(); // parallel / colinear
        rd = 1.0 / rd;
        var rn = (v2x * (Dy(l1.Start) - Dy(l2.Start)) - v2y * (Dx(l1.Start) - Dx(l2.Start))) * rd;
        var sn = (v1x * (Dy(l1.Start) - Dy(l2.Start)) - v1y * (Dx(l1.Start) - Dx(l2.Start))) * rd;
        if (!infinite && (rn < 0 || rn > 1 || sn < 0 || sn > 1)) return new List<Vector2d<T>>();
        return new List<Vector2d<T>>
        {
            new(T.CreateChecked(Dx(l1.Start) + rn * v1x), T.CreateChecked(Dy(l1.Start) + rn * v1y))
        };
    }

    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        if (!Overlaps(c, Closest(l, c.Pos))) return new List<Vector2d<T>>(); // segment too far
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var uLine = (dx * (Dx(c.Pos) - Dx(l.Start)) + dy * (Dy(c.Pos) - Dy(l.Start))) / mag2;
        double clX = Dx(l.Start) + uLine * dx, clY = Dy(l.Start) + uLine * dy;
        var distToLine = (Dx(c.Pos) - clX) * (Dx(c.Pos) - clX) + (Dy(c.Pos) - clY) * (Dy(c.Pos) - clY);
        var r2 = Convert.ToDouble(c.Radius) * Convert.ToDouble(c.Radius);
        if (Math.Abs(distToLine - r2) < Epsilon)
            return new List<Vector2d<T>> { new(T.CreateChecked(clX), T.CreateChecked(clY)) }; // kisses

        var half = Math.Sqrt(Math.Max(0.0, r2 - distToLine));
        var nlen = Math.Sqrt(mag2);
        double nx = dx / nlen, ny = dy / nlen;
        var p1 = new Vector2d<T>(T.CreateChecked(clX + nx * half), T.CreateChecked(clY + ny * half));
        var p2 = new Vector2d<T>(T.CreateChecked(clX - nx * half), T.CreateChecked(clY - ny * half));
        var result = new List<Vector2d<T>>();
        if (Dist2(p1, Closest(l, p1)) < Epsilon * Epsilon) result.Add(p1);
        if (Dist2(p2, Closest(l, p2)) < Epsilon * Epsilon) result.Add(p2);
        return FilterDuplicatePoints(result);
    }

    public static List<Vector2d<T>> Intersects<T>(Circle<T> c1, Circle<T> c2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        if (c1.Pos == c2.Pos) return new List<Vector2d<T>>();
        double bx = Dx(c2.Pos) - Dx(c1.Pos), by = Dy(c2.Pos) - Dy(c1.Pos);
        var dist2 = bx * bx + by * by;
        double r1 = Convert.ToDouble(c1.Radius), r2 = Convert.ToDouble(c2.Radius);
        var radiusSum = r1 + r2;
        if (dist2 > radiusSum * radiusSum) return new List<Vector2d<T>>();
        if (Contains(c1, c2) || Contains(c2, c1)) return new List<Vector2d<T>>();
        var dist = Math.Sqrt(dist2);
        double nx = bx / dist, ny = by / dist;
        var ccDist = (dist2 + r1 * r1 - r2 * r2) / (2 * dist);
        double chordX = Dx(c1.Pos) + nx * ccDist, chordY = Dy(c1.Pos) + ny * ccDist;
        var halfChord = Math.Sqrt(Math.Max(0.0, r1 * r1 - ccDist * ccDist));
        double hx = -ny * halfChord, hy = nx * halfChord; // perpendicular of the unit between-vector
        return new List<Vector2d<T>>
        {
            new(T.CreateChecked(chordX + hx), T.CreateChecked(chordY + hy)),
            new(T.CreateChecked(chordX - hx), T.CreateChecked(chordY - hy))
        };
    }

    // Side-decomposed intersects (collect side intersections, then dedup).
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => FilterDuplicatePoints(Collect(l, r.Top(), r.Bottom(), r.Left(), r.Right()));

    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => FilterDuplicatePoints(Collect(l, t.Side(0), t.Side(1), t.Side(2)));

    public static List<Vector2d<T>> Intersects<T>(Rect<T> r1, Rect<T> r2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(r1, r2.Side(i)));
        return FilterDuplicatePoints(result);
    }

    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(c, r.Side(i)));
        return FilterDuplicatePoints(result);
    }

    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(t, r.Side(i)));
        return FilterDuplicatePoints(result);
    }

    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 3; i++) result.AddRange(Intersects(c, t.Side(i)));
        return FilterDuplicatePoints(result);
    }

    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t1, Triangle<T> t2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 3; i++) result.AddRange(Intersects(t1, t2.Side(i)));
        return FilterDuplicatePoints(result);
    }

    // Reverse convenience overloads.
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(r, l);
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(c, l);
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, l);
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(c, r);
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, r);
    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, c);

    private static List<Vector2d<T>> Collect<T>(Line<T> l, params Line<T>[] sides) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        foreach (var side in sides) result.AddRange(Intersects(side, l));
        return result;
    }
}
