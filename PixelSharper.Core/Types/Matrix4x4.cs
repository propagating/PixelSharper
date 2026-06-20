using System.Runtime.CompilerServices;
using SnMatrix = System.Numerics.Matrix4x4;
using SnVector4 = System.Numerics.Vector4;

namespace PixelSharper.Core.Types;

/// <summary>Port of olc::m_4d (float, olc's mf4d): a column-major 4x4 transform matrix for HW3D model/view/projection, backed by System.Numerics for SIMD. The backing _sn holds the transpose, so its row-major floats equal our column-major data. Build via new Matrix4x4() or factories, never default (all-zeros).</summary>
/// <remarks>
/// <para>The concrete <c>float</c> element type means the full operator set (matrix*matrix and matrix*vector) is unambiguous.</para>
/// <para>Always construct via <see cref="Matrix4x4()"/> or a factory (<see cref="Translation(float,float,float)"/>, <see cref="Scaling(float,float,float)"/>, the <c>Rotate*</c> / <see cref="Projection"/> / <see cref="PointAt"/> methods); the <c>default</c> value is all-zeros and not a valid transform.</para>
/// </remarks>
/// <seealso cref="Vector3d"/>
public struct Matrix4x4
{
    /// <summary>Backing store holding our-matrix transposed; reinterpreted as 16 floats for element access while multiply/transform use its SIMD operators.</summary>
    /// <value>A <see cref="System.Numerics.Matrix4x4"/> whose row-major layout equals our column-major data.</value>
    private SnMatrix _sn;

    /// <summary>Constructs the identity matrix.</summary>
    public Matrix4x4() { _sn = SnMatrix.Identity; }
    /// <summary>Wraps an existing System.Numerics matrix (already in transposed form).</summary>
    /// <param name="sn">The backing matrix, already stored in transposed form.</param>
    private Matrix4x4(SnMatrix sn) { _sn = sn; }

    /// <summary>Reads the i-th float of the flat column-major layout.</summary>
    /// <param name="i">Flat index in <c>[0, 16)</c>.</param>
    /// <returns>The float at flat index <paramref name="i"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetFlat(int i) => Unsafe.Add(ref Unsafe.As<SnMatrix, float>(ref _sn), i);

    /// <summary>Writes the i-th float of the flat column-major layout.</summary>
    /// <param name="i">Flat index in <c>[0, 16)</c>.</param>
    /// <param name="v">The value to store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlat(int i, float v) => Unsafe.Add(ref Unsafe.As<SnMatrix, float>(ref _sn), i) = v;

    /// <summary>Maps (column, row) to the flat column-major index (col*4 + row).</summary>
    /// <param name="c">Column index <c>[0, 4)</c>.</param>
    /// <param name="r">Row index <c>[0, 4)</c>.</param>
    /// <returns>The flat column-major index <c>c*4 + r</c>.</returns>
    private static int Idx(int c, int r) => c * 4 + r;
    /// <summary>Element access by column and row.</summary>
    /// <param name="c">Column index <c>[0, 4)</c>.</param>
    /// <param name="r">Row index <c>[0, 4)</c>.</param>
    /// <value>The float at column <paramref name="c"/>, row <paramref name="r"/>.</value>
    /// <returns>The float at column <paramref name="c"/>, row <paramref name="r"/>.</returns>
    public float this[int c, int r]
    {
        get => GetFlat(Idx(c, r));
        set => SetFlat(Idx(c, r), value);
    }

    /// <summary>The matrix as a flat column-major float[16] (e.g. for HW3D_Projection / GPUTask.Mvp).</summary>
    /// <returns>A new 16-element array in flat column-major order.</returns>
    /// <seealso cref="GPUTask.Mvp"/>
    public float[] ToArray()
    {
        var a = new float[16];
        for (var i = 0; i < 16; i++) a[i] = GetFlat(i);
        return a;
    }

    /// <summary>Builds a translation matrix.</summary>
    /// <param name="x">Translation along X.</param>
    /// <param name="y">Translation along Y.</param>
    /// <param name="z">Translation along Z.</param>
    /// <returns>An identity matrix with the translation set in its last column.</returns>
    public static Matrix4x4 Translation(float x, float y, float z)
    {
        var m = new Matrix4x4();
        m[3, 0] = x; m[3, 1] = y; m[3, 2] = z;
        return m;
    }

    /// <summary>Builds a translation matrix from a vector.</summary>
    /// <param name="v">Translation vector; its X, Y, Z are used.</param>
    /// <returns>An identity matrix translated by <paramref name="v"/>.</returns>
    /// <seealso cref="Translation(float,float,float)"/>
    public static Matrix4x4 Translation(Vector3d v) => Translation(v.X, v.Y, v.Z);

    /// <summary>Builds a non-uniform scaling matrix.</summary>
    /// <param name="x">Scale factor along X.</param>
    /// <param name="y">Scale factor along Y.</param>
    /// <param name="z">Scale factor along Z.</param>
    /// <returns>An identity matrix with the diagonal set to the given scale factors.</returns>
    public static Matrix4x4 Scaling(float x, float y, float z)
    {
        var m = new Matrix4x4();
        m[0, 0] = x; m[1, 1] = y; m[2, 2] = z;
        return m;
    }

