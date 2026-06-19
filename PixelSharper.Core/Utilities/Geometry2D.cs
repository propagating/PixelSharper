using System;
using System.Numerics;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Geometry;

// Port of a useful slice of olcUTIL_Geometry2D (olc::utils::geom2d): the shape types and the
// Closest / Contains / Overlaps relations, the full Intersects matrix (incl. ray pairs),
// EnvelopeC/EnvelopeR bounding shapes, and ray Collision/Reflect (line/rect/circle/triangle).
// olc's relations mix T and double arithmetic freely; here the math runs in double internally (via
// Geom2D helpers) and converts back to T. (olc's `polygon` is a bare data struct with no relations.)

/// <summary>A line segment between two points (olc geom2d line). T is the numeric component type.</summary>
/// <typeparam name="T">The numeric component type.</typeparam>
public struct Line<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The start point.</summary>
    public Vector2d<T> Start;
    /// <summary>The end point.</summary>
    public Vector2d<T> End;
    /// <summary>Constructs a line from its endpoints.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="end">The end point.</param>
    public Line(Vector2d<T> start = default, Vector2d<T> end = default) { Start = start; End = end; }

    /// <summary>The displacement vector from start to end.</summary>
    /// <returns>The vector <c>End - Start</c>.</returns>
    public Vector2d<T> Vector() => End - Start;
    /// <summary>The segment length.</summary>
    /// <returns>The Euclidean length of the segment.</returns>
    /// <seealso cref="Length2"/>
    public T Length() => Vector().Magnitude();
    /// <summary>The squared segment length (no square root).</summary>
    /// <returns>The squared length of the segment.</returns>
    /// <seealso cref="Length"/>
    public T Length2() => Vector().MagnitudeSquared();
    /// <summary>The point a real-world distance along the segment direction from start.</summary>
    /// <param name="distance">The real-world distance from <see cref="Start"/> along the segment direction.</param>
    /// <returns>The point at the given distance along the (normalised) segment direction.</returns>
    /// <seealso cref="UPoint"/>
    public Vector2d<T> RPoint(T distance) => Start + Vector().Normalize() * distance;
    /// <summary>The point at parameter distance (0..1) along the segment from start.</summary>
    /// <param name="distance">The parameter (0 at <see cref="Start"/>, 1 at <see cref="End"/>) along the segment.</param>
    /// <returns>The interpolated point at the given parameter.</returns>
    /// <seealso cref="RPoint"/>
    public Vector2d<T> UPoint(T distance) => Start + Vector() * distance;

    /// <summary>Which side of the directed line a point lies on: -1, 0 (on), or +1.</summary>
    /// <param name="point">The point to classify.</param>
    /// <returns><c>-1</c> on one side, <c>+1</c> on the other, or <c>0</c> if the point is on the line.</returns>
    public int Side(Vector2d<T> point)
    {
        var d = double.CreateChecked(Vector().CrossProduct<T, T>(point - Start));
        return d < 0 ? -1 : d > 0 ? 1 : 0;
    }
}

/// <summary>A ray: an origin and a direction (olc geom2d ray). T is the numeric component type.</summary>
/// <typeparam name="T">The numeric component type.</typeparam>
public struct Ray<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The ray's origin point.</summary>
    public Vector2d<T> Origin;
    /// <summary>The ray's direction vector.</summary>
    public Vector2d<T> Direction;
    /// <summary>Constructs a ray from an origin and direction.</summary>
    /// <param name="origin">The ray's origin point.</param>
    /// <param name="direction">The ray's direction vector.</param>
    public Ray(Vector2d<T> origin = default, Vector2d<T> direction = default) { Origin = origin; Direction = direction; }
}

/// <summary>An axis-aligned rectangle by position and size (olc geom2d rect). T is the numeric component type.</summary>
/// <typeparam name="T">The numeric component type.</typeparam>
public struct Rect<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The top-left corner position.</summary>
    public Vector2d<T> Pos;
    /// <summary>The width and height.</summary>
    public Vector2d<T> Size;
    /// <summary>Constructs a rectangle from position and size.</summary>
    /// <param name="pos">The top-left corner position.</param>
    /// <param name="size">The width and height.</param>
    public Rect(Vector2d<T> pos, Vector2d<T> size) { Pos = pos; Size = size; }

    /// <summary>Constructs a rectangle at pos with olc's default unit size (1,1).</summary>
    /// <param name="pos">The top-left corner position.</param>
    // olc defaults: pos = {0,0}, size = {1,1}. {1,1} isn't a compile-time constant, so these
    // are expressed as overloads rather than C# default parameters.
    public Rect(Vector2d<T> pos) : this(pos, new Vector2d<T>(T.One, T.One)) { }
    /// <summary>Constructs a rectangle at origin with olc's default unit size (1,1).</summary>
    public Rect() : this(default, new Vector2d<T>(T.One, T.One)) { }

    /// <summary>The centre point.</summary>
    /// <returns>The rectangle's centre point.</returns>
    public Vector2d<T> Middle() => Pos + Size / T.CreateChecked(2);
    /// <summary>The top edge as a line.</summary>
    /// <returns>The top edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Top() => new(Pos, new Vector2d<T>(Pos.X + Size.X, Pos.Y));
    /// <summary>The bottom edge as a line.</summary>
    /// <returns>The bottom edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Bottom() => new(new Vector2d<T>(Pos.X, Pos.Y + Size.Y), Pos + Size);
    /// <summary>The left edge as a line.</summary>
    /// <returns>The left edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Left() => new(Pos, new Vector2d<T>(Pos.X, Pos.Y + Size.Y));
    /// <summary>The right edge as a line.</summary>
    /// <returns>The right edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Right() => new(new Vector2d<T>(Pos.X + Size.X, Pos.Y), Pos + Size);
    /// <summary>Edge i (0=top, 1=right, 2=bottom, 3=left) as a line.</summary>
    /// <param name="i">The edge index, masked to two bits: 0=top, 1=right, 2=bottom, 3=left.</param>
    /// <returns>The selected edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Side(int i) => (i & 0b11) switch { 0 => Top(), 1 => Right(), 2 => Bottom(), _ => Left() };
    /// <summary>The rectangle's area.</summary>
    /// <returns>The area <c>Size.X * Size.Y</c>.</returns>
    public T Area() => Size.X * Size.Y;
    /// <summary>The rectangle's perimeter.</summary>
    /// <returns>The perimeter <c>2 * (Size.X + Size.Y)</c>.</returns>
    public T Perimeter() => T.CreateChecked(2) * (Size.X + Size.Y);
    /// <summary>The number of sides (always 4).</summary>
    /// <returns>The constant <c>4</c>.</returns>
    public int SideCount() => 4;
}

