using BenchmarkDotNet.Attributes;
using PixelSharper.Core.Types;

namespace PixelSharper.Benchmarks;

// Measures the hot Vector2d<T> math. `Add` is the control: a pure operator path. The Magnitude*/FromPolar*
// rows compare the NEW in-T floating fast paths (IRootFunctions<float>.Sqrt / ITrigonometricFunctions<float>.Sin
// + Cos, specialized via JIT-folded typeof(T) tests) against the OLD T->double->T round-trip replicated inline,
// plus the overflow-safe Hypot variant. [MemoryDiagnoser] confirms none of these allocate (all should be ~0).
[MemoryDiagnoser]        // Allocated bytes + Gen0/Gen1/Gen2 collections
[ThreadingDiagnoser]     // Lock contentions + completed work items
[MinColumn, MaxColumn, MeanColumn, MedianColumn]  // full timing spread (Error/StdDev shown by default)
[RankColumn]
public class Vector2dBenchmarks
{
    private const int N = 10_000;
    private Vector2d<float>[] _a = null!;
    private Vector2d<float>[] _b = null!;
    private Vector2d<float> _lo, _hi;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(1);
        _a = new Vector2d<float>[N];
        _b = new Vector2d<float>[N];
        for (var i = 0; i < N; i++)
        {
            _a[i] = new Vector2d<float>((float)(rnd.NextDouble() * 100), (float)(rnd.NextDouble() * 100));
            _b[i] = new Vector2d<float>((float)(rnd.NextDouble() * 100), (float)(rnd.NextDouble() * 100));
        }
        _lo = new Vector2d<float>(10, 10);
        _hi = new Vector2d<float>(90, 90);
    }

    [Benchmark(Baseline = true)] // control: operator path, no boxing
    public float Add()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += (_a[i] + _b[i]).X;
        return s;
    }

    [Benchmark] // NEW fast path: IRootFunctions<float>.Sqrt, stays in float (no double round-trip)
    public float Magnitude()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += _a[i].Magnitude();
        return s;
    }

    [Benchmark] // OLD double-round-trip path, replicated inline for an apples-to-apples delta
    public float MagnitudeDoublePath()
    {
        var s = 0f;
        for (var i = 0; i < N; i++)
        {
            double x = _a[i].X, y = _a[i].Y;
            s += (float)Math.Sqrt(x * x + y * y);
        }
        return s;
    }

    [Benchmark] // Overflow-safe IRootFunctions<float>.Hypot path (robustness vs speed tradeoff)
    public float MagnitudeHypot()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += _a[i].MagnitudeRobust();
        return s;
    }

    [Benchmark] // NEW fast path: ITrigonometricFunctions<float>.Sin/Cos, stays in float
    public float FromPolar()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += Vector2d<float>.FromPolar(_a[i].X, _b[i].Y).X;
        return s;
    }

    [Benchmark] // OLD double-round-trip FromPolar, replicated inline
    public float FromPolarDoublePath()
    {
        var s = 0f;
        for (var i = 0; i < N; i++)
        {
            double r = _a[i].X, t = _b[i].Y;
            s += (float)(r * Math.Cos(t));
        }
        return s;
    }

    // --- ToPolar candidates: gauges the IFloatingPointIeee754<float>.Atan2 win BEFORE wiring it into the engine ---

    [Benchmark] // CANDIDATE: in-float ToPolar via IFloatingPointIeee754<float>.Atan2 + IRootFunctions<float>.Sqrt
    public float ToPolarFloatAtan2()
    {
        var s = 0f;
        for (var i = 0; i < N; i++)
        {
            float x = _a[i].X, y = _a[i].Y;
            s += float.Atan2(y, x) + float.Sqrt(x * x + y * y);
        }
        return s;
    }

    [Benchmark] // BASELINE: ToPolar via the double round-trip (Math.Atan2 / Math.Sqrt), replicated inline
    public float ToPolarDoublePath()
    {
        var s = 0f;
        for (var i = 0; i < N; i++)
        {
            double x = _a[i].X, y = _a[i].Y;
            s += (float)Math.Atan2(y, x) + (float)Math.Sqrt(x * x + y * y);
        }
        return s;
    }

    [Benchmark] // Distance: Convert.ToDouble round-trip
    public double Distance()
    {
        var s = 0.0;
        for (var i = 0; i < N; i++) s += Vector2d<float>.Distance(_a[i], _b[i]);
        return s;
    }

    [Benchmark] // Min: Convert.ToDouble + Math.Min, x2 components
    public float Min()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += Vector2d<float>.Min(_a[i], _b[i]).X;
        return s;
    }

    [Benchmark] // Clamp: 6x Convert.ToDouble per call
    public float Clamp()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += _a[i].Clamp(_lo, _hi).X;
        return s;
    }
}
