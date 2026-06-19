<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Runtime.Intrinsics</Namespace>
</Query>

// =====================================================================================================
// THE PROGRESSION: basic generics -> unsafe -> intrinsics. Run in LINQPad 8 (.NET 10).
//
// One operation, four implementations: the magnitude sqrt(x^2 + y^2) of N 2D points, written to an output
// array. Magnitude RETURNS the component type, so the generic versions pay a T -> double -> T round-trip --
// and that round-trip is exactly where the unsafe specialization earns its keep. This is the arc the
// engine's Vector2d<T> walked, ladder rung by ladder rung.
//
//   Stage 1a  BASIC GENERICS, naive   — Convert.ToDouble(T). Correct & portable, but on a generic T it
//                                       binds to Convert.ToDouble(object) and BOXES every component.
//   Stage 1b  BASIC GENERICS, clean   — double.CreateChecked(T). Same generic-math idea, NO boxing.
//   Stage 2   UNSAFE                   — typeof(T)==float (the JIT folds it to a constant per instantiation,
//                                       so the branch is free) + Unsafe.BitCast to reinterpret the component
//                                       as a real float and call native float.Sqrt -- no conversion at all.
//   Stage 3   INTRINSICS               — Vector256.Sqrt over separate x/y streams, 8 magnitudes per step.
//
// WHAT THE NUMBERS TEACH (measure, don't guess):
//   * 1a -> 1b is mostly an ALLOCATION win: killing the boxing removes ~12 MB of gen0 garbage per call.
//     It is NOT automatically faster -- generic-math CreateChecked isn't free -- which is the whole point.
//   * 1b -> 2 is the big SPEED rung: staying in native float skips the round-trip entirely (~5-6x).
//   * 2 -> 3 is the bulk rung: SIMD does 8 lanes at once (~7x over unsafe).
// Reach DOWN the ladder only as far as the measurement justifies. Each rung also costs you something
// (portability, then safety, then readability) -- so spend it where the number says it's worth it.
//
// Indicative on an AMD Ryzen Threadripper 7970X, .NET 10, N=262,144 (BenchmarkDotNet; LINQPad timings are
// noisier -- run it for YOUR machine):
//   1a generic/Convert  ~1400 us, 12.6 MB     1b generic/CreateChecked  ~1780 us, 0 B
//   2  unsafe/BitCast    ~286 us, 0 B          3  intrinsics/Vector256    ~42 us, 0 B
// =====================================================================================================

struct V2<T> where T : struct, INumber<T> { public T X, Y; public V2(T x, T y) { X = x; Y = y; } }

const int N = 1 << 18; // 262,144 points

void Main()
{
    var rnd = new Random(1);
    var pts = new V2<float>[N];
    float[] xs = new float[N], ys = new float[N];
    for (var i = 0; i < N; i++)
    {
        float x = (float)(rnd.NextDouble() * 200 - 100), y = (float)(rnd.NextDouble() * 200 - 100);
        pts[i] = new V2<float>(x, y); xs[i] = x; ys[i] = y;
    }

    // Correctness: all four agree to float tolerance (generic uses double sqrt, the rest float sqrt).
    float[] a = new float[N], b = new float[N], c = new float[N], d = new float[N];
    MagConvert(pts, a); MagCreate(pts, b); MagUnsafe(pts, c); MagIntrinsics(xs, ys, d);
    var ok = Close(a, b) && Close(a, c) && Close(a, d);
    $"all stages agree (float tolerance): {ok}".Dump();

    var rows = new List<Row>
    {
        Measure("1a  generic  — Convert.ToDouble (boxes)", o => MagConvert(pts, o)),
        Measure("1b  generic  — double.CreateChecked",     o => MagCreate(pts, o)),
        Measure("2   unsafe   — Unsafe.BitCast (float)",   o => MagUnsafe(pts, o)),
        Measure("3   intrinsics — Vector256.Sqrt",         o => MagIntrinsics(xs, ys, o)),
    };
    var basis = rows[0].MeanUs;
    rows.Select(r => new
    {
        Stage = r.Stage,
        Mean_us = r.MeanUs.ToString("F1"),
        vs_1a = (basis / r.MeanUs).ToString("F1") + "x",
        Alloc_KB = (r.Alloc / 1024.0).ToString("F0"),
    }).Dump($"basic generics -> unsafe -> intrinsics: magnitude of {N:N0} points");
    $"Vector256 hardware-accelerated: {Vector256.IsHardwareAccelerated}".Dump();
}

// 1a — basic generics, naive: Convert.ToDouble(T) boxes; result narrowed back via T.CreateChecked.
static void MagConvert<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
{
    for (var i = 0; i < p.Length; i++)
    {
        double x = Convert.ToDouble(p[i].X), y = Convert.ToDouble(p[i].Y);
        o[i] = T.CreateChecked(Math.Sqrt(x * x + y * y));
    }
}

// 1b — basic generics, clean: double.CreateChecked(T), no boxing, but still T->double->T per element.
static void MagCreate<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
{
    for (var i = 0; i < p.Length; i++)
    {
        double x = double.CreateChecked(p[i].X), y = double.CreateChecked(p[i].Y);
        o[i] = T.CreateChecked(Math.Sqrt(x * x + y * y));
    }
}

// 2 — unsafe: JIT-folded typeof(T) + Unsafe.BitCast -> native float.Sqrt, no conversion either way.
static void MagUnsafe<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
{
    if (typeof(T) == typeof(float))
    {
        for (var i = 0; i < p.Length; i++)
        {
            float x = Unsafe.BitCast<T, float>(p[i].X), y = Unsafe.BitCast<T, float>(p[i].Y);
            o[i] = Unsafe.BitCast<float, T>(float.Sqrt(x * x + y * y));
        }
        return;
    }
    MagCreate(p, o);
}

// 3 — intrinsics: SoA x/y streams, Vector256.Sqrt does 8 magnitudes at once.
static void MagIntrinsics(float[] xs, float[] ys, float[] o)
{
    if (Vector256.IsHardwareAccelerated && xs.Length >= 8)
    {
        int i = 0, lim = xs.Length - (xs.Length & 7);
        ReadOnlySpan<float> sx = xs, sy = ys;
        Span<float> so = o;
        for (; i < lim; i += 8)
        {
            var vx = Vector256.Create(sx.Slice(i, 8));
            var vy = Vector256.Create(sy.Slice(i, 8));
            Vector256.Sqrt(vx * vx + vy * vy).CopyTo(so.Slice(i, 8));
        }
        for (; i < xs.Length; i++) o[i] = float.Sqrt(xs[i] * xs[i] + ys[i] * ys[i]);
        return;
    }
    for (var i = 0; i < xs.Length; i++) o[i] = float.Sqrt(xs[i] * xs[i] + ys[i] * ys[i]);
}

static bool Close(float[] a, float[] b)
{
    for (var i = 0; i < a.Length; i++)
        if (Math.Abs(a[i] - b[i]) > 1e-3f * Math.Max(1f, Math.Abs(a[i]))) return false;
    return true;
}

Row Measure(string stage, Action<float[]> body)
{
    var o = new float[N];
    body(o); // warm up / JIT
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    const int reps = 30;
    var allocBefore = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < reps; r++) body(o);
    sw.Stop();
    var alloc = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / reps;
    return new Row(stage, sw.Elapsed.TotalMicroseconds / reps, alloc);
}

record Row(string Stage, double MeanUs, long Alloc);
