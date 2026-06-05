using System;

namespace PixelSharper.Core.Types;

// Port of olc::v_3d (float — olc's vf3d). A 3D vector with an optional w component for homogeneous
// transforms. Concrete float type, so the full scalar operator set is unambiguous (unlike the
// generic Vector2d, where typed scalar overloads collide with the generic T ones).
public struct Vector3d : IEquatable<Vector3d>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector3d(float x, float y, float z = 0f, float w = 1f) { X = x; Y = y; Z = z; W = w; }

    public Vector2d<float> Xy() => new(X, Y);
    public Vector2d<float> Xz() => new(X, Z);
    public Vector2d<float> Zw() => new(Z, W);

    public float Volume() => X * Y * Z;
    public float Mag() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float Mag2() => X * X + Y * Y + Z * Z;
    public Vector3d Norm() { var r = 1f / Mag(); return new Vector3d(X * r, Y * r, Z * r); }
    public Vector3d Floor() => new(MathF.Floor(X), MathF.Floor(Y), MathF.Floor(Z), W);
    public Vector3d Ceil() => new(MathF.Ceiling(X), MathF.Ceiling(Y), MathF.Ceiling(Z), W);
    public Vector3d Max(Vector3d v) => new(MathF.Max(X, v.X), MathF.Max(Y, v.Y), MathF.Max(Z, v.Z));
    public Vector3d Min(Vector3d v) => new(MathF.Min(X, v.X), MathF.Min(Y, v.Y), MathF.Min(Z, v.Z));
    public float Dot(Vector3d r) => X * r.X + Y * r.Y + Z * r.Z;
    public Vector3d Cross(Vector3d r) => new(Y * r.Z - Z * r.Y, Z * r.X - X * r.Z, X * r.Y - Y * r.X);
    public Vector3d Clamp(Vector3d lo, Vector3d hi) => Max(lo).Min(hi);
    public Vector3d Lerp(Vector3d v, float t) => this * (1f - t) + v * t;
    public float[] ToArray() => new[] { X, Y, Z, W };

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(float s, Vector3d a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(Vector3d a, Vector3d b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3d operator /(Vector3d a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    public static Vector3d operator /(Vector3d a, Vector3d b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static bool operator ==(Vector3d a, Vector3d b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3d a, Vector3d b) => !(a == b);

    public bool Equals(Vector3d o) => this == o;
    public override bool Equals(object obj) => obj is Vector3d v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X},{Y},{Z})";
}
