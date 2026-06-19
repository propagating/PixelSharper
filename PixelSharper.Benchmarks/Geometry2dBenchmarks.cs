using System.Numerics;
using BenchmarkDotNet.Attributes;
using PixelSharper.Core.Types;

namespace PixelSharper.Benchmarks;

// Geom2D relations run their math in double and convert the generic T components in. The old code used
// Convert.ToDouble(T), which resolves to Convert.ToDouble(object) for a generic T and therefore BOXES
// every component. double.CreateChecked<T>(T) does the same widening via generic math with no boxing.
// [MemoryDiagnoser] makes the difference obvious: the boxing path allocates gen0 garbage, the new one is 0.
[MemoryDiagnoser]
[MinColumn, MaxColumn]
public class Geometry2dBenchmarks
{
    private const int N = 10_000;
    private Vector2d<float>[] _pts = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(1);
        _pts = new Vector2d<float>[N];
        for (var i = 0; i < N; i++)
            _pts[i] = new Vector2d<float>((float)(rnd.NextDouble() * 100), (float)(rnd.NextDouble() * 100));
    }

    [Benchmark(Baseline = true)] // OLD: Convert.ToDouble(T) -> Convert.ToDouble(object) -> boxes T
    public double Convert_Boxing() => SumConvert(_pts);

    [Benchmark] // NEW: double.CreateChecked(T) via generic math, no boxing
    public double CreateChecked_NoBox() => SumCreate(_pts);

    private static double SumConvert<T>(Vector2d<T>[] a) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var s = 0.0;
        foreach (var v in a) s += Convert.ToDouble(v.X) - Convert.ToDouble(v.Y);
        return s;
    }

    private static double SumCreate<T>(Vector2d<T>[] a) where T : struct, INumber<T>, IEquatable<T>, IComparable<T>
    {
        var s = 0.0;
        foreach (var v in a) s += double.CreateChecked(v.X) - double.CreateChecked(v.Y);
        return s;
    }
}
