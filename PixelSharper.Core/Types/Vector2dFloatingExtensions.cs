using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixelSharper.Core.Types;

/// <summary>
/// Same-type rounding helpers for floating <see cref="Vector2d{T}"/>, built on <see cref="IFloatingPoint{TSelf}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These are C# 14 extension members rather than instance methods on the struct on purpose. The struct is
/// constrained only to <see cref="INumber{T}"/> (so <c>Vector2d&lt;int&gt;</c> stays valid), and these operations
/// require <see cref="IFloatingPoint{TSelf}"/>. Because the struct has no parameterless <c>Round</c>/<c>Truncate</c>/
/// <c>Floor</c>/<c>Ceiling</c> members, these extensions are <b>not shadowed</b> and bind cleanly — and they only
/// exist for floating <c>T</c> (<c>float</c>, <c>double</c>, <c>Half</c>, <c>decimal</c>, …),
/// where rounding is meaningful. (Contrast the in-place <see cref="Vector2d{T}.Magnitude"/>/<see cref="Vector2d{T}.ToPolar"/>
/// fast paths, which had to be specialized inside the existing methods because same-named extensions would be shadowed.)
/// </para>
/// <para>
/// Unlike the cross-type <see cref="Vector2d{T}.Floor{TFloating}"/>/<see cref="Vector2d{T}.Ceiling{TFloating}"/>
/// (which convert to a different floating type), these stay in <c>T</c> and call the
/// <see cref="IFloatingPoint{TSelf}"/> members directly.
/// </para>
/// </remarks>
public static class Vector2dFloatingExtensions
{
    extension<T>(Vector2d<T> v) where T : struct, INumber<T>, IFloatingPoint<T>, IEquatable<T>, IComparable<T>
    {
        /// <summary>Rounds each component to the nearest integral value (banker's rounding), staying in <typeparamref name="T"/>.</summary>
        /// <returns>A vector with each component rounded via <see cref="IFloatingPoint{TSelf}.Round(TSelf)"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2d<T> Round() => new(T.Round(v.X), T.Round(v.Y));

        /// <summary>Truncates each component toward zero, staying in <typeparamref name="T"/>.</summary>
        /// <returns>A vector with each component truncated via <see cref="IFloatingPoint{TSelf}.Truncate(TSelf)"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2d<T> Truncate() => new(T.Truncate(v.X), T.Truncate(v.Y));

        /// <summary>Floors each component toward negative infinity, staying in <typeparamref name="T"/>.</summary>
        /// <returns>A vector with each component floored via <see cref="IFloatingPoint{TSelf}.Floor(TSelf)"/>.</returns>
        /// <seealso cref="Vector2d{T}.Floor{TFloating}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2d<T> Floored() => new(T.Floor(v.X), T.Floor(v.Y));

        /// <summary>Ceils each component toward positive infinity, staying in <typeparamref name="T"/>.</summary>
        /// <returns>A vector with each component ceiled via <see cref="IFloatingPoint{TSelf}.Ceiling(TSelf)"/>.</returns>
        /// <seealso cref="Vector2d{T}.Ceiling{TFloating}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2d<T> Ceiled() => new(T.Ceiling(v.X), T.Ceiling(v.Y));
    }
}
