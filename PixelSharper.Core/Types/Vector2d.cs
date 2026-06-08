using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixelSharper.Core.Types;

public struct Vector2d<T> : IEquatable<Vector2d<T>> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public T X { get; set; }
    public T Y { get; set; }

    public Vector2d(T x, T y)
    {
        // No runtime validation needed: the `where T : INumber<T>` constraint already guarantees, at
        // compile time, that T is a valid number — so the old IsValidNumeric check could never fail and
        // only kept the ctor from inlining. Just store the components.
        X = x;
        Y = y;
    }

    #region validation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPositive(T value) => value > T.Zero;
    
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator +(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator -(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X * scalar, a.Y * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(T scalar, Vector2d<T> a) => new Vector2d<T>(a.X * scalar, a.Y * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X * b.X, a.Y * b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X / scalar, a.Y / scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X / b.X, a.Y / b.Y);

    // Comparison operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Vector2d<T> a, Vector2d<T> b) => a.X > b.X && a.Y > b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Vector2d<T> a, Vector2d<T> b) => a.X >= b.X && a.Y >= b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Vector2d<T> a, Vector2d<T> b) => a.X < b.X && a.Y < b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Vector2d<T> a, Vector2d<T> b) => a.X <= b.X && a.Y <= b.Y;

    // Equality operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2d<T> a, Vector2d<T> b) => a.X == b.X && a.Y == b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2d<T> a, Vector2d<T> b) => !(a == b);

    // Logical operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator &(Vector2d<T> a, Vector2d<T> b) => a.X != T.Zero && a.Y != T.Zero && b.X != T.Zero && b.Y != T.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator |(Vector2d<T> a, Vector2d<T> b) => a.X != T.Zero || a.Y != T.Zero || b.X != T.Zero || b.Y != T.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ^(Vector2d<T> a, Vector2d<T> b) =>
        (a.X != T.Zero && a.Y != T.Zero) ^ (b.X != T.Zero && b.Y != T.Zero);
    #endregion
    
    #region vector manipulation
    // Other methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult DotProduct<T2, TResult>(Vector2d<T2> other)
        where T2 : struct, INumber<T2>
        where TResult : struct, INumber<TResult>
    {
        return (TResult.CreateChecked(this.X) * TResult.CreateChecked(other.X)) +
               (TResult.CreateChecked(this.Y) * TResult.CreateChecked(other.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult CrossProduct<T2, TResult>(Vector2d<T2> other)
        where T2 : struct, INumber<T2>
        where TResult : struct, INumber<TResult>
    {
        return (TResult.CreateChecked(this.X) * TResult.CreateChecked(other.Y)) -
               (TResult.CreateChecked(this.Y) * TResult.CreateChecked(other.X));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Area()
    {
        return X * Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Normalize()
    {
        var length = Magnitude();

        return IsZero(length) ? throw new InvalidOperationException("Cannot normalize a vector with length zero.") : new Vector2d<T>(X / length, Y / length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Magnitude()
    {
        return T.CreateChecked(MagnitudeAsDouble());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T MagnitudeSquared()
    {
        return (X * X) + (Y * Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Perpendicular()
    {
        return new Vector2d<T>(-Y, X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TFloating> Floor<TFloating>() where TFloating : struct, IFloatingPoint<TFloating>
    {
        TFloating x = TFloating.CreateChecked(X);
        TFloating y = TFloating.CreateChecked(Y);
        return new Vector2d<TFloating>(TFloating.Floor(x), TFloating.Floor(y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TFloating> Ceiling<TFloating>() where TFloating : struct, IFloatingPoint<TFloating>
    {
        TFloating x = TFloating.CreateChecked(X);
        TFloating y = TFloating.CreateChecked(Y);
        return new Vector2d<TFloating>(TFloating.Ceiling(x), TFloating.Ceiling(y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Min(Vector2d<T> a, Vector2d<T> b)
    {
        // Entirely in T (INumber<T>.Min) — no double round-trip, no boxing.
        return new Vector2d<T>(T.Min(a.X, b.X), T.Min(a.Y, b.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Max(Vector2d<T> a, Vector2d<T> b)
    {
        // Entirely in T (INumber<T>.Max) — no double round-trip, no boxing.
        return new Vector2d<T>(T.Max(a.X, b.X), T.Max(a.Y, b.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (T r, T theta) ToPolar()
    {
        double r = MagnitudeAsDouble();
        double theta = Math.Atan2(double.CreateChecked(Y), double.CreateChecked(X));
        return (T.CreateChecked(r), T.CreateChecked(theta));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> FromPolar(T r, T theta)
    {
        double rDouble = double.CreateChecked(r);
        double thetaDouble = double.CreateChecked(theta);
        T x = T.CreateChecked(rDouble * Math.Cos(thetaDouble));
        T y = T.CreateChecked(rDouble * Math.Sin(thetaDouble));
        return new Vector2d<T>(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Clamp(Vector2d<T> min, Vector2d<T> max)
    {
        // Clamp each component to [min, max], entirely in T (INumber<T>.Clamp) — no double, no boxing.
        // (Also keeps the fix for the original bug where only X's lower and Y's upper bound applied.)
        return new Vector2d<T>(T.Clamp(X, min.X, max.X), T.Clamp(Y, min.Y, max.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Lerp(Vector2d<T> a, Vector2d<T> b, float t, bool forceInteger = false)
    {
        // t is converted to T (checked) — for integer T this truncates, matching the prior behaviour.
        if(forceInteger) return a + (b - a) * T.CreateChecked(t);
        bool isNotFloatingPoint = !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<float>)) && !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<double>));
        if (isNotFloatingPoint) throw new InvalidOperationException("Lerp with integer types may lose precision. Set forceInteger = true to allow.");
        return a + (b - a) * T.CreateChecked(t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Reflect(Vector2d<T> normal)
    {
        normal = normal.Normalize();
        return this - normal * (T.CreateChecked(2) * this.DotProduct<T, T>(normal));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MagnitudeAsDouble()
    {
        // double.CreateChecked converts T->double without boxing (was Convert.ToDouble, which boxed).
        double x = double.CreateChecked(X), y = double.CreateChecked(Y);
        return Math.Sqrt(x * x + y * y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AngleBetween(Vector2d<T> a, Vector2d<T> b)
    {
        double dot = a.DotProduct<T, double>(b);
        double magA = a.MagnitudeAsDouble();
        double magB = b.MagnitudeAsDouble();
        if (magA == 0 || magB == 0) throw new InvalidOperationException("Cannot compute angle with zero-length vector.");
        return Math.Acos(Math.Clamp(dot / (magA * magB), -1.0, 1.0));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Vector2d<T> a, Vector2d<T> b)
    {
        // Sqrt needs double; double.CreateChecked converts T->double without boxing.
        return Math.Sqrt(double.CreateChecked(DistanceSquared(a, b)));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DistanceSquared(Vector2d<T> a, Vector2d<T> b)
    {
        T dx = a.X - b.X;
        T dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Negate()
    {
        return new Vector2d<T>(-X, -Y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Swizzle(bool swap)
    {
        return swap ? new Vector2d<T>(Y, X) : new Vector2d<T>(X, Y);
    }

    // Convert to a vector of another numeric type (component-wise, checked). The supported,
    // unambiguous way to do mixed-type vector math, e.g. vfloat / visize.As<float>().
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<TOut> As<TOut>() where TOut : struct, INumber<TOut>, IEquatable<TOut>, IComparable<TOut>
        => new Vector2d<TOut>(TOut.CreateChecked(X), TOut.CreateChecked(Y));

    // The components as a 2-element array (olc's v_2d::a()).
    public T[] ToArray() => new[] { X, Y };
    #endregion
    
    #region interface methods
    public override string ToString() => $"({X}, {Y})";

    public override bool Equals(object obj)
    {
        if (obj is Vector2d<T> other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode() => (X, Y).GetHashCode();

    public bool Equals(Vector2d<T> other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }
    #endregion
}