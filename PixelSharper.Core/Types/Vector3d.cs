namespace PixelSharper.Core.Types;

/// <summary>Port of olc::v_3d (float, olc's vf3d): a 3D vector with an optional homogeneous w component; concrete float type so the full scalar operator set is unambiguous.</summary>
/// <remarks>
/// <para>Equality (<see cref="op_Equality"/>, <see cref="Equals(Vector3d)"/>, <see cref="GetHashCode"/>) considers only X, Y and Z; the homogeneous <see cref="W"/> component is ignored.</para>
/// <para>Because the type is concretely <c>float</c> (unlike the generic <see cref="Vector2d{T}"/>), the full scalar operator set is unambiguous.</para>
/// </remarks>
/// <seealso cref="Matrix4x4"/>
/// <seealso cref="Vector2d{T}"/>
public struct Vector3d : IEquatable<Vector3d>
{
    /// <summary>X component.</summary>
    /// <value>The x coordinate.</value>
    public float X;
    /// <summary>Y component.</summary>
    /// <value>The y coordinate.</value>
    public float Y;
    /// <summary>Z component.</summary>
    /// <value>The z coordinate.</value>
    public float Z;
    /// <summary>Homogeneous w component (defaults to 1).</summary>
    /// <value>The homogeneous w coordinate; ignored by equality.</value>
    public float W;
    
