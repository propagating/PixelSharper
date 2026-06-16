using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixelSharper.Core.Types;

/// <summary>Generic 2D vector over any numeric T (olc v_2d), with hardware-accelerated generic math; operators are same-type only by design.</summary>
/// <typeparam name="T">The numeric component type (constrained to <see cref="INumber{T}"/>).</typeparam>
/// <remarks>
/// <para>
/// Arithmetic operators are <b>same-type only</b>: there are intentionally no typed (<c>float</c>/<c>double</c>/<c>int</c>)
/// scalar or cross-type vector overloads, because a typed overload is ambiguous with the generic <typeparamref name="T"/>
/// overload when <typeparamref name="T"/> equals that type. For mixed-type math, convert first with <see cref="As{TOut}"/>
/// (e.g. <c>vfloat / visize.As{float}()</c>).
/// </para>
/// </remarks>
public struct Vector2d<T> : IEquatable<Vector2d<T>> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    /// <summary>The X component.</summary>
    /// <value>The horizontal component.</value>
    public T X { get; set; }
    /// <summary>The Y component.</summary>
    /// <value>The vertical component.</value>
    public T Y { get; set; }

    /// <summary>Constructs a vector from its components.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    public Vector2d(T x, T y)
    {
        // No runtime validation needed: the `where T : INumber<T>` constraint already guarantees, at
        // compile time, that T is a valid number — so the old IsValidNumeric check could never fail and
        // only kept the ctor from inlining. Just store the components.
        X = x;
        Y = y;
    }

    #region validation
    /// <summary>True if the value is strictly greater than zero.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is greater than zero; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPositive(T value) => value > T.Zero;

    /// <summary>True if the value equals zero.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> if <paramref name="value"/> equals zero; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZero(T value)
    {
        var result = value == T.Zero;
        return result;
    }

    #endregion
    
    #region operators

    // Arithmetic operations — same-type only. Typed scalar (float/double/int) and cross-type
    // vector overloads were removed: a typed overload is AMBIGUOUS with the generic T overload
    // whenever T equals that type (e.g. Vector2d<float> * float), and the old cross-type vector
    // operators silently dropped the Y component (a bug). For mixed types, convert with As<TOut>()
    // first, e.g. `vfloat / visize.As<float>()`.
    /// <summary>Component-wise addition (same-type only by design).</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns>The component-wise sum <c>(a.X + b.X, a.Y + b.Y)</c>.</returns>
    /// <remarks>
    /// <para>Same-type only; for mixed types convert first with <see cref="As{TOut}"/>.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator +(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X + b.X, a.Y + b.Y);

    /// <summary>Component-wise subtraction (same-type only by design).</summary>
    /// <param name="a">The vector subtracted from (minuend).</param>
    /// <param name="b">The vector to subtract (subtrahend).</param>
    /// <returns>The component-wise difference <c>(a.X - b.X, a.Y - b.Y)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator -(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X - b.X, a.Y - b.Y);

    /// <summary>Scales a vector by a scalar of the same type.</summary>
    /// <param name="a">The vector to scale.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled vector <c>(a.X * scalar, a.Y * scalar)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X * scalar, a.Y * scalar);

    /// <summary>Scales a vector by a scalar of the same type (scalar on the left).</summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="a">The vector to scale.</param>
    /// <returns>The scaled vector <c>(a.X * scalar, a.Y * scalar)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(T scalar, Vector2d<T> a) => new Vector2d<T>(a.X * scalar, a.Y * scalar);

    /// <summary>Component-wise (Hadamard) product of two vectors.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns>The component-wise product <c>(a.X * b.X, a.Y * b.Y)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X * b.X, a.Y * b.Y);

    /// <summary>Divides a vector by a scalar of the same type.</summary>
    /// <param name="a">The vector to divide.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>The scaled vector <c>(a.X / scalar, a.Y / scalar)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X / scalar, a.Y / scalar);

    /// <summary>Component-wise division of two vectors.</summary>
    /// <param name="a">The numerator vector.</param>
    /// <param name="b">The denominator vector.</param>
    /// <returns>The component-wise quotient <c>(a.X / b.X, a.Y / b.Y)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X / b.X, a.Y / b.Y);

    /// <summary>True if both components of a are greater than b's.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if both <c>a.X &gt; b.X</c> and <c>a.Y &gt; b.Y</c>; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Vector2d<T> a, Vector2d<T> b) => a.X > b.X && a.Y > b.Y;

    /// <summary>True if both components of a are greater than or equal to b's.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if both <c>a.X &gt;= b.X</c> and <c>a.Y &gt;= b.Y</c>; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Vector2d<T> a, Vector2d<T> b) => a.X >= b.X && a.Y >= b.Y;

    /// <summary>True if both components of a are less than b's.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if both <c>a.X &lt; b.X</c> and <c>a.Y &lt; b.Y</c>; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Vector2d<T> a, Vector2d<T> b) => a.X < b.X && a.Y < b.Y;

    /// <summary>True if both components of a are less than or equal to b's.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if both <c>a.X &lt;= b.X</c> and <c>a.Y &lt;= b.Y</c>; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Vector2d<T> a, Vector2d<T> b) => a.X <= b.X && a.Y <= b.Y;

    /// <summary>Component-wise equality.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if both components are equal; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2d<T> a, Vector2d<T> b) => a.X == b.X && a.Y == b.Y;

    /// <summary>Component-wise inequality.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if any component differs; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2d<T> a, Vector2d<T> b) => !(a == b);

    /// <summary>Logical AND: true if all four components are non-zero.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if every component of both vectors is non-zero; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator &(Vector2d<T> a, Vector2d<T> b) => a.X != T.Zero && a.Y != T.Zero && b.X != T.Zero && b.Y != T.Zero;

    /// <summary>Logical OR: true if any component is non-zero.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if any component of either vector is non-zero; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator |(Vector2d<T> a, Vector2d<T> b) => a.X != T.Zero || a.Y != T.Zero || b.X != T.Zero || b.Y != T.Zero;

    /// <summary>Logical XOR of the two vectors' both-components-non-zero truth values.</summary>
    /// <param name="a">The left-hand vector.</param>
    /// <param name="b">The right-hand vector.</param>
    /// <returns><c>true</c> if exactly one of the two vectors has both components non-zero; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ^(Vector2d<T> a, Vector2d<T> b) =>
        (a.X != T.Zero && a.Y != T.Zero) ^ (b.X != T.Zero && b.Y != T.Zero);
    #endregion
    
    #region vector manipulation
    /// <summary>Dot product with another vector, computed and returned in the chosen result type.</summary>
    /// <typeparam name="T2">The component type of the other vector.</typeparam>
    /// <typeparam name="TResult">The numeric type the dot product is computed and returned in.</typeparam>
    /// <param name="other">The vector to dot with.</param>
    /// <returns>The dot product <c>X*other.X + Y*other.Y</c> in <typeparamref name="TResult"/>.</returns>
    /// <seealso cref="CrossProduct{T2, TResult}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult DotProduct<T2, TResult>(Vector2d<T2> other)
        where T2 : struct, INumber<T2>
        where TResult : struct, INumber<TResult>
    {
        return (TResult.CreateChecked(this.X) * TResult.CreateChecked(other.X)) +
               (TResult.CreateChecked(this.Y) * TResult.CreateChecked(other.Y));
    }

    /// <summary>2D scalar cross product (z of the 3D cross), in the chosen result type.</summary>
    /// <typeparam name="T2">The component type of the other vector.</typeparam>
    /// <typeparam name="TResult">The numeric type the cross product is computed and returned in.</typeparam>
    /// <param name="other">The vector to cross with.</param>
    /// <returns>The 2D cross product <c>X*other.Y - Y*other.X</c> in <typeparamref name="TResult"/>.</returns>
    /// <seealso cref="DotProduct{T2, TResult}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult CrossProduct<T2, TResult>(Vector2d<T2> other)
        where T2 : struct, INumber<T2>
        where TResult : struct, INumber<TResult>
    {
        return (TResult.CreateChecked(this.X) * TResult.CreateChecked(other.Y)) -
               (TResult.CreateChecked(this.Y) * TResult.CreateChecked(other.X));
    }

    /// <summary>The rectangular area X*Y.</summary>
    /// <returns>The product of the components <c>X * Y</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Area()
    {
        return X * Y;
    }

    /// <summary>Returns the unit-length vector in the same direction; throws if length is zero.</summary>
    /// <returns>The unit-length vector in the same direction as this one.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this vector has zero length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Normalize()
    {
        var length = Magnitude();

        return IsZero(length) ? throw new InvalidOperationException("Cannot normalize a vector with length zero.") : new Vector2d<T>(X / length, Y / length);
    }

    /// <summary>Euclidean length, converted back to T.</summary>
    /// <returns>The Euclidean length (magnitude) of the vector in <typeparamref name="T"/>.</returns>
    /// <seealso cref="MagnitudeSquared"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Magnitude()
    {
        return T.CreateChecked(MagnitudeAsDouble());
    }

    /// <summary>Squared length (no square root), staying in T.</summary>
    /// <returns>The squared length <c>X*X + Y*Y</c>.</returns>
    /// <seealso cref="Magnitude"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T MagnitudeSquared()
    {
        return (X * X) + (Y * Y);
    }

    /// <summary>The 90-degree perpendicular (-Y, X).</summary>
    /// <returns>The perpendicular vector <c>(-Y, X)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Perpendicular()
    {
        return new Vector2d<T>(-Y, X);
    }

    /// <summary>Floors each component, returning a vector of the chosen floating-point type.</summary>
    /// <typeparam name="TFloating">The floating-point type of the resulting vector.</typeparam>
    /// <returns>A vector whose components are this vector's components rounded down.</returns>
    /// <seealso cref="Ceiling{TFloating}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TFloating> Floor<TFloating>() where TFloating : struct, IFloatingPoint<TFloating>
    {
        TFloating x = TFloating.CreateChecked(X);
        TFloating y = TFloating.CreateChecked(Y);
        return new Vector2d<TFloating>(TFloating.Floor(x), TFloating.Floor(y));
    }

    /// <summary>Ceils each component, returning a vector of the chosen floating-point type.</summary>
    /// <typeparam name="TFloating">The floating-point type of the resulting vector.</typeparam>
    /// <returns>A vector whose components are this vector's components rounded up.</returns>
    /// <seealso cref="Floor{TFloating}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TFloating> Ceiling<TFloating>() where TFloating : struct, IFloatingPoint<TFloating>
    {
        TFloating x = TFloating.CreateChecked(X);
        TFloating y = TFloating.CreateChecked(Y);
        return new Vector2d<TFloating>(TFloating.Ceiling(x), TFloating.Ceiling(y));
    }

    /// <summary>Component-wise minimum of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>A vector of the per-component minimums.</returns>
    /// <seealso cref="Max"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Min(Vector2d<T> a, Vector2d<T> b)
    {
        // Entirely in T (INumber<T>.Min) — no double round-trip, no boxing.
        return new Vector2d<T>(T.Min(a.X, b.X), T.Min(a.Y, b.Y));
    }

    /// <summary>Component-wise maximum of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>A vector of the per-component maximums.</returns>
    /// <seealso cref="Min"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Max(Vector2d<T> a, Vector2d<T> b)
    {
        // Entirely in T (INumber<T>.Max) — no double round-trip, no boxing.
        return new Vector2d<T>(T.Max(a.X, b.X), T.Max(a.Y, b.Y));
    }

    /// <summary>Converts to polar form, returning radius and angle (radians).</summary>
    /// <returns>A tuple of the radius <c>r</c> and angle <c>theta</c> in radians.</returns>
    /// <seealso cref="FromPolar"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (T r, T theta) ToPolar()
    {
        double r = MagnitudeAsDouble();
        double theta = Math.Atan2(double.CreateChecked(Y), double.CreateChecked(X));
        return (T.CreateChecked(r), T.CreateChecked(theta));
    }

    /// <summary>Builds a Cartesian vector from a radius and angle (radians).</summary>
    /// <param name="r">The radius (magnitude).</param>
    /// <param name="theta">The angle in radians.</param>
    /// <returns>The Cartesian vector for the given polar coordinates.</returns>
    /// <seealso cref="ToPolar"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> FromPolar(T r, T theta)
    {
        double rDouble = double.CreateChecked(r);
        double thetaDouble = double.CreateChecked(theta);
        T x = T.CreateChecked(rDouble * Math.Cos(thetaDouble));
        T y = T.CreateChecked(rDouble * Math.Sin(thetaDouble));
        return new Vector2d<T>(x, y);
    }

    /// <summary>Clamps each component to the corresponding [min, max] range.</summary>
    /// <param name="min">The per-component lower bounds.</param>
    /// <param name="max">The per-component upper bounds.</param>
    /// <returns>A vector with each component clamped to <c>[min, max]</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Clamp(Vector2d<T> min, Vector2d<T> max)
    {
        // Clamp each component to [min, max], entirely in T (INumber<T>.Clamp) — no double, no boxing.
        // (Also keeps the fix for the original bug where only X's lower and Y's upper bound applied.)
        return new Vector2d<T>(T.Clamp(X, min.X, max.X), T.Clamp(Y, min.Y, max.Y));
    }

    /// <summary>Linearly interpolates between a and b by t; integer T requires forceInteger (it truncates).</summary>
    /// <param name="a">The start vector (returned at <c>t = 0</c>).</param>
    /// <param name="b">The end vector (returned at <c>t = 1</c>).</param>
    /// <param name="t">The interpolation factor; <c>t</c> is converted to <typeparamref name="T"/> (checked), which truncates for integer types.</param>
    /// <param name="forceInteger">When <c>true</c>, allows interpolation with an integer <typeparamref name="T"/> (accepting truncation); otherwise an integer <typeparamref name="T"/> throws.</param>
    /// <returns>The interpolated vector <c>a + (b - a) * t</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> is not a floating-point type and <paramref name="forceInteger"/> is <c>false</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Lerp(Vector2d<T> a, Vector2d<T> b, float t, bool forceInteger = false)
    {
        // t is converted to T (checked) — for integer T this truncates, matching the prior behaviour.
        if(forceInteger) return a + (b - a) * T.CreateChecked(t);
        bool isNotFloatingPoint = !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<float>)) && !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<double>));
        if (isNotFloatingPoint) throw new InvalidOperationException("Lerp with integer types may lose precision. Set forceInteger = true to allow.");
        return a + (b - a) * T.CreateChecked(t);
    }

    /// <summary>Reflects this vector about the given surface normal (normal is normalised internally).</summary>
    /// <param name="normal">The surface normal to reflect about (normalised internally).</param>
    /// <returns>This vector reflected about <paramref name="normal"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="normal"/> has zero length (cannot be normalised).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Reflect(Vector2d<T> normal)
    {
        normal = normal.Normalize();
        return this - normal * (T.CreateChecked(2) * this.DotProduct<T, T>(normal));
    }

    /// <summary>Euclidean length computed in double (no boxing).</summary>
    /// <returns>The Euclidean length of the vector as a <c>double</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MagnitudeAsDouble()
    {
        // double.CreateChecked converts T->double without boxing (was Convert.ToDouble, which boxed).
        double x = double.CreateChecked(X), y = double.CreateChecked(Y);
        return Math.Sqrt(x * x + y * y);
    }
    
    /// <summary>Angle (radians) between two vectors; throws on a zero-length input.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The angle between the two vectors in radians.</returns>
    /// <exception cref="InvalidOperationException">Thrown when either <paramref name="a"/> or <paramref name="b"/> has zero length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AngleBetween(Vector2d<T> a, Vector2d<T> b)
    {
        double dot = a.DotProduct<T, double>(b);
        double magA = a.MagnitudeAsDouble();
        double magB = b.MagnitudeAsDouble();
        if (magA == 0 || magB == 0) throw new InvalidOperationException("Cannot compute angle with zero-length vector.");
        return Math.Acos(Math.Clamp(dot / (magA * magB), -1.0, 1.0));
    }
    
    /// <summary>Euclidean distance between two points (in double).</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The Euclidean distance between the points as a <c>double</c>.</returns>
    /// <seealso cref="DistanceSquared"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Vector2d<T> a, Vector2d<T> b)
    {
        // Sqrt needs double; double.CreateChecked converts T->double without boxing.
        return Math.Sqrt(double.CreateChecked(DistanceSquared(a, b)));
    }

    /// <summary>Squared distance between two points, staying in T.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The squared distance between the points in <typeparamref name="T"/>.</returns>
    /// <seealso cref="Distance"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DistanceSquared(Vector2d<T> a, Vector2d<T> b)
    {
        T dx = a.X - b.X;
        T dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>The negated vector (-X, -Y).</summary>
    /// <returns>The vector with both components negated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Negate()
    {
        return new Vector2d<T>(-X, -Y);
    }

    /// <summary>Returns the vector with X and Y swapped when swap is true, otherwise a copy.</summary>
    /// <param name="swap">When <c>true</c>, swap the X and Y components; otherwise return a copy.</param>
    /// <returns>The swapped vector <c>(Y, X)</c> when <paramref name="swap"/> is <c>true</c>; otherwise a copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Swizzle(bool swap)
    {
        return swap ? new Vector2d<T>(Y, X) : new Vector2d<T>(X, Y);
    }

    /// <summary>Converts to a vector of another numeric type (component-wise, checked) — the supported, unambiguous way to do mixed-type vector math.</summary>
    /// <typeparam name="TOut">The numeric component type of the resulting vector.</typeparam>
    /// <returns>A vector whose components are this vector's components converted (checked) to <typeparamref name="TOut"/>.</returns>
    /// <remarks>
    /// <para>Because the arithmetic operators are same-type only, convert with this method before doing mixed-type math, e.g. <c>vfloat / visize.As{float}()</c>.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TOut> As<TOut>() where TOut : struct, INumber<TOut>, IEquatable<TOut>, IComparable<TOut>
        => new Vector2d<TOut>(TOut.CreateChecked(X), TOut.CreateChecked(Y));

    /// <summary>The components as a 2-element array (olc v_2d::a()).</summary>
    /// <returns>A new 2-element array <c>{ X, Y }</c>.</returns>
    public T[] ToArray() => new[] { X, Y };
    #endregion

    #region interface methods
    /// <summary>Formats the vector as "(X, Y)".</summary>
    /// <returns>The string <c>"(X, Y)"</c>.</returns>
    public override string ToString() => $"({X}, {Y})";

    /// <summary>Value equality against another boxed vector of the same type.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Vector2d{T}"/> with equal components; otherwise <c>false</c>.</returns>
    /// <seealso cref="Equals(Vector2d{T})"/>
    public override bool Equals(object obj)
    {
        if (obj is Vector2d<T> other)
        {
            return this == other;
        }
        return false;
    }

    /// <summary>Hash code derived from the components.</summary>
    /// <returns>A hash code combining the X and Y components.</returns>
    public override int GetHashCode() => (X, Y).GetHashCode();

    /// <summary>Strongly-typed value equality against another vector.</summary>
    /// <param name="other">The vector to compare against.</param>
    /// <returns><c>true</c> if both components are equal; otherwise <c>false</c>.</returns>
    /// <seealso cref="Equals(object)"/>
    public bool Equals(Vector2d<T> other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }
    #endregion
}