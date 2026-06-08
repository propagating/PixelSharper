using BenchmarkDotNet.Attributes;
using PixelSharper.Core.Types;

namespace PixelSharper.Benchmarks;

// Measures the hot Vector2d<T> math. [MemoryDiagnoser] reports allocated bytes — the boxing in the
// double-path helpers (Convert.ToDouble / Convert.ChangeType over a generic T) shows up here as gen0
// garbage. `Add` is the control: an operator path that never boxes, so its Allocated should be ~0.
// After the boxing fix, the boxing rows should fall to ~0 allocated and run faster.
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

    [Benchmark] // MagnitudeAsDouble: 4x Convert.ToDouble per call
    public float Magnitude()
    {
        var s = 0f;
        for (var i = 0; i < N; i++) s += _a[i].Magnitude();
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