/// <summary>A circle by centre and radius (olc geom2d circle). T is the numeric component type.</summary>
/// <typeparam name="T">The numeric component type.</typeparam>
public struct Circle<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The centre position.</summary>
    public Vector2d<T> Pos;
    /// <summary>The radius.</summary>
    public T Radius;
    /// <summary>Constructs a circle from centre and radius.</summary>
    /// <param name="pos">The centre position.</param>
    /// <param name="radius">The radius.</param>
    public Circle(Vector2d<T> pos = default, T radius = default) { Pos = pos; Radius = radius; }

    /// <summary>The enclosed area.</summary>
    /// <returns>The area <c>pi * r^2</c>.</returns>
    public T Area() => T.CreateChecked(Math.PI) * Radius * Radius;
    /// <summary>The perimeter (circumference).</summary>
    /// <returns>The circumference <c>2 * pi * r</c>.</returns>
    /// <seealso cref="Circumference"/>
    public T Perimeter() => T.CreateChecked(2.0 * Math.PI) * Radius;
    /// <summary>The circumference (alias for the perimeter).</summary>
    /// <returns>The circumference (same as <see cref="Perimeter"/>).</returns>
    /// <seealso cref="Perimeter"/>
    public T Circumference() => Perimeter();
}

/// <summary>A triangle defined by three points (olc geom2d triangle). T is the numeric component type.</summary>
/// <typeparam name="T">The numeric component type.</typeparam>
public struct Triangle<T> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The three vertices.</summary>
    public Vector2d<T> P0, P1, P2;
    /// <summary>Constructs a triangle from its three vertices.</summary>
    /// <param name="p0">The first vertex.</param>
    /// <param name="p1">The second vertex.</param>
    /// <param name="p2">The third vertex.</param>
    public Triangle(Vector2d<T> p0 = default, Vector2d<T> p1 = default, Vector2d<T> p2 = default) { P0 = p0; P1 = p1; P2 = p2; }

    /// <summary>Vertex i, wrapping modulo 3.</summary>
    /// <param name="i">The vertex index (taken modulo 3).</param>
    /// <value>The vertex at index <paramref name="i"/> modulo 3.</value>
    /// <returns>The vertex at index <paramref name="i"/> modulo 3.</returns>
    public Vector2d<T> this[int i] => i % 3 == 0 ? P0 : i % 3 == 1 ? P1 : P2;
    /// <summary>Edge i (between vertex i and i+1) as a line.</summary>
    /// <param name="i">The edge index; the edge runs from vertex <c>i % 3</c> to <c>(i + 1) % 3</c>.</param>
    /// <returns>The selected edge as a <see cref="Line{T}"/>.</returns>
    public Line<T> Side(int i) => new(this[i % 3], this[(i + 1) % 3]);
    /// <summary>The number of sides (always 3).</summary>
    /// <returns>The constant <c>3</c>.</returns>
    public int SideCount() => 3;

    /// <summary>The triangle's area (shoelace formula).</summary>
    /// <returns>The triangle's area computed via the shoelace formula.</returns>
    public T Area()
    {
        var a = 0.5 * Math.Abs(
            double.CreateChecked(P0.X) * (double.CreateChecked(P1.Y) - double.CreateChecked(P2.Y)) +
            double.CreateChecked(P1.X) * (double.CreateChecked(P2.Y) - double.CreateChecked(P0.Y)) +
            double.CreateChecked(P2.X) * (double.CreateChecked(P0.Y) - double.CreateChecked(P1.Y)));
        return T.CreateChecked(a);
    }

    /// <summary>The triangle's perimeter (sum of side lengths).</summary>
    /// <returns>The sum of the three side lengths.</returns>
    public T Perimeter() =>
        new Line<T>(P0, P1).Length() + new Line<T>(P1, P2).Length() + new Line<T>(P2, P0).Length();
}

/// <summary>Static geometric relations (Closest/Contains/Overlaps/Intersects/Collision/Reflect/Envelope) over the shape types; math runs in double internally.</summary>
public static class Geom2D
{
    /// <summary>Tolerance used for on-shape and duplicate-point tests.</summary>
    public const double Epsilon = 0.001;
    /// <summary>Convenience constant for pi.</summary>
    public const double Pi = Math.PI;

    /// <summary>The X component of a vector as a double.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="v">The vector.</param>
    /// <returns>The X component converted to <c>double</c>.</returns>
    private static double Dx<T>(Vector2d<T> v) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => double.CreateChecked(v.X);
    /// <summary>The Y component of a vector as a double.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="v">The vector.</param>
    /// <returns>The Y component converted to <c>double</c>.</returns>
    private static double Dy<T>(Vector2d<T> v) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => double.CreateChecked(v.Y);

    /// <summary>Squared distance between two points, computed in double.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The squared distance between the points as a <c>double</c>.</returns>
    private static double Dist2<T>(Vector2d<T> a, Vector2d<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var dx = Dx(a) - Dx(b);
        var dy = Dy(a) - Dy(b);
        return dx * dx + dy * dy;
    }

    // O--- Closest point on [shape] to a point ---O
    /// <summary>The closest point on a line segment to p.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="p">The query point.</param>
    /// <returns>The point on <paramref name="l"/> nearest to <paramref name="p"/>.</returns>
    /// <remarks><para>The relation math runs in <c>double</c> internally and converts back to <typeparamref name="T"/>.</para></remarks>
    /// <seealso cref="Closest{T}(Circle{T}, Vector2d{T})"/>
    /// <seealso cref="Closest{T}(Rect{T}, Vector2d{T})"/>
    /// <seealso cref="Closest{T}(Triangle{T}, Vector2d{T})"/>
    public static Vector2d<T> Closest<T>(Line<T> l, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var dot = dx * (Dx(p) - Dx(l.Start)) + dy * (Dy(p) - Dy(l.Start));
        var u = mag2 == 0 ? 0 : Math.Clamp(dot / mag2, 0.0, 1.0);
        return new Vector2d<T>(T.CreateChecked(Dx(l.Start) + u * dx), T.CreateChecked(Dy(l.Start) + u * dy));
    }

