using BenchmarkDotNet.Attributes;
using Our = PixelSharper.Core.Types.Matrix4x4;
using Sn = System.Numerics.Matrix4x4;

namespace PixelSharper.Benchmarks;

// Head-to-head: our Stage-1 struct matrix (scalar field math) vs System.Numerics.Matrix4x4 (SIMD-
// accelerated by the JIT). This measures the *potential* SIMD win before we take on the column->row
// major mapping risk of actually backing our type with System.Numerics. Each op composes proj*view*model
// 1000x (a stand-in for a frame's worth of matrix work). [MemoryDiagnoser] also confirms the Stage-1
// struct allocates nothing per multiply.
[MemoryDiagnoser]
[MinColumn, MaxColumn, MedianColumn]
public class Matrix4x4Benchmarks
{
    private const int N = 1000;
    private Our _a, _b, _c;
    private Sn _sa, _sb, _sc;

    [GlobalSetup]
    public void Setup()
    {
        _a = Our.RotateY(0.5f); _b = Our.RotateX(0.3f); _c = Our.Translation(1, 2, 3);
        _sa = Sn.CreateRotationY(0.5f); _sb = Sn.CreateRotationX(0.3f); _sc = Sn.CreateTranslation(1, 2, 3);
    }

    [Benchmark(Baseline = true)]
    public Our OurMultiply()                 // our scalar struct (Stage 1)
    {
        var r = new Our();
        for (var i = 0; i < N; i++) r = _a * _b * _c;
        return r;
    }

    [Benchmark]
    public Sn SystemNumericsMultiply()       // SIMD reference
    {
        var r = default(Sn);
        for (var i = 0; i < N; i++) r = _sa * _sb * _sc;
        return r;
    }
}
