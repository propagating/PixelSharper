using System;
using System.Runtime.CompilerServices;
using SnMatrix = System.Numerics.Matrix4x4;
using SnVector4 = System.Numerics.Vector4;

namespace PixelSharper.Core.Types;

// Port of olc::m_4d (float — olc's mf4d). A column-major 4x4 matrix (flat index = col*4 + row, to mirror
// OpenGL) used for HW3D model/view/projection transforms. **Value type, backed by System.Numerics for
// SIMD.** The backing _sn holds the TRANSPOSE of our logical matrix — which means _sn's raw row-major
// floats (M11..M44) are *exactly* our column-major data, so our indexer / ToArray / factory methods read
// and write the same bytes unchanged, while the hot multiply and vector transform route through
// System.Numerics' hardware-accelerated ops. Default ctor = identity; `default(Matrix4x4)` is all-zeros,
// so always build via `new Matrix4x4()` or the factory methods.
public struct Matrix4x4
{
    // Holds our-matrix^T. Its row-major M11..M44 == our column-major flat (col*4 + row), so we reinterpret
    // it as 16 floats for element access; multiply/transform use its SIMD operators directly.
    private SnMatrix _sn;

    public Matrix4x4() { _sn = SnMatrix.Identity; }
    private Matrix4x4(SnMatrix sn) { _sn = sn; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetFlat(int i) => Unsafe.Add(ref Unsafe.As<SnMatrix, float>(ref _sn), i);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlat(int i, float v) => Unsafe.Add(ref Unsafe.As<SnMatrix, float>(ref _sn), i) = v;

    private static int Idx(int c, int r) => c * 4 + r;
    public float this[int c, int r]
    {
        get => GetFlat(Idx(c, r));
        set => SetFlat(Idx(c, r), value);
    }

    // Flat (column-major) accessor as a float[16] — e.g. for HW3D_Projection / GPUTask.Mvp.
    public float[] ToArray()
    {
        var a = new float[16];
        for (var i = 0; i < 16; i++) a[i] = GetFlat(i);
        return a;
    }

    public static Matrix4x4 Translation(float x, float y, float z)
    {
        var m = new Matrix4x4();
        m[3, 0] = x; m[3, 1] = y; m[3, 2] = z;
        return m;
    }

    public static Matrix4x4 Translation(Vector3d v) => Translation(v.X, v.Y, v.Z);

    public static Matrix4x4 Scaling(float x, float y, float z)
    {
        var m = new Matrix4x4();
        m[0, 0] = x; m[1, 1] = y; m[2, 2] = z;
        return m;
    }

    public static Matrix4x4 Scaling(Vector3d v) => Scaling(v.X, v.Y, v.Z);

    public static Matrix4x4 RotateX(float rads)
    {
        var m = new Matrix4x4();
        m[1, 1] = MathF.Cos(rads); m[1, 2] = MathF.Sin(rads); m[2, 1] = -m[1, 2]; m[2, 2] = m[1, 1];
        return m;
    }

    public static Matrix4x4 RotateY(float rads)
    {
        var m = new Matrix4x4();
        m[0, 0] = MathF.Cos(rads); m[0, 2] = MathF.Sin(rads); m[2, 0] = -m[0, 2]; m[2, 2] = m[0, 0];
        return m;
    }

    public static Matrix4x4 RotateZ(float rads)
    {
        var m = new Matrix4x4();
        m[0, 0] = MathF.Cos(rads); m[0, 1] = MathF.Sin(rads); m[1, 0] = -m[0, 1]; m[1, 1] = m[0, 0];
        return m;
    }

    public static Matrix4x4 Projection(float fov, float ratio, float near, float far)
    {
        var m = new Matrix4x4();
        var invFov = 1f / MathF.Tan(fov * 0.5f);
        m[0, 0] = -invFov / ratio;
        m[1, 1] = invFov;
        m[2, 2] = -far / (far - near);
        m[3, 2] = -(far * near) / (far - near);
        m[2, 3] = -1f;
        m[3, 3] = 0f;
        return m;
    }

    // Look-from matrix (camera placement); QuickInvert() gives the corresponding "look at" view.
    public static Matrix4x4 PointAt(Vector3d origin, Vector3d target, Vector3d up)
    {
        var m = new Matrix4x4();
        var vF = (target - origin).Norm();
        var vU = (up - vF * up.Dot(vF)).Norm();
        var vR = vU.Cross(vF);
        m[0, 0] = vR.X; m[0, 1] = vR.Y; m[0, 2] = vR.Z;
        m[1, 0] = vU.X; m[1, 1] = vU.Y; m[1, 2] = vU.Z;
        m[2, 0] = vF.X; m[2, 1] = vF.Y; m[2, 2] = vF.Z;
        m[3, 0] = origin.X; m[3, 1] = origin.Y; m[3, 2] = origin.Z; m[3, 3] = 1f;
        return m;
    }

    public Matrix4x4 Transpose()
    {
        var o = new Matrix4x4();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                o[i, j] = this[j, i];
        return o;
    }

    // Fast inverse for a 4x4 with no scale component (e.g. the inverse of PointAt -> a view matrix).
    public Matrix4x4 QuickInvert()
    {
        var o = new Matrix4x4();
        o[0, 0] = this[0, 0]; o[0, 1] = this[1, 0]; o[0, 2] = this[2, 0]; o[0, 3] = 0f;
        o[1, 0] = this[0, 1]; o[1, 1] = this[1, 1]; o[1, 2] = this[2, 1]; o[1, 3] = 0f;
        o[2, 0] = this[0, 2]; o[2, 1] = this[1, 2]; o[2, 2] = this[2, 2]; o[2, 3] = 0f;
        o[3, 0] = -(this[3, 0] * o[0, 0] + this[3, 1] * o[1, 0] + this[3, 2] * o[2, 0]);
        o[3, 1] = -(this[3, 0] * o[0, 1] + this[3, 1] * o[1, 1] + this[3, 2] * o[2, 1]);
        o[3, 2] = -(this[3, 0] * o[0, 2] + this[3, 1] * o[1, 2] + this[3, 2] * o[2, 2]);
        o[3, 3] = 1f;
        return o;
    }

    // Full general inverse (gluInvertMatrix). Costly — avoid per-frame use.
    public Matrix4x4 Invert()
    {
        var m = ToArray();      // local column-major copy
        var r = new float[16];
        r[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
        r[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
        r[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        r[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        r[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        r[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        r[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        r[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        r[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        r[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        r[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        r[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        r[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        r[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        r[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        r[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];

        var det = m[0] * r[0] + m[1] * r[4] + m[2] * r[8] + m[3] * r[12];
        var invdet = 1f / det;
        var o = new Matrix4x4();
        for (var i = 0; i < 16; i++) o.SetFlat(i, r[i] * invdet);
        return o;
    }

    // Transform a vector (homogeneous, includes the w row). _sn = M^T, so v * M^T (row-vector) == M * v.
    public static Vector3d operator *(Matrix4x4 me, Vector3d v)
    {
        var t = SnVector4.Transform(new SnVector4(v.X, v.Y, v.Z, v.W), me._sn);
        return new Vector3d(t.X, t.Y, t.Z, t.W);
    }

    // Our column-vector product A*B applies B then A (= standard A·B). With _sn = M^T this is
    // (A·B)^T = B^T · A^T = b._sn * a._sn (System.Numerics SIMD multiply, operands reversed).
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new(b._sn * a._sn);
}
