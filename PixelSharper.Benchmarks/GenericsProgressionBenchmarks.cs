using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;

namespace PixelSharper.Benchmarks;

// The progression a generic numeric type walks for performance: BASIC GENERICS -> UNSAFE -> INTRINSICS,
// on one operation -- the magnitude sqrt(x^2 + y^2) of N 2D points, written to an output array. Magnitude
// RETURNS the component type, so the generic versions pay a T->double->T round-trip; that is exactly where
// the unsafe BitCast specialization earns its keep, and it's the arc PixelSharper's Vector2d<T> took.
// [MemoryDiagnoser] also exposes the boxing in stage 1a.
//
//   1a  generic, naive  — Convert.ToDouble(T): binds to Convert.ToDouble(object) for a generic T -> BOXES.
//   1b  generic, clean  — double.CreateChecked(T): same generic math, no boxing, but still T->double->T.
//   2   unsafe          — typeof(T)==float (JIT-folds) + Unsafe.BitCast: native float.Sqrt, no round-trip.
//   3   intrinsics      — Vector256.Sqrt over SoA x/y streams, 8 magnitudes per step.
[MemoryDiagnoser]
[MinColumn, MaxColumn]
public class GenericsProgressionBenchmarks
{
    private const int N = 1 << 18; // 262,144 points (a multiple of 8)
    private V2<float>[] _pts = null!;
    private float[] _xs = null!, _ys = null!, _out = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(1);
        _pts = new V2<float>[N];
        _xs = new float[N]; _ys = new float[N]; _out = new float[N];
        for (var i = 0; i < N; i++)
        {
            float x = (float)(rnd.NextDouble() * 200 - 100), y = (float)(rnd.NextDouble() * 200 - 100);
            _pts[i] = new V2<float>(x, y);
            _xs[i] = x; _ys[i] = y;
        }
    }

    [Benchmark(Baseline = true)] public void Generic_Convert_Boxing() => MagConvert(_pts, _out);
    [Benchmark] public void Generic_CreateChecked() => MagCreate(_pts, _out);
    [Benchmark] public void Unsafe_BitCast() => MagUnsafe(_pts, _out);
    [Benchmark] public void Intrinsics_Vector256() => MagIntrinsics(_xs, _ys, _out);

    // 1a — basic generics, naive: Convert.ToDouble(T) boxes a generic T; result narrowed back via CreateChecked.
    private static void MagConvert<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
    {
        for (var i = 0; i < p.Length; i++)
        {
            double x = Convert.ToDouble(p[i].X), y = Convert.ToDouble(p[i].Y);
            o[i] = T.CreateChecked(Math.Sqrt(x * x + y * y));
        }
    }

    // 1b — basic generics, clean: double.CreateChecked(T), no boxing, but still T->double->T per element.
    private static void MagCreate<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
    {
        for (var i = 0; i < p.Length; i++)
        {
            double x = double.CreateChecked(p[i].X), y = double.CreateChecked(p[i].Y);
            o[i] = T.CreateChecked(Math.Sqrt(x * x + y * y));
        }
    }

    // 2 — unsafe: JIT-folded typeof(T) test + Unsafe.BitCast -> native float.Sqrt, no conversion either way.
    private static void MagUnsafe<T>(V2<T>[] p, T[] o) where T : struct, INumber<T>
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
        MagCreate(p, o); // fallback for non-float/double T
    }

    // 3 — intrinsics: SoA x/y streams, Vector256.Sqrt does 8 magnitudes at once.
    private static void MagIntrinsics(float[] xs, float[] ys, float[] o)
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

    private struct V2<T> where T : struct, INumber<T>
    {
        public T X, Y;
        public V2(T x, T y) { X = x; Y = y; }
    }
}
