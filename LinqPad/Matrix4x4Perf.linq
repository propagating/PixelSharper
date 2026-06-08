<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
</Query>

// ===========================================================================================
// Matrix4x4: ORIGINAL (class wrapping float[16], scalar multiply) vs CHANGED (value type backed
// by System.Numerics.Matrix4x4, SIMD multiply). Run in LINQPad 8 (.NET 8).
//
// WHY THE CHANGE WAS MADE
//   The HW3D / Camera3D path composes matrices every frame (projection*view, view*model per object).
//   Two problems with the original:
//     1) It was a CLASS. Every operator (e.g. a*b) allocated a new object on the heap (a float[16] +
//        object headers). In a render loop that is steady gen0 garbage -> GC pauses.
//     2) The multiply was SCALAR (64 mul + 48 add, one float at a time). A 4x4 multiply is the
//        textbook SIMD candidate (4 floats per instruction).
//
// WHAT WE CHANGED IT TO
//   A value-type struct backed by System.Numerics.Matrix4x4, which is (a) a struct -> no heap
//   allocation, and (b) JIT-intrinsified -> the multiply emits SSE/AVX. We didn't hand-write
//   intrinsics; System.Numerics already is the maintained, portable SIMD layer.
//
// THE ONE SUBTLETY (why it's not a drop-in)
//   Our matrix is COLUMN-major (M*v, translation in the 4th column); System.Numerics is ROW-major
//   (v*M, translation in the 4th row). They are transposes. We store _sn = ourMatrix^T, which means
//   _sn's raw row-major floats ARE our column-major data (so indexer/ToArray are unchanged), and the
//   column-vector product a*b maps to (A.B)^T = b._sn * a._sn (System.Numerics multiply, REVERSED).
//
// WHY IT'S SO MUCH FASTER
//   class -> struct removes the per-op heap allocation entirely (Allocated drops to 0).
//   scalar -> SIMD does the 16-element multiply ~4 lanes at a time (~5x here).
// ===========================================================================================

const int N = 1000;   // an "op" = compose proj*view*model N times
const int Reps = 500;

void Main()
{
    // Originals
    var oa = OldMat.RotY(0.5f); var ob = OldMat.RotX(0.3f); var oc = OldMat.Trans(1, 2, 3);
    // Changed (System.Numerics is what backs our new struct)
    var na = Matrix4x4.CreateRotationY(0.5f); var nb = Matrix4x4.CreateRotationX(0.3f); var nc = Matrix4x4.CreateTranslation(1, 2, 3);

    var rows = new List<Row>
    {
        Bench("ORIGINAL  (class, scalar)", () => { var r = oa * ob * oc; _sink += r.M[0]; }),
        Bench("CHANGED   (struct, SIMD)", () => { var r = na * nb * nc; _sink += r.M11; }),
    };
    rows.Dump("Matrix4x4 multiply — original vs changed");
    $"(sink={_sink:F0})".Dump();
}

double _sink;

Row Bench(string label, Action body)
{
    for (var i = 0; i < N; i++) body();                 // warm up
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var before = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < Reps; r++) for (var i = 0; i < N; i++) body();
    sw.Stop();
    var alloc = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Reps;
    return new Row(label, $"{sw.Elapsed.TotalMicroseconds / Reps:F1} us", $"{alloc:N0} B / op");
}

record Row(string Variant, string Mean, string Allocated);

// ---- ORIGINAL: a column-major 4x4 as a class with float[16] (allocates on every operation) ----
class OldMat
{
    public readonly float[] M = new float[16];          // <-- heap array, allocated per matrix
    public OldMat() { M[0] = M[5] = M[10] = M[15] = 1f; }
    private float this[int c, int r] { get => M[c * 4 + r]; set => M[c * 4 + r] = value; }

    public static OldMat operator *(OldMat a, OldMat b)  // scalar, allocates a new OldMat
    {
        var o = new OldMat();
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                o[c, r] = a[0, r] * b[c, 0] + a[1, r] * b[c, 1] + a[2, r] * b[c, 2] + a[3, r] * b[c, 3];
        return o;
    }
    public static OldMat RotY(float a) { var m = new OldMat(); m[0, 0] = MathF.Cos(a); m[0, 2] = MathF.Sin(a); m[2, 0] = -MathF.Sin(a); m[2, 2] = MathF.Cos(a); return m; }
    public static OldMat RotX(float a) { var m = new OldMat(); m[1, 1] = MathF.Cos(a); m[1, 2] = MathF.Sin(a); m[2, 1] = -MathF.Sin(a); m[2, 2] = MathF.Cos(a); return m; }
    public static OldMat Trans(float x, float y, float z) { var m = new OldMat(); m[3, 0] = x; m[3, 1] = y; m[3, 2] = z; return m; }
}
