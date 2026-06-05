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
}
