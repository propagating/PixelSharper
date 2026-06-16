namespace PixelSharper.Core.Types;

/// <summary>2D affine transform as a 3x3 float matrix (olc mat3_generic{float}), point*matrix convention; a reference type so each operator returns a fresh instance and transform chains never alias storage.</summary>
public class Matrix2D
{
    /// <summary>Row-major 9-element backing store for the 3x3 matrix.</summary>
    private readonly float[] _m = new float[9];

    /// <summary>Creates a matrix initialised to identity.</summary>
    /// <seealso cref="Identity"/>
    public Matrix2D() => Identity();

    /// <summary>Maps a (row, column) pair to the flat backing-array index.</summary>
    /// <param name="r">The row index (0..2).</param>
    /// <param name="c">The column index (0..2).</param>
    /// <returns>The index into the row-major 9-element backing store.</returns>
    private static int Idx(int r, int c) => r * 3 + c;

    /// <summary>Indexed access to the element at the given row and column.</summary>
    /// <param name="r">The row index (0..2).</param>
    /// <param name="c">The column index (0..2).</param>
    /// <value>The matrix element at row <paramref name="r"/>, column <paramref name="c"/>.</value>
    /// <returns>The element value at the given row and column.</returns>
    public float this[int r, int c]
    {
        get => _m[Idx(r, c)];
        set => _m[Idx(r, c)] = value;
    }

    /// <summary>Zeros every element.</summary>
    public void Clear() => System.Array.Clear(_m, 0, 9);

    /// <summary>Resets to the identity matrix.</summary>
    /// <seealso cref="Clear"/>
    public void Identity()
    {
        Clear();
        this[0, 0] = 1; this[1, 1] = 1; this[2, 2] = 1;
    }

    /// <summary>Sets this to a translation by (x, y).</summary>
    /// <param name="x">The translation along X.</param>
    /// <param name="y">The translation along Y.</param>
    public void Translate(float x, float y) { Identity(); this[2, 0] = x; this[2, 1] = y; }
    /// <summary>Sets this to a translation by a vector.</summary>
    /// <param name="v">The translation as a vector; its <c>X</c>/<c>Y</c> become the translation amounts.</param>
    /// <seealso cref="Translate(float, float)"/>
    public void Translate(Vector2d<float> v) => Translate(v.X, v.Y);
    /// <summary>Sets this to a scale by (x, y).</summary>
    /// <param name="x">The scale factor along X.</param>
    /// <param name="y">The scale factor along Y.</param>
    public void Scale(float x, float y) { Identity(); this[0, 0] = x; this[1, 1] = y; }
    /// <summary>Sets this to a scale by a vector's components.</summary>
    /// <param name="v">The scale as a vector; its <c>X</c>/<c>Y</c> become the scale factors.</param>
    /// <seealso cref="Scale(float, float)"/>
    public void Scale(Vector2d<float> v) => Scale(v.X, v.Y);

    /// <summary>Sets this to a rotation by angle a (radians).</summary>
    /// <param name="a">The rotation angle in radians.</param>
    public void Rotate(float a)
    {
        Identity();
        this[0, 0] = MathF.Cos(a); this[0, 1] = MathF.Sin(a);
        this[1, 0] = -this[0, 1]; this[1, 1] = this[0, 0];
    }

    /// <summary>Transforms a point (homogeneous, with the perspective divide olc applies).</summary>
    /// <param name="m">The transform matrix applied to the point.</param>
    /// <param name="v">The point to transform.</param>
    /// <returns>The transformed point after the homogeneous perspective divide.</returns>
    /// <seealso cref="op_Multiply(Matrix2D, Matrix2D)"/>
    public static Vector2d<float> operator *(Matrix2D m, Vector2d<float> v)
    {
        var x = m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0];
        var y = m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1];
        var z = m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2];
        return new Vector2d<float>(x / z, y / z);
    }

    /// <summary>Composes two transforms (matrix product), returning a new matrix.</summary>
    /// <param name="a">The left-hand (first-applied under point*matrix convention) matrix.</param>
    /// <param name="b">The right-hand matrix.</param>
    /// <returns>A new matrix that is the product <paramref name="a"/> * <paramref name="b"/>.</returns>
    /// <remarks>
    /// <para>Returns a fresh instance; neither operand is mutated, so transform chains never alias storage.</para>
    /// </remarks>
    /// <seealso cref="op_Multiply(Matrix2D, Vector2d{float})"/>
    public static Matrix2D operator *(Matrix2D a, Matrix2D b)
    {
        var o = new Matrix2D();
        for (var c = 0; c < 3; c++)
            for (var r = 0; r < 3; r++)
                o[r, c] = a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
        return o;
    }
}
