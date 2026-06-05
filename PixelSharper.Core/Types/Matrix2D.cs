namespace PixelSharper.Core.Types;

// 2D affine transform as a 3x3 matrix (olc's mat3_generic<float> / Matrix2D), point*matrix
// convention. Shared by the Wireframe and (future) TransformedView extensions. Reference type:
// every operator returns a fresh instance, so building transform chains never aliases storage.
public class Matrix2D
{
    private readonly float[] _m = new float[9];

    public Matrix2D() => Identity();

    private static int Idx(int r, int c) => r * 3 + c;

    public float this[int r, int c]
    {
        get => _m[Idx(r, c)];
        set => _m[Idx(r, c)] = value;
    }

    public void Clear() => System.Array.Clear(_m, 0, 9);

    public void Identity()
    {
        Clear();
        this[0, 0] = 1; this[1, 1] = 1; this[2, 2] = 1;
    }

    public void Translate(float x, float y) { Identity(); this[2, 0] = x; this[2, 1] = y; }
    public void Translate(Vector2d<float> v) => Translate(v.X, v.Y);
    public void Scale(float x, float y) { Identity(); this[0, 0] = x; this[1, 1] = y; }
    public void Scale(Vector2d<float> v) => Scale(v.X, v.Y);

    public void Rotate(float a)
    {
        Identity();
        this[0, 0] = MathF.Cos(a); this[0, 1] = MathF.Sin(a);
        this[1, 0] = -this[0, 1]; this[1, 1] = this[0, 0];
    }

    // Transform a point (homogeneous, with the perspective divide olc applies).
    public static Vector2d<float> operator *(Matrix2D m, Vector2d<float> v)
    {
        var x = m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0];
        var y = m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1];
        var z = m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2];
        return new Vector2d<float>(x / z, y / z);
    }

    public static Matrix2D operator *(Matrix2D a, Matrix2D b)
    {
        var o = new Matrix2D();
        for (var c = 0; c < 3; c++)
            for (var r = 0; r < 3; r++)
                o[r, c] = a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
        return o;
    }
}