    /// <summary>The closest point on a circle's edge to p (returns the centre if p is the centre).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="p">The query point.</param>
    /// <returns>The point on <paramref name="c"/>'s edge nearest to <paramref name="p"/>, or the centre if <paramref name="p"/> is the centre.</returns>
    /// <seealso cref="Closest{T}(Line{T}, Vector2d{T})"/>
    public static Vector2d<T> Closest<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double dx = Dx(p) - Dx(c.Pos), dy = Dy(p) - Dy(c.Pos);
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len == 0) return c.Pos;
        var r = double.CreateChecked(c.Radius);
        return new Vector2d<T>(T.CreateChecked(Dx(c.Pos) + dx / len * r), T.CreateChecked(Dy(c.Pos) + dy / len * r));
    }

    /// <summary>The closest point on a rectangle's perimeter to p.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns>The point on <paramref name="r"/>'s perimeter nearest to <paramref name="p"/>.</returns>
    /// <seealso cref="Closest{T}(Line{T}, Vector2d{T})"/>
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

    /// <summary>The closest point on a triangle's perimeter to p.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns>The point on <paramref name="t"/>'s perimeter nearest to <paramref name="p"/>.</returns>
    /// <seealso cref="Closest{T}(Line{T}, Vector2d{T})"/>
    public static Vector2d<T> Closest<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var p0 = Closest(new Line<T>(t.P0, t.P1), p); var d0 = Dist2(p0, p);
        var p1 = Closest(new Line<T>(t.P0, t.P2), p); var d1 = Dist2(p1, p);
        var p2 = Closest(new Line<T>(t.P1, t.P2), p); var d2 = Dist2(p2, p);
        if (d0 <= d1 && d0 <= d2) return p0;
        return d1 <= d0 && d1 <= d2 ? p1 : p2;
    }

    // O--- Contains ---O
    /// <summary>True if two points coincide within Epsilon.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><c>true</c> if the points coincide within <see cref="Epsilon"/>; otherwise <c>false</c>.</returns>
    public static bool Contains<T>(Vector2d<T> a, Vector2d<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(a, b) < Epsilon;

    /// <summary>True if a point lies on a line segment (within Epsilon).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> lies on <paramref name="l"/> within <see cref="Epsilon"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Vector2d{T}, Line{T})"/>
    public static bool Contains<T>(Line<T> l, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var d = (Dx(p) - Dx(l.Start)) * (Dy(l.End) - Dy(l.Start)) - (Dy(p) - Dy(l.Start)) * (Dx(l.End) - Dx(l.Start));
        if (Math.Abs(d) >= Epsilon) return false;
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var u = mag2 == 0 ? 0 : (dx * (Dx(p) - Dx(l.Start)) + dy * (Dy(p) - Dy(l.Start))) / mag2;
        return u >= 0.0 && u <= 1.0;
    }

    /// <summary>True if a point is inside (or on) the rectangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> is inside or on <paramref name="r"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Rect{T}, Vector2d{T})"/>
    public static bool Contains<T>(Rect<T> r, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => !(Dx(p) < Dx(r.Pos) || Dy(p) < Dy(r.Pos) ||
             Dx(p) > Dx(r.Pos) + Dx(r.Size) || Dy(p) > Dy(r.Pos) + Dy(r.Size));

    /// <summary>True if a point is inside (or on) the circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> is inside or on <paramref name="c"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Circle{T}, Vector2d{T})"/>
    public static bool Contains<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(c.Pos, p) <= double.CreateChecked(c.Radius) * double.CreateChecked(c.Radius);

    /// <summary>True if a point is inside the triangle (barycentric test).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> is inside <paramref name="t"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Triangle{T}, Vector2d{T})"/>
    public static bool Contains<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var a = 0.5 * (-Dy(t.P1) * Dx(t.P2) + Dy(t.P0) * (-Dx(t.P1) + Dx(t.P2)) + Dx(t.P0) * (Dy(t.P1) - Dy(t.P2)) + Dx(t.P1) * Dy(t.P2));
        var sign = a < 0 ? -1 : 1;
        var s = (Dy(t.P0) * Dx(t.P2) - Dx(t.P0) * Dy(t.P2) + (Dy(t.P2) - Dy(t.P0)) * Dx(p) + (Dx(t.P0) - Dx(t.P2)) * Dy(p)) * sign;
        var v = (Dx(t.P0) * Dy(t.P1) - Dy(t.P0) * Dx(t.P1) + (Dy(t.P0) - Dy(t.P1)) * Dx(p) + (Dx(t.P1) - Dx(t.P0)) * Dy(p)) * sign;
        return s >= 0 && v >= 0 && s + v <= 2 * a * sign;
    }

    /// <summary>True if the rectangle fully contains the inner rectangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The outer rectangle.</param>
    /// <param name="inner">The candidate inner rectangle.</param>
    /// <returns><c>true</c> if <paramref name="r"/> fully contains <paramref name="inner"/>; otherwise <c>false</c>.</returns>
    public static bool Contains<T>(Rect<T> r, Rect<T> inner) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(r, inner.Pos) && Contains(r, inner.Pos + inner.Size);

    /// <summary>True if the rectangle fully contains the circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The outer rectangle.</param>
    /// <param name="c">The candidate inner circle.</param>
    /// <returns><c>true</c> if <paramref name="r"/> fully contains <paramref name="c"/>; otherwise <c>false</c>.</returns>
    public static bool Contains<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var rad = double.CreateChecked(c.Radius);
        return Dx(r.Pos) + rad <= Dx(c.Pos) && Dx(c.Pos) <= Dx(r.Pos) + Dx(r.Size) - rad
            && Dy(r.Pos) + rad <= Dy(c.Pos) && Dy(c.Pos) <= Dy(r.Pos) + Dy(r.Size) - rad;
    }

    /// <summary>True if the outer circle fully contains the inner circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="outer">The outer circle.</param>
    /// <param name="inner">The candidate inner circle.</param>
    /// <returns><c>true</c> if <paramref name="outer"/> fully contains <paramref name="inner"/>; otherwise <c>false</c>.</returns>
    public static bool Contains<T>(Circle<T> outer, Circle<T> inner) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Math.Sqrt(Dist2(inner.Pos, outer.Pos)) + double.CreateChecked(inner.Radius) <= double.CreateChecked(outer.Radius);

    // O--- Overlaps ---O
    /// <summary>True if a point overlaps the rectangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> overlaps <paramref name="r"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Contains{T}(Rect{T}, Vector2d{T})"/>
    public static bool Overlaps<T>(Rect<T> r, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(r, p);
    /// <summary>True if a point overlaps the circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> overlaps <paramref name="c"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Contains{T}(Circle{T}, Vector2d{T})"/>
    public static bool Overlaps<T>(Circle<T> c, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(c, p);
    /// <summary>True if a point overlaps the triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="p">The query point.</param>
    /// <returns><c>true</c> if <paramref name="p"/> overlaps <paramref name="t"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Contains{T}(Triangle{T}, Vector2d{T})"/>
    public static bool Overlaps<T>(Triangle<T> t, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(t, p);

    /// <summary>True if two rectangles overlap (AABB test).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns><c>true</c> if the rectangles overlap; otherwise <c>false</c>.</returns>
    public static bool Overlaps<T>(Rect<T> a, Rect<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dx(a.Pos) <= Dx(b.Pos) + Dx(b.Size) && Dx(a.Pos) + Dx(a.Size) >= Dx(b.Pos)
        && Dy(a.Pos) <= Dy(b.Pos) + Dy(b.Size) && Dy(a.Pos) + Dy(a.Size) >= Dy(b.Pos);

    /// <summary>True if two circles overlap.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first circle.</param>
    /// <param name="b">The second circle.</param>
    /// <returns><c>true</c> if the circles overlap; otherwise <c>false</c>.</returns>
    public static bool Overlaps<T>(Circle<T> a, Circle<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var r = double.CreateChecked(a.Radius) + double.CreateChecked(b.Radius);
        return Dist2(a.Pos, b.Pos) <= r * r;
    }

    /// <summary>True if a circle overlaps a rectangle (clamp-to-rect distance test).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns><c>true</c> if <paramref name="c"/> overlaps <paramref name="r"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Rect{T}, Circle{T})"/>
    public static bool Overlaps<T>(Circle<T> c, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var cx = Math.Clamp(Dx(c.Pos), Dx(r.Pos), Dx(r.Pos) + Dx(r.Size));
        var cy = Math.Clamp(Dy(c.Pos), Dy(r.Pos), Dy(r.Pos) + Dy(r.Size));
        var dx = cx - Dx(c.Pos); var dy = cy - Dy(c.Pos);
        return dx * dx + dy * dy - double.CreateChecked(c.Radius) * double.CreateChecked(c.Radius) < 0;
    }

    /// <summary>True if a rectangle overlaps a circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="c">The circle.</param>
    /// <returns><c>true</c> if <paramref name="r"/> overlaps <paramref name="c"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Circle{T}, Rect{T})"/>
    public static bool Overlaps<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(c, r);

    // O--- Overlaps: line/triangle pairs ---O
    /// <summary>True if a point lies on a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="p">The query point.</param>
    /// <param name="l">The line segment.</param>
    /// <returns><c>true</c> if <paramref name="p"/> lies on <paramref name="l"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Contains{T}(Line{T}, Vector2d{T})"/>
    public static bool Overlaps<T>(Vector2d<T> p, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Contains(l, p);

    /// <summary>True if two line segments intersect.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first line segment.</param>
    /// <param name="b">The second line segment.</param>
    /// <returns><c>true</c> if the segments intersect; <c>false</c> if they are parallel or disjoint.</returns>
    /// <seealso cref="Intersects{T}(Line{T}, Line{T}, bool)"/>
    public static bool Overlaps<T>(Line<T> a, Line<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var dd = (Dy(b.End) - Dy(b.Start)) * (Dx(a.End) - Dx(a.Start)) - (Dx(b.End) - Dx(b.Start)) * (Dy(a.End) - Dy(a.Start));
        if (dd == 0) return false; // parallel
        var uA = ((Dx(b.End) - Dx(b.Start)) * (Dy(a.Start) - Dy(b.Start)) - (Dy(b.End) - Dy(b.Start)) * (Dx(a.Start) - Dx(b.Start))) / dd;
        var uB = ((Dx(a.End) - Dx(a.Start)) * (Dy(a.Start) - Dy(b.Start)) - (Dy(a.End) - Dy(a.Start)) * (Dx(a.Start) - Dx(b.Start))) / dd;
        return uA >= 0 && uA <= 1 && uB >= 0 && uB <= 1;
    }

    /// <summary>True if a rectangle overlaps a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns><c>true</c> if <paramref name="r"/> overlaps <paramref name="l"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Line{T}, Rect{T})"/>
    public static bool Overlaps<T>(Rect<T> r, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(r, l.Start) || Overlaps(r.Top(), l) || Overlaps(r.Bottom(), l) || Overlaps(r.Left(), l) || Overlaps(r.Right(), l);
    /// <summary>True if a line segment overlaps a rectangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns><c>true</c> if <paramref name="l"/> overlaps <paramref name="r"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Rect{T}, Line{T})"/>
    public static bool Overlaps<T>(Line<T> l, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(r, l);

    /// <summary>True if a circle overlaps a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns><c>true</c> if <paramref name="c"/> overlaps <paramref name="l"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Line{T}, Circle{T})"/>
    public static bool Overlaps<T>(Circle<T> c, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Dist2(c.Pos, Closest(l, c.Pos)) <= double.CreateChecked(c.Radius) * double.CreateChecked(c.Radius);
    /// <summary>True if a line segment overlaps a circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="c">The circle.</param>
    /// <returns><c>true</c> if <paramref name="l"/> overlaps <paramref name="c"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Circle{T}, Line{T})"/>
    public static bool Overlaps<T>(Line<T> l, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(c, l);

    /// <summary>True if a triangle overlaps a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns><c>true</c> if <paramref name="t"/> overlaps <paramref name="l"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Line{T}, Triangle{T})"/>
    public static bool Overlaps<T>(Triangle<T> t, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(t, l.Start) || Overlaps(t.Side(0), l) || Overlaps(t.Side(1), l) || Overlaps(t.Side(2), l);
    /// <summary>True if a line segment overlaps a triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="t">The triangle.</param>
    /// <returns><c>true</c> if <paramref name="l"/> overlaps <paramref name="t"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Triangle{T}, Line{T})"/>
    public static bool Overlaps<T>(Line<T> l, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, l);

    /// <summary>True if a triangle overlaps a rectangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns><c>true</c> if <paramref name="t"/> overlaps <paramref name="r"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Rect{T}, Triangle{T})"/>
    public static bool Overlaps<T>(Triangle<T> t, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(t, r.Top()) || Overlaps(t, r.Bottom()) || Overlaps(t, r.Left()) || Overlaps(t, r.Right()) || Contains(r, t.P0);
    /// <summary>True if a rectangle overlaps a triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="t">The triangle.</param>
    /// <returns><c>true</c> if <paramref name="r"/> overlaps <paramref name="t"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Triangle{T}, Rect{T})"/>
    public static bool Overlaps<T>(Rect<T> r, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, r);

    /// <summary>True if a triangle overlaps a circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="c">The circle.</param>
    /// <returns><c>true</c> if <paramref name="t"/> overlaps <paramref name="c"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Circle{T}, Triangle{T})"/>
    public static bool Overlaps<T>(Triangle<T> t, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Contains(t, c.Pos) || Dist2(c.Pos, Closest(t, c.Pos)) <= double.CreateChecked(c.Radius) * double.CreateChecked(c.Radius);
    /// <summary>True if a circle overlaps a triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="t">The triangle.</param>
    /// <returns><c>true</c> if <paramref name="c"/> overlaps <paramref name="t"/>; otherwise <c>false</c>.</returns>
    /// <seealso cref="Overlaps{T}(Triangle{T}, Circle{T})"/>
    public static bool Overlaps<T>(Circle<T> c, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Overlaps(t, c);

    /// <summary>True if two triangles overlap.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="a">The first triangle.</param>
    /// <param name="b">The second triangle.</param>
    /// <returns><c>true</c> if the triangles overlap; otherwise <c>false</c>.</returns>
    public static bool Overlaps<T>(Triangle<T> a, Triangle<T> b) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => Overlaps(a, b.Side(0)) || Overlaps(a, b.Side(1)) || Overlaps(a, b.Side(2)) || Overlaps(b, a.P0);

    // O--- Intersects: returns the set of intersection points ---O
    /// <summary>Removes points coincident within Epsilon, keeping the first of each cluster.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="points">The candidate points to deduplicate.</param>
    /// <returns>A new list with points coincident within <see cref="Epsilon"/> collapsed to the first of each cluster.</returns>
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

    /// <summary>Intersection point(s) of two line segments (or infinite lines when infinite is true).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l1">The first line.</param>
    /// <param name="l2">The second line.</param>
    /// <param name="infinite">When <c>true</c>, treat both as infinite lines rather than bounded segments.</param>
    /// <returns>The list of intersection points (empty if parallel/colinear, or out of range when not <paramref name="infinite"/>).</returns>
    /// <seealso cref="Overlaps{T}(Line{T}, Line{T})"/>
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

    /// <summary>Intersection point(s) of a circle and a line segment (0, 1 tangent, or 2 points).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns>The intersection points: 0 if disjoint, 1 if tangent, or 2 if the segment crosses the circle.</returns>
    /// <seealso cref="Intersects{T}(Line{T}, Circle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        if (!Overlaps(c, Closest(l, c.Pos))) return new List<Vector2d<T>>(); // segment too far
        double dx = Dx(l.End) - Dx(l.Start), dy = Dy(l.End) - Dy(l.Start);
        var mag2 = dx * dx + dy * dy;
        var uLine = (dx * (Dx(c.Pos) - Dx(l.Start)) + dy * (Dy(c.Pos) - Dy(l.Start))) / mag2;
        double clX = Dx(l.Start) + uLine * dx, clY = Dy(l.Start) + uLine * dy;
        var distToLine = (Dx(c.Pos) - clX) * (Dx(c.Pos) - clX) + (Dy(c.Pos) - clY) * (Dy(c.Pos) - clY);
        var r2 = double.CreateChecked(c.Radius) * double.CreateChecked(c.Radius);
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

    /// <summary>Intersection point(s) of two circles (empty if separate, coincident, or one inside the other).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c1">The first circle.</param>
    /// <param name="c2">The second circle.</param>
    /// <returns>The intersection points; empty if the circles are separate, coincident, or one is inside the other.</returns>
    public static List<Vector2d<T>> Intersects<T>(Circle<T> c1, Circle<T> c2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        if (c1.Pos == c2.Pos) return new List<Vector2d<T>>();
        double bx = Dx(c2.Pos) - Dx(c1.Pos), by = Dy(c2.Pos) - Dy(c1.Pos);
        var dist2 = bx * bx + by * by;
        double r1 = double.CreateChecked(c1.Radius), r2 = double.CreateChecked(c2.Radius);
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
    /// <summary>Intersection points of a rectangle's edges with a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns>The deduplicated intersection points of <paramref name="l"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Line{T}, Rect{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => FilterDuplicatePoints(Collect(l, r.Top(), r.Bottom(), r.Left(), r.Right()));

    /// <summary>Intersection points of a triangle's edges with a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="l">The line segment.</param>
    /// <returns>The deduplicated intersection points of <paramref name="l"/> with <paramref name="t"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Line{T}, Triangle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => FilterDuplicatePoints(Collect(l, t.Side(0), t.Side(1), t.Side(2)));

    /// <summary>Intersection points of two rectangles' edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r1">The first rectangle.</param>
    /// <param name="r2">The second rectangle.</param>
    /// <returns>The deduplicated intersection points of the two rectangles' edges.</returns>
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r1, Rect<T> r2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(r1, r2.Side(i)));
        return FilterDuplicatePoints(result);
    }

    /// <summary>Intersection points of a circle with a rectangle's edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="c"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Rect{T}, Circle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(c, r.Side(i)));
        return FilterDuplicatePoints(result);
    }

    /// <summary>Intersection points of a triangle with a rectangle's edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="t"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Rect{T}, Triangle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(t, r.Side(i)));
        return FilterDuplicatePoints(result);
    }

    /// <summary>Intersection points of a circle with a triangle's edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <param name="c">The circle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="c"/> with <paramref name="t"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Circle{T}, Triangle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 3; i++) result.AddRange(Intersects(c, t.Side(i)));
        return FilterDuplicatePoints(result);
    }

    /// <summary>Intersection points of two triangles' edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t1">The first triangle.</param>
    /// <param name="t2">The second triangle.</param>
    /// <returns>The deduplicated intersection points of the two triangles' edges.</returns>
    public static List<Vector2d<T>> Intersects<T>(Triangle<T> t1, Triangle<T> t2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 3; i++) result.AddRange(Intersects(t1, t2.Side(i)));
        return FilterDuplicatePoints(result);
    }

    // Reverse convenience overloads.
    /// <summary>Intersection points of a line segment with a rectangle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="l"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Rect{T}, Line{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(r, l);
    /// <summary>Intersection points of a line segment with a circle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="c">The circle.</param>
    /// <returns>The intersection points of <paramref name="l"/> with <paramref name="c"/>.</returns>
    /// <seealso cref="Intersects{T}(Circle{T}, Line{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(c, l);
    /// <summary>Intersection points of a line segment with a triangle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <param name="t">The triangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="l"/> with <paramref name="t"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Triangle{T}, Line{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Line<T> l, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, l);
    /// <summary>Intersection points of a rectangle with a circle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="c">The circle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="c"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Circle{T}, Rect{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(c, r);
    /// <summary>Intersection points of a rectangle with a triangle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <param name="t">The triangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="t"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Triangle{T}, Rect{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Rect<T> r, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, r);
    /// <summary>Intersection points of a circle with a triangle (argument-order convenience).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <param name="t">The triangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="c"/> with <paramref name="t"/>'s edges.</returns>
    /// <seealso cref="Intersects{T}(Triangle{T}, Circle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Circle<T> c, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => Intersects(t, c);

    /// <summary>Collects all intersection points of a line with the given sides.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment to test against each side.</param>
    /// <param name="sides">The sides to intersect with.</param>
    /// <returns>The concatenated (not yet deduplicated) intersection points.</returns>
    private static List<Vector2d<T>> Collect<T>(Line<T> l, params Line<T>[] sides) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        foreach (var side in sides) result.AddRange(Intersects(side, l));
        return result;
    }

    /// <summary>The point a parameter distance t along a ray/line direction from an origin.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="origin">The origin point.</param>
    /// <param name="dir">The direction vector.</param>
    /// <param name="t">The parameter distance along <paramref name="dir"/> from <paramref name="origin"/>.</param>
    /// <returns>The point <c>origin + dir * t</c>.</returns>
    private static Vector2d<T> Along<T>(Vector2d<T> origin, Vector2d<T> dir, double t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => new(T.CreateChecked(Dx(origin) + Dx(dir) * t), T.CreateChecked(Dy(origin) + Dy(dir) * t));

    // O--- Envelopes (bounding circle / bounding rect) ---O
    /// <summary>The bounding circle of a point (zero radius).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="p">The point.</param>
    /// <returns>A circle at <paramref name="p"/> with zero radius.</returns>
    /// <seealso cref="EnvelopeR{T}(Vector2d{T})"/>
    public static Circle<T> EnvelopeC<T>(Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => new(p, default);

    /// <summary>The bounding circle of a line segment (midpoint, half-length radius).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <returns>A circle centred at the segment midpoint with a radius of half the segment length.</returns>
    /// <seealso cref="EnvelopeR{T}(Line{T})"/>
    public static Circle<T> EnvelopeC<T>(Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var mid = new Vector2d<T>(T.CreateChecked((Dx(l.Start) + Dx(l.End)) / 2), T.CreateChecked((Dy(l.Start) + Dy(l.End)) / 2));
        return new Circle<T>(mid, T.CreateChecked(Math.Sqrt(Dist2(l.Start, l.End)) / 2));
    }

    /// <summary>The bounding circle of a rectangle (through its corner diagonal).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <returns>The circle whose diameter is the rectangle's corner diagonal.</returns>
    /// <seealso cref="EnvelopeR{T}(Rect{T})"/>
    public static Circle<T> EnvelopeC<T>(Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
        => EnvelopeC(new Line<T>(r.Pos, r.Pos + r.Size));

    /// <summary>The bounding circle of a circle (itself).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <returns>The circle <paramref name="c"/> unchanged.</returns>
    /// <seealso cref="EnvelopeR{T}(Circle{T})"/>
    public static Circle<T> EnvelopeC<T>(Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => c;

    /// <summary>The circumscribed circle of a triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <returns>The triangle's circumscribed circle (through all three vertices).</returns>
    /// <seealso cref="EnvelopeR{T}(Triangle{T})"/>
    public static Circle<T> EnvelopeC<T>(Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double ax = Dx(t.P0), ay = Dy(t.P0), bx = Dx(t.P1), by = Dy(t.P1), cx = Dx(t.P2), cy = Dy(t.P2);
        var d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        var ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
        var uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
        var r = 0.0;
        r = Math.Max(r, Math.Sqrt((ux - ax) * (ux - ax) + (uy - ay) * (uy - ay)));
        r = Math.Max(r, Math.Sqrt((ux - bx) * (ux - bx) + (uy - by) * (uy - by)));
        r = Math.Max(r, Math.Sqrt((ux - cx) * (ux - cx) + (uy - cy) * (uy - cy)));
        return new Circle<T>(new Vector2d<T>(T.CreateChecked(ux), T.CreateChecked(uy)), T.CreateChecked(r));
    }

    /// <summary>The bounding rectangle of a point (zero size).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="p">The point.</param>
    /// <returns>A rectangle at <paramref name="p"/> with zero size.</returns>
    /// <seealso cref="EnvelopeC{T}(Vector2d{T})"/>
    public static Rect<T> EnvelopeR<T>(Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => new(p, new Vector2d<T>(default, default));

    /// <summary>The axis-aligned bounding rectangle of a line segment.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line segment.</param>
    /// <returns>The axis-aligned bounding rectangle of <paramref name="l"/>.</returns>
    /// <seealso cref="EnvelopeC{T}(Line{T})"/>
    public static Rect<T> EnvelopeR<T>(Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double minX = Math.Min(Dx(l.Start), Dx(l.End)), minY = Math.Min(Dy(l.Start), Dy(l.End));
        return new Rect<T>(new Vector2d<T>(T.CreateChecked(minX), T.CreateChecked(minY)),
            new Vector2d<T>(T.CreateChecked(Math.Abs(Dx(l.Start) - Dx(l.End))), T.CreateChecked(Math.Abs(Dy(l.Start) - Dy(l.End)))));
    }

    /// <summary>The bounding rectangle of a rectangle (itself).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="r">The rectangle.</param>
    /// <returns>The rectangle <paramref name="r"/> unchanged.</returns>
    /// <seealso cref="EnvelopeC{T}(Rect{T})"/>
    public static Rect<T> EnvelopeR<T>(Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => r;

    /// <summary>The axis-aligned bounding rectangle of a circle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="c">The circle.</param>
    /// <returns>The axis-aligned bounding rectangle of <paramref name="c"/>.</returns>
    /// <seealso cref="EnvelopeC{T}(Circle{T})"/>
    public static Rect<T> EnvelopeR<T>(Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var r = double.CreateChecked(c.Radius);
        return new Rect<T>(new Vector2d<T>(T.CreateChecked(Dx(c.Pos) - r), T.CreateChecked(Dy(c.Pos) - r)),
            new Vector2d<T>(T.CreateChecked(r * 2), T.CreateChecked(r * 2)));
    }

    /// <summary>The axis-aligned bounding rectangle of a triangle.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <returns>The axis-aligned bounding rectangle of <paramref name="t"/>.</returns>
    /// <seealso cref="EnvelopeC{T}(Triangle{T})"/>
    /// <seealso cref="BoundingBox{T}(Triangle{T})"/>
    public static Rect<T> EnvelopeR<T>(Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var minX = Math.Min(Dx(t.P0), Math.Min(Dx(t.P1), Dx(t.P2)));
        var minY = Math.Min(Dy(t.P0), Math.Min(Dy(t.P1), Dy(t.P2)));
        var maxX = Math.Max(Dx(t.P0), Math.Max(Dx(t.P1), Dx(t.P2)));
        var maxY = Math.Max(Dy(t.P0), Math.Max(Dy(t.P1), Dy(t.P2)));
        return new Rect<T>(new Vector2d<T>(T.CreateChecked(minX), T.CreateChecked(minY)),
            new Vector2d<T>(T.CreateChecked(maxX - minX), T.CreateChecked(maxY - minY)));
    }

    /// <summary>The axis-aligned bounding box of a triangle (alias for EnvelopeR).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="t">The triangle.</param>
    /// <returns>The axis-aligned bounding rectangle of <paramref name="t"/> (same as <see cref="EnvelopeR{T}(Triangle{T})"/>).</returns>
    /// <seealso cref="EnvelopeR{T}(Triangle{T})"/>
    public static Rect<T> BoundingBox<T>(Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => EnvelopeR(t);

    // O--- Ray relations ---O
    /// <summary>Closest point on a ray to p (olc stub: returns p unchanged).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="p">The query point.</param>
    /// <returns><paramref name="p"/> unchanged (olc leaves this a stub).</returns>
    public static Vector2d<T> Closest<T>(Ray<T> q, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => p; // olc TODO

    /// <summary>Intersection point of two rays (origin if colinear, empty if parallel or behind either origin).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q1">The first ray.</param>
    /// <param name="q2">The second ray.</param>
    /// <returns>A single intersection point; the origin if colinear, or empty if parallel or behind either origin.</returns>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q1, Ray<T> q2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double ox = Dx(q2.Origin) - Dx(q1.Origin), oy = Dy(q2.Origin) - Dy(q1.Origin);
        var cp1 = Dx(q1.Direction) * Dy(q2.Direction) - Dy(q1.Direction) * Dx(q2.Direction);
        var cp2 = ox * Dy(q2.Direction) - oy * Dx(q2.Direction);
        if (cp1 == 0) return cp2 == 0 ? new List<Vector2d<T>> { q1.Origin } : new List<Vector2d<T>>();
        var cp3 = ox * Dy(q1.Direction) - oy * Dx(q1.Direction);
        double t1 = cp2 / cp1, t2 = cp3 / cp1;
        return t1 >= 0 && t2 >= 0 ? new List<Vector2d<T>> { Along(q1.Origin, q1.Direction, t1) } : new List<Vector2d<T>>();
    }

    /// <summary>True-hit (returns p) if a point lies on the ray's line, otherwise empty.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="p">The query point.</param>
    /// <returns><paramref name="p"/> in a single-element list if it lies on the ray's line; otherwise empty.</returns>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var l = new Line<T>(q.Origin, q.Origin + q.Direction);
        return l.Side(p) == 0 ? new List<Vector2d<T>> { p } : new List<Vector2d<T>>();
    }

    /// <summary>Intersection point of a ray with a line segment (within the segment, ahead of the origin).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="l">The line segment.</param>
    /// <returns>The intersection point if within the segment and ahead of the ray origin; otherwise empty.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Line{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double ldx = Dx(l.End) - Dx(l.Start), ldy = Dy(l.End) - Dy(l.Start);
        double ox = Dx(l.Start) - Dx(q.Origin), oy = Dy(l.Start) - Dy(q.Origin);
        var cp1 = Dx(q.Direction) * ldy - Dy(q.Direction) * ldx;
        var cp2 = ox * ldy - oy * ldx;
        if (cp1 == 0) return cp2 == 0 ? new List<Vector2d<T>> { q.Origin } : new List<Vector2d<T>>();
        var cp3 = ox * Dy(q.Direction) - oy * Dx(q.Direction);
        double t1 = cp2 / cp1, t2 = cp3 / cp1;
        return t1 >= 0 && t2 >= 0 && t2 <= 1 ? new List<Vector2d<T>> { Along(q.Origin, q.Direction, t1) } : new List<Vector2d<T>>();
    }

    /// <summary>Intersection point(s) of a ray with a circle (only hits ahead of the origin, near-first).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="c">The circle.</param>
    /// <returns>The intersection points ahead of the ray origin, nearest first; empty if there is no hit.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Circle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double dirx = Dx(q.Direction), diry = Dy(q.Direction), ox = Dx(q.Origin), oy = Dy(q.Origin), cx = Dx(c.Pos), cy = Dy(c.Pos);
        var a = dirx * dirx + diry * diry;
        var b = 2.0 * ((ox * dirx + oy * diry) - (cx * dirx + cy * diry));
        var rad = double.CreateChecked(c.Radius);
        var cc = cx * cx + cy * cy + ox * ox + oy * oy - 2.0 * cx * ox - 2.0 * cy * oy - rad * rad;
        var d = b * b - 4.0 * a * cc;
        if (d < 0) return new List<Vector2d<T>>();
        var sd = Math.Sqrt(d);
        double s1 = (-b + sd) / (2 * a), s2 = (-b - sd) / (2 * a);
        if (s1 < 0 && s2 < 0) return new List<Vector2d<T>>();
        if (s1 < 0) return new List<Vector2d<T>> { Along(q.Origin, q.Direction, s2) };
        if (s2 < 0) return new List<Vector2d<T>> { Along(q.Origin, q.Direction, s1) };
        return new List<Vector2d<T>> { Along(q.Origin, q.Direction, Math.Min(s1, s2)), Along(q.Origin, q.Direction, Math.Max(s1, s2)) };
    }

    /// <summary>Intersection points of a ray with a rectangle's edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="q"/> with <paramref name="r"/>'s edges.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Rect{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 4; i++) result.AddRange(Intersects(q, r.Side(i)));
        return FilterDuplicatePoints(result);
    }

    /// <summary>Intersection points of a ray with a triangle's edges.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="t">The triangle.</param>
    /// <returns>The deduplicated intersection points of <paramref name="q"/> with <paramref name="t"/>'s edges.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Triangle{T})"/>
    public static List<Vector2d<T>> Intersects<T>(Ray<T> q, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var result = new List<Vector2d<T>>();
        for (var i = 0; i < 3; i++) result.AddRange(Intersects(q, t.Side(i)));
        return FilterDuplicatePoints(result);
    }

    // Collision = nearest hit point + surface normal (null if no hit). Normals only make sense for
    // floating-point T (an integer T truncates the unit normal).
    /// <summary>Ray-versus-line collision: nearest hit point and surface normal, or null. Normals only make sense for floating-point T.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="l">The line segment.</param>
    /// <returns>A tuple of the hit point and surface normal, or <c>null</c> if the ray misses.</returns>
    /// <remarks><para>Normals only make sense for floating-point <typeparamref name="T"/>; an integer <typeparamref name="T"/> truncates the unit normal.</para></remarks>
    /// <seealso cref="Reflect{T}(Ray{T}, Line{T})"/>
    /// <seealso cref="Intersects{T}(Ray{T}, Line{T})"/>
    public static (Vector2d<T> Point, Vector2d<T> Normal)? Collision<T>(Ray<T> q, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var hits = Intersects(q, l);
        if (hits.Count == 0) return null;
        return (hits[0], SideNormal(l, q.Origin));
    }

    /// <summary>Ray-versus-rectangle collision: nearest-edge hit point and surface normal, or null.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>A tuple of the nearest-edge hit point and surface normal, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Reflect{T}(Ray{T}, Rect{T})"/>
    public static (Vector2d<T> Point, Vector2d<T> Normal)? Collision<T>(Ray<T> q, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        Vector2d<T> best = default, normal = default;
        var bestDist = double.MaxValue;
        var hit = false;
        for (var i = 0; i < 4; i++)
        {
            var side = r.Side(i);
            var hits = Intersects(q, side);
            if (hits.Count == 0) continue;
            var d = Dist2(hits[0], q.Origin);
            if (d < bestDist)
            {
                bestDist = d; best = hits[0]; normal = SideNormal(side, q.Origin); hit = true;
            }
        }
        return hit ? (best, normal) : null;
    }

    /// <summary>Reflects a ray off a line at the collision point, or null if it misses.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The incident ray.</param>
    /// <param name="l">The line segment to reflect off.</param>
    /// <returns>The reflected ray from the collision point, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Line{T})"/>
    public static Ray<T>? Reflect<T>(Ray<T> q, Line<T> l) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var col = Collision(q, l);
        return col == null ? null : new Ray<T>(col.Value.Point, q.Direction.Reflect(col.Value.Normal));
    }

    /// <summary>Reflects a ray off a rectangle at the collision point, or null if it misses.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The incident ray.</param>
    /// <param name="r">The rectangle to reflect off.</param>
    /// <returns>The reflected ray from the collision point, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Rect{T})"/>
    public static Ray<T>? Reflect<T>(Ray<T> q, Rect<T> r) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var col = Collision(q, r);
        return col == null ? null : new Ray<T>(col.Value.Point, q.Direction.Reflect(col.Value.Normal));
    }

    /// <summary>Ray-versus-circle collision: nearest hit point and outward surface normal, or null.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="c">The circle.</param>
    /// <returns>A tuple of the nearest hit point and outward surface normal, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Reflect{T}(Ray{T}, Circle{T})"/>
    public static (Vector2d<T> Point, Vector2d<T> Normal)? Collision<T>(Ray<T> q, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var hits = Intersects(q, c);
        if (hits.Count == 0) return null;
        double nx = Dx(hits[0]) - Dx(c.Pos), ny = Dy(hits[0]) - Dy(c.Pos);
        var len = Math.Sqrt(nx * nx + ny * ny);
        var normal = len == 0 ? default : new Vector2d<T>(T.CreateChecked(nx / len), T.CreateChecked(ny / len));
        return (hits[0], normal);
    }

    /// <summary>Ray-versus-triangle collision: nearest-edge hit point and surface normal, or null.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The ray.</param>
    /// <param name="t">The triangle.</param>
    /// <returns>A tuple of the nearest-edge hit point and surface normal, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Reflect{T}(Ray{T}, Triangle{T})"/>
    public static (Vector2d<T> Point, Vector2d<T> Normal)? Collision<T>(Ray<T> q, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        Vector2d<T> best = default, normal = default;
        var bestDist = double.MaxValue;
        var hit = false;
        for (var i = 0; i < 3; i++)
        {
            var side = t.Side(i);
            var hits = Intersects(q, side);
            if (hits.Count == 0) continue;
            var d = Dist2(hits[0], q.Origin);
            if (d >= bestDist) continue;
            bestDist = d; best = hits[0]; hit = true;
            double vx = Dx(side.End) - Dx(side.Start), vy = Dy(side.End) - Dy(side.Start);
            var len = Math.Sqrt(vx * vx + vy * vy); // unit perpendicular of the side
            normal = len == 0 ? default : new Vector2d<T>(T.CreateChecked(-vy / len), T.CreateChecked(vx / len));
        }
        return hit ? (best, normal) : null;
    }

    /// <summary>Reflects a ray off a circle at the collision point, or null if it misses.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The incident ray.</param>
    /// <param name="c">The circle to reflect off.</param>
    /// <returns>The reflected ray from the collision point, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Circle{T})"/>
    public static Ray<T>? Reflect<T>(Ray<T> q, Circle<T> c) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var col = Collision(q, c);
        return col == null ? null : new Ray<T>(col.Value.Point, q.Direction.Reflect(col.Value.Normal));
    }

    /// <summary>Reflects a ray off a triangle at the collision point, or null if it misses.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The incident ray.</param>
    /// <param name="t">The triangle to reflect off.</param>
    /// <returns>The reflected ray from the collision point, or <c>null</c> if the ray misses.</returns>
    /// <seealso cref="Collision{T}(Ray{T}, Triangle{T})"/>
    public static Ray<T>? Reflect<T>(Ray<T> q, Triangle<T> t) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var col = Collision(q, t);
        return col == null ? null : new Ray<T>(col.Value.Point, q.Direction.Reflect(col.Value.Normal));
    }

    // olc: a ray can't be reflected off another ray, nor (yet) off a point.
    /// <summary>Always null: a ray can't be reflected off another ray (olc).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q1">The incident ray.</param>
    /// <param name="q2">The ray to reflect off (unused).</param>
    /// <returns>Always <c>null</c> (olc does not define ray-off-ray reflection).</returns>
    public static Ray<T>? Reflect<T>(Ray<T> q1, Ray<T> q2) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => null;
    /// <summary>Always null: a ray can't (yet) be reflected off a point (olc).</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="q">The incident ray.</param>
    /// <param name="p">The point to reflect off (unused).</param>
    /// <returns>Always <c>null</c> (olc does not yet define ray-off-point reflection).</returns>
    public static Ray<T>? Reflect<T>(Ray<T> q, Vector2d<T> p) where T : struct, INumber<T>, IEquatable<T>, IComparable<T> => null;

    /// <summary>Unit normal of a line's perpendicular, oriented by which side the point is on.</summary>
    /// <typeparam name="T">The numeric component type.</typeparam>
    /// <param name="l">The line whose perpendicular is taken.</param>
    /// <param name="point">The reference point determining the normal's orientation.</param>
    /// <returns>The unit normal oriented toward <paramref name="point"/>'s side, or the zero vector for a degenerate line.</returns>
    private static Vector2d<T> SideNormal<T>(Line<T> l, Vector2d<T> point) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        double vx = Dx(l.End) - Dx(l.Start), vy = Dy(l.End) - Dy(l.Start);
        var len = Math.Sqrt(vx * vx + vy * vy);
        var sign = l.Side(point);
        if (len == 0) return new Vector2d<T>(default, default);
        return new Vector2d<T>(T.CreateChecked(-vy / len * sign), T.CreateChecked(vx / len * sign));
    }
}
