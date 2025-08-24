using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixelSharper.Core.Types;

public struct Vector2d<T> : IEquatable<Vector2d<T>> where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
{
    public T X { get; set; }
    public T Y { get; set; }

    public Vector2d(T x, T y)
    {
        // Ensure X and Y are valid numeric types
        if (!IsValidNumeric(x) || !IsValidNumeric(y))
            throw new ArgumentException("X and Y must be valid numeric types.");

        X = x;
        Y = y;
    }

    #region validation
    private static bool IsValidNumeric<U>(U value) where U : struct, INumber<U>
    {
        try
        {
            var _ = U.One; // Accessing INumber interface property triggers a compile-time check
            return true;
        }
        catch
        {
            return false;
        }
    }


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

    // Arithmetic operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator +(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator -(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X * scalar, a.Y * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(T scalar, Vector2d<T> a) => new Vector2d<T>(a.X * scalar, a.Y * scalar);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, float scalar)
    {
        T tScalar = T.CreateChecked(scalar);
        return new Vector2d<T>(a.X * tScalar, a.Y * tScalar);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(float scalar, Vector2d<T> a)
    {
        T tScalar = T.CreateChecked(scalar);
        return new Vector2d<T>(a.X * tScalar, a.Y * tScalar);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(Vector2d<T> a, double scalar)
    {
        T tScalar = T.CreateChecked(scalar);
        return new Vector2d<T>(a.X * tScalar, a.Y * tScalar);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator *(double scalar, Vector2d<T> a)
    {
        T tScalar = T.CreateChecked(scalar);
        return new Vector2d<T>(a.X * tScalar, a.Y * tScalar);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, T scalar) => new Vector2d<T>(a.X / scalar, a.Y / scalar);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, int scalar) => new Vector2d<T>(a.X / T.CreateChecked(scalar), a.Y / T.CreateChecked(scalar));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, float scalar) => new Vector2d<T>(a.X / T.CreateChecked(scalar), a.Y / T.CreateChecked(scalar));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, double scalar) => new Vector2d<T>(a.X / T.CreateChecked(scalar), a.Y / T.CreateChecked(scalar));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<T> b) => new Vector2d<T>(a.X / b.X, a.Y / b.Y);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<int> b) => new Vector2d<T>(a.X /T.CreateChecked(b.X),  T.CreateChecked(b.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<float> b) => new Vector2d<T>(a.X / T.CreateChecked(b.X),  T.CreateChecked(b.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> operator /(Vector2d<T> a, Vector2d<double> b) => new Vector2d<T>(a.X / T.CreateChecked(b.X),  T.CreateChecked(b.Y));
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
        return new Vector2d<T>(
            T.CreateChecked(Math.Min(Convert.ToDouble(a.X), Convert.ToDouble(b.X))),
            T.CreateChecked(Math.Min(Convert.ToDouble(a.Y), Convert.ToDouble(b.Y)))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Max(Vector2d<T> a, Vector2d<T> b)
    {
        return new Vector2d<T>(
            T.CreateChecked(Math.Max(Convert.ToDouble(a.X), Convert.ToDouble(b.X))),
            T.CreateChecked(Math.Max(Convert.ToDouble(a.Y), Convert.ToDouble(b.Y)))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (T r, T theta) ToPolar()
    {
        double r = MagnitudeAsDouble();
        double theta = Math.Atan2(Convert.ToDouble(Y), Convert.ToDouble(X));
        return (T.CreateChecked(ConvertToT(r)), T.CreateChecked(ConvertToT(theta)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> FromPolar(T r, T theta)
    {
        double rDouble = Convert.ToDouble(r);
        double thetaDouble = Convert.ToDouble(theta);
        T x = T.CreateChecked(ConvertToT(rDouble * Math.Cos(thetaDouble)));
        T y = T.CreateChecked(ConvertToT(rDouble * Math.Sin(thetaDouble)));
        return new Vector2d<T>(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2d<T> Clamp(Vector2d<T> min, Vector2d<T> max)
    {
        return new Vector2d<T>(
            T.CreateChecked(Math.Max(Convert.ToDouble(X), Convert.ToDouble(min.X))),
            T.CreateChecked(Math.Min(Convert.ToDouble(Y), Convert.ToDouble(max.Y)))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d<T> Lerp(Vector2d<T> a, Vector2d<T> b, float t, bool forceInteger = false)
    {
        if(forceInteger) return a + (b - a) * t; 
        bool isNotFloatingPoint = !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<float>)) && !typeof(T).IsAssignableTo(typeof(IFloatingPointIeee754<double>));
        if (isNotFloatingPoint) throw new InvalidOperationException("Lerp with integer types may lose precision. Set forceInteger = true to allow.");
        return a + (b - a) * t; 
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
        return Math.Sqrt(Convert.ToDouble(X) * Convert.ToDouble(X) + Convert.ToDouble(Y) * Convert.ToDouble(Y));
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
        //unfortunately Math.Sqrt only works with double and we cannot do this without boxing distance squared
        return Math.Sqrt(Convert.ToDouble(DistanceSquared(a, b)));
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
    #endregion
    
    #region interface methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ConvertToT(double value)
    {
        return (T)Convert.ChangeType(value, typeof(T));
    }

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