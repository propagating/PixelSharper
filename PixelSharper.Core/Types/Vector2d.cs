using System.ComponentModel.Design;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixelSharper.Core.Types;

public struct Vector2d<T> : 
    IAdditionOperators<Vector2d<T>, Vector2d<T>, Vector2d<T>>,
    IDivisionOperators<Vector2d<T>, T, Vector2d<T>>,
    IMultiplyOperators<Vector2d<T>, Vector2d<T>, Vector2d<T>>,
    IMultiplyOperators<Vector2d<T>, T, Vector2d<T>>,
    IModulusOperators<Vector2d<T>, Vector2d<T>, Vector2d<T>>,
    IModulusOperators<Vector2d<T>, T, Vector2d<T>>, 
    ISubtractionOperators<Vector2d<T>, Vector2d<T>, Vector2d<T>>,
    IUnaryPlusOperators<Vector2d<T>,Vector2d<T>>, 
    IUnaryNegationOperators<Vector2d<T>, Vector2d<T>>,
    IRootFunctions<Vector2d<T>>,
    IFormattable
    where T : INumberBase<T>
{

    public T X;
    public T Y;

    public Vector2d(T x, T y)
    {
        X = x;
        Y = y;
    }

    public static Vector2d<T> operator +(Vector2d<T> left, Vector2d<T> right)
    {
        return new Vector2d<T>(left.X + right.X, left.Y + right.Y);
    }

    public static Vector2d<T> operator /(Vector2d<T> left, T scalar)
    {
        return new Vector2d<T>(left.X / scalar, left.Y / scalar);
    }
    
    public static Vector2d<T> operator /(Vector2d<T> left, Vector2d<T> right)
    {
        throw new NotSupportedException("Cannot divide a vector by a vector.");

    }
    
    public static Vector2d<T> operator /(T scalar, Vector2d<T> vector)
    {
        throw new NotSupportedException("Cannot divide a scalar by a vector.");
    }


    public static Vector2d<T> operator *(Vector2d<T> left, Vector2d<T> right)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator *(Vector2d<T> left, T right)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator %(Vector2d<T> left, Vector2d<T> right)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator %(Vector2d<T> left, T right)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator -(Vector2d<T> left, Vector2d<T> right)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator +(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> operator -(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public bool Equals(Vector2d<T> other)
    {
        throw new NotImplementedException();
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        throw new NotImplementedException();
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Parse(string s, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Vector2d<T> result)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Vector2d<T> result)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> AdditiveIdentity { get; }
    public static Vector2d<T> operator --(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector2d<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }
    
    public static bool operator ==(Vector2d<T> left, Vector2d<T> right)
    {
        return left.X == right.X && left.Y == right.Y;
    }

    public static bool operator !=(Vector2d<T> left, Vector2d<T> right)
    {
        return left.X != right.X || left.Y != right.Y;
    }

    public static Vector2d<T> operator ++(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> MultiplicativeIdentity { get; }
    public static Vector2d<T> Abs(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsCanonical(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsComplexNumber(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsEvenInteger(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsFinite(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsImaginaryNumber(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsInfinity(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsInteger(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsNaN(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsNegative(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsNegativeInfinity(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsNormal(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsOddInteger(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsPositive(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsPositiveInfinity(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsRealNumber(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsSubnormal(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static bool IsZero(Vector2d<T> value)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> MaxMagnitude(Vector2d<T> x, Vector2d<T> y)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> MaxMagnitudeNumber(Vector2d<T> x, Vector2d<T> y)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> MinMagnitude(Vector2d<T> x, Vector2d<T> y)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> MinMagnitudeNumber(Vector2d<T> x, Vector2d<T> y)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertFromChecked<TOther>(TOther value, out Vector2d<T> result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertFromSaturating<TOther>(TOther value, out Vector2d<T> result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertFromTruncating<TOther>(TOther value, out Vector2d<T> result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertToChecked<TOther>(Vector2d<T> value, out TOther result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertToSaturating<TOther>(Vector2d<T> value, out TOther result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryConvertToTruncating<TOther>(Vector2d<T> value, out TOther result) where TOther : INumberBase<TOther>
    {
        throw new NotImplementedException();
    }

    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Vector2d<T> result)
    {
        throw new NotImplementedException();
    }

    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Vector2d<T> result)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> One { get; }
    public static int Radix { get; }
    public static Vector2d<T> Zero { get; }
    public static Vector2d<T> E { get; }
    public static Vector2d<T> Pi { get; }
    public static Vector2d<T> Tau { get; }
    public static Vector2d<T> Cbrt(Vector2d<T> x)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Hypot(Vector2d<T> x, Vector2d<T> y)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> RootN(Vector2d<T> x, int n)
    {
        throw new NotImplementedException();
    }

    public static Vector2d<T> Sqrt(Vector2d<T> x)
    {
        throw new NotImplementedException();
    }
}