    /// <summary>Indexes the components by position (0 = X, 1 = Y, 2 = Z).</summary>
    /// <param name="i">The component index (0–2).</param>
    /// <value>The component at index <paramref name="i"/>.</value>
    public double this[int i]
    {
        get
        {
            return i switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                _ => throw new IndexOutOfRangeException()
            };
        }
    }

    /// <summary>Constructs a vector from components; z defaults to 0 and w to 1.</summary>
    /// <param name="x">X component.</param>
    /// <param name="y">Y component.</param>
    /// <param name="z">Z component; defaults to <c>0</c>.</param>
    /// <param name="w">Homogeneous w component; defaults to <c>1</c>.</param>
    public Vector3d(float x, float y, float z = 0f, float w = 1f) { X = x; Y = y; Z = z; W = w; }

    /// <summary>The (X, Y) components as a 2D vector.</summary>
    /// <returns>A <see cref="Vector2d{T}"/> holding (<see cref="X"/>, <see cref="Y"/>).</returns>
    public Vector2d<float> Xy() => new(X, Y);
    /// <summary>The (X, Z) components as a 2D vector.</summary>
    /// <returns>A <see cref="Vector2d{T}"/> holding (<see cref="X"/>, <see cref="Z"/>).</returns>
    public Vector2d<float> Xz() => new(X, Z);
    /// <summary>The (Z, W) components as a 2D vector.</summary>
    /// <returns>A <see cref="Vector2d{T}"/> holding (<see cref="Z"/>, <see cref="W"/>).</returns>
    public Vector2d<float> Zw() => new(Z, W);

    /// <summary>Product of the three components (X*Y*Z).</summary>
    /// <returns>The scalar product <c>X * Y * Z</c>.</returns>
    public float Volume() => X * Y * Z;
    /// <summary>Euclidean magnitude (length).</summary>
    /// <returns>The length <c>sqrt(X*X + Y*Y + Z*Z)</c>.</returns>
    /// <seealso cref="Mag2"/>
    public float Mag() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    /// <summary>Squared magnitude (avoids the sqrt).</summary>
    /// <returns>The squared length <c>X*X + Y*Y + Z*Z</c>.</returns>
    /// <seealso cref="Mag"/>
    public float Mag2() => X * X + Y * Y + Z * Z;
    /// <summary>Returns the unit-length vector in the same direction.</summary>
    /// <returns>This vector scaled to unit length (X, Y, Z divided by <see cref="Mag"/>).</returns>
    public Vector3d Norm() { var r = 1f / Mag(); return new Vector3d(X * r, Y * r, Z * r); }
    /// <summary>Component-wise floor (preserves W).</summary>
    /// <returns>A vector with each of X, Y, Z floored; <see cref="W"/> is carried through unchanged.</returns>
    public Vector3d Floor() => new(MathF.Floor(X), MathF.Floor(Y), MathF.Floor(Z), W);
    /// <summary>Component-wise ceiling (preserves W).</summary>
    /// <returns>A vector with each of X, Y, Z ceiled; <see cref="W"/> is carried through unchanged.</returns>
    public Vector3d Ceil() => new(MathF.Ceiling(X), MathF.Ceiling(Y), MathF.Ceiling(Z), W);
    /// <summary>Component-wise maximum against another vector.</summary>
    /// <param name="v">The vector to take the per-component maximum with.</param>
    /// <returns>A vector whose X, Y, Z are the larger of this and <paramref name="v"/>.</returns>
    public Vector3d Max(Vector3d v) => new(MathF.Max(X, v.X), MathF.Max(Y, v.Y), MathF.Max(Z, v.Z));
    /// <summary>Component-wise minimum against another vector.</summary>
    /// <param name="v">The vector to take the per-component minimum with.</param>
    /// <returns>A vector whose X, Y, Z are the smaller of this and <paramref name="v"/>.</returns>
    public Vector3d Min(Vector3d v) => new(MathF.Min(X, v.X), MathF.Min(Y, v.Y), MathF.Min(Z, v.Z));
    /// <summary>Dot product.</summary>
    /// <param name="r">The right-hand vector.</param>
    /// <returns>The scalar dot product <c>X*r.X + Y*r.Y + Z*r.Z</c>.</returns>
    public float Dot(Vector3d r) => X * r.X + Y * r.Y + Z * r.Z;
    /// <summary>Cross product.</summary>
    /// <param name="r">The right-hand vector.</param>
    /// <returns>The vector perpendicular to this and <paramref name="r"/>.</returns>
    public Vector3d Cross(Vector3d r) => new(Y * r.Z - Z * r.Y, Z * r.X - X * r.Z, X * r.Y - Y * r.X);
    /// <summary>Clamps each component into the [lo, hi] range.</summary>
    /// <param name="lo">Per-component lower bound.</param>
    /// <param name="hi">Per-component upper bound.</param>
    /// <returns>A vector with each component clamped to <c>[lo, hi]</c>.</returns>
    /// <seealso cref="Max(Vector3d)"/>
    /// <seealso cref="Min(Vector3d)"/>
    public Vector3d Clamp(Vector3d lo, Vector3d hi) => Max(lo).Min(hi);
    /// <summary>Linear interpolation toward v by t.</summary>
    /// <param name="v">The target vector.</param>
    /// <param name="t">Interpolation factor; <c>0</c> yields this vector, <c>1</c> yields <paramref name="v"/>.</param>
    /// <returns>The interpolated vector <c>this*(1-t) + v*t</c>.</returns>
    public Vector3d Lerp(Vector3d v, float t) => this * (1f - t) + v * t;
    /// <summary>The four components as a float[4] (x, y, z, w).</summary>
    /// <returns>A new 4-element array <c>[X, Y, Z, W]</c>.</returns>
    public float[] ToArray() => new[] { X, Y, Z, W };

    /// <summary>Component-wise addition.</summary>
    /// <param name="a">Left operand.</param>
    /// <param name="b">Right operand.</param>
    /// <returns>The sum <c>(a.X+b.X, a.Y+b.Y, a.Z+b.Z)</c>.</returns>
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    /// <summary>Component-wise subtraction.</summary>
    /// <param name="a">Left operand (minuend).</param>
    /// <param name="b">Right operand (subtrahend).</param>
    /// <returns>The difference <c>(a.X-b.X, a.Y-b.Y, a.Z-b.Z)</c>.</returns>
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    /// <summary>Unary negation.</summary>
    /// <param name="a">The vector to negate.</param>
    /// <returns>The negated vector <c>(-a.X, -a.Y, -a.Z)</c>.</returns>
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    /// <summary>Scalar multiply (vector * scalar).</summary>
    /// <param name="a">The vector operand.</param>
    /// <param name="s">The scalar multiplier.</param>
    /// <returns>The scaled vector <c>(a.X*s, a.Y*s, a.Z*s)</c>.</returns>
    public static Vector3d operator *(Vector3d a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    /// <summary>Scalar multiply (scalar * vector).</summary>
    /// <param name="s">The scalar multiplier.</param>
    /// <param name="a">The vector operand.</param>
    /// <returns>The scaled vector <c>(a.X*s, a.Y*s, a.Z*s)</c>.</returns>
    public static Vector3d operator *(float s, Vector3d a) => new(a.X * s, a.Y * s, a.Z * s);
    /// <summary>Component-wise (Hadamard) multiply.</summary>
    /// <param name="a">Left operand.</param>
    /// <param name="b">Right operand.</param>
    /// <returns>The element-wise product <c>(a.X*b.X, a.Y*b.Y, a.Z*b.Z)</c>.</returns>
    public static Vector3d operator *(Vector3d a, Vector3d b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    /// <summary>Scalar divide.</summary>
    /// <param name="a">The vector operand (dividend).</param>
    /// <param name="s">The scalar divisor.</param>
    /// <returns>The scaled vector <c>(a.X/s, a.Y/s, a.Z/s)</c>.</returns>
    public static Vector3d operator /(Vector3d a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    /// <summary>Component-wise divide.</summary>
    /// <param name="a">Left operand (dividend).</param>
    /// <param name="b">Right operand (divisor).</param>
    /// <returns>The element-wise quotient <c>(a.X/b.X, a.Y/b.Y, a.Z/b.Z)</c>.</returns>
    public static Vector3d operator /(Vector3d a, Vector3d b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    /// <summary>Equality over X, Y, Z (W ignored).</summary>
    /// <param name="a">Left operand.</param>
    /// <param name="b">Right operand.</param>
    /// <returns><c>true</c> if X, Y and Z are all equal; otherwise <c>false</c>. <see cref="W"/> is not compared.</returns>
    public static bool operator ==(Vector3d a, Vector3d b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    /// <summary>Inequality over X, Y, Z.</summary>
    /// <param name="a">Left operand.</param>
    /// <param name="b">Right operand.</param>
    /// <returns><c>true</c> if X, Y or Z differ; otherwise <c>false</c>. <see cref="W"/> is not compared.</returns>
    public static bool operator !=(Vector3d a, Vector3d b) => !(a == b);

    /// <summary>Typed equality (compares X, Y, Z).</summary>
    /// <param name="o">The vector to compare against.</param>
    /// <returns><c>true</c> if X, Y and Z match; otherwise <c>false</c>.</returns>
    public bool Equals(Vector3d o) => this == o;
    /// <summary>Object equality.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Vector3d"/> with equal X, Y and Z; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is Vector3d v && Equals(v);
    /// <summary>Hash over X, Y, Z.</summary>
    /// <returns>A hash code combining X, Y and Z (excluding <see cref="W"/>).</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    /// <summary>Renders as "(X,Y,Z)".</summary>
    /// <returns>A string of the form <c>(X,Y,Z)</c>.</returns>
    public override string ToString() => $"({X},{Y},{Z})";
}