    /// <summary>Builds a scaling matrix from a vector.</summary>
    /// <param name="v">Scale vector; its X, Y, Z are used as the per-axis factors.</param>
    /// <returns>An identity matrix scaled by <paramref name="v"/>.</returns>
    /// <seealso cref="Scaling(float,float,float)"/>
    public static Matrix4x4 Scaling(Vector3d v) => Scaling(v.X, v.Y, v.Z);

    /// <summary>Builds a rotation matrix about the X axis (radians).</summary>
    /// <param name="rads">Rotation angle in radians.</param>
    /// <returns>A rotation matrix about the X axis.</returns>
    public static Matrix4x4 RotateX(float rads)
    {
        var m = new Matrix4x4();
        m[1, 1] = MathF.Cos(rads); m[1, 2] = MathF.Sin(rads); m[2, 1] = -m[1, 2]; m[2, 2] = m[1, 1];
        return m;
    }

    /// <summary>Builds a rotation matrix about the Y axis (radians).</summary>
    /// <param name="rads">Rotation angle in radians.</param>
    /// <returns>A rotation matrix about the Y axis.</returns>
    public static Matrix4x4 RotateY(float rads)
    {
        var m = new Matrix4x4();
        m[0, 0] = MathF.Cos(rads); m[0, 2] = MathF.Sin(rads); m[2, 0] = -m[0, 2]; m[2, 2] = m[0, 0];
        return m;
    }

    /// <summary>Builds a rotation matrix about the Z axis (radians).</summary>
    /// <param name="rads">Rotation angle in radians.</param>
    /// <returns>A rotation matrix about the Z axis.</returns>
    public static Matrix4x4 RotateZ(float rads)
    {
        var m = new Matrix4x4();
        m[0, 0] = MathF.Cos(rads); m[0, 1] = MathF.Sin(rads); m[1, 0] = -m[0, 1]; m[1, 1] = m[0, 0];
        return m;
    }

    /// <summary>Builds a perspective projection matrix from field-of-view, aspect ratio, and near/far planes.</summary>
    /// <param name="fov">Vertical field of view in radians.</param>
    /// <param name="ratio">Aspect ratio (width / height).</param>
    /// <param name="near">Distance to the near clip plane.</param>
    /// <param name="far">Distance to the far clip plane.</param>
    /// <returns>A perspective projection matrix.</returns>
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

    /// <summary>Builds a look-from (camera placement) matrix; QuickInvert() gives the corresponding look-at view.</summary>
    /// <param name="origin">Camera position (the look-from point).</param>
    /// <param name="target">Point the camera looks toward.</param>
    /// <param name="up">Approximate up direction; re-orthogonalised against the look axis.</param>
    /// <returns>A camera-placement matrix; pass it through <see cref="QuickInvert"/> for the view matrix.</returns>
    /// <seealso cref="QuickInvert"/>
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

    /// <summary>Returns the transpose.</summary>
    /// <returns>A new matrix that is the transpose of this one.</returns>
    public Matrix4x4 Transpose()
    {
        var o = new Matrix4x4();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                o[i, j] = this[j, i];
        return o;
    }

    /// <summary>Fast inverse valid for a rigid (no-scale) 4x4, e.g. inverting PointAt into a view matrix.</summary>
    /// <returns>The inverse, valid only for rigid (rotation + translation, no scale/shear) transforms.</returns>
    /// <seealso cref="PointAt"/>
    /// <seealso cref="Invert"/>
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

    /// <summary>Full general inverse (gluInvertMatrix); costly, avoid per-frame use.</summary>
    /// <returns>The full general inverse of this matrix.</returns>
    /// <remarks>
    /// <para>This is the costly cofactor-based inverse; prefer <see cref="QuickInvert"/> for rigid transforms.</para>
    /// </remarks>
    /// <seealso cref="QuickInvert"/>
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

    /// <summary>Transforms a homogeneous vector by the matrix (includes the w row).</summary>
    /// <param name="me">The transform matrix.</param>
    /// <param name="v">The homogeneous vector to transform; its <see cref="Vector3d.W"/> participates.</param>
    /// <returns>The transformed homogeneous vector.</returns>
    public static Vector3d operator *(Matrix4x4 me, Vector3d v)
    {
        var t = SnVector4.Transform(new SnVector4(v.X, v.Y, v.Z, v.W), me._sn);
        return new Vector3d(t.X, t.Y, t.Z, t.W);
    }

    /// <summary>Matrix product A*B (applies B then A); routed through SIMD as b._sn * a._sn since the backing store is transposed.</summary>
    /// <param name="a">Left operand (applied second).</param>
    /// <param name="b">Right operand (applied first).</param>
    /// <returns>The product <c>a*b</c>, which applies <paramref name="b"/> then <paramref name="a"/>.</returns>
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new(b._sn * a._sn);
}
