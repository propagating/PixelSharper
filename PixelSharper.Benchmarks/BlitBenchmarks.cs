using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using PixelSharper.Core.Components;

namespace PixelSharper.Benchmarks;

// Variable-source alpha blit ("source-over"): blend a SOURCE row (each pixel carries its own alpha) over a
// destination row. This is the kernel behind DrawSprite in Alpha mode, which today goes per-pixel through
// Draw(). Harder than the constant-source case (BlendRowConstant) because the alpha factor varies per pixel
// and must be broadcast across each pixel's R/G/B lanes.
//
// Bit-exact with Draw()'s Alpha case:  a = s.A/255*blend; c = 1-a; out.rgb = a*s.rgb + c*d.rgb; out.a = 255.
// GlobalSetup gates every SIMD variant on byte-for-byte equality with the scalar reference before timing.
[MemoryDiagnoser]
[MinColumn, MaxColumn, MedianColumn]
[RankColumn]
public class BlitBenchmarks
{
    [Params(256, 4096, 61440)]
    public int N;

    private Pixel[] _dst = null!;
    private Pixel[] _src = null!;
    private const float Blend = 1.0f;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(1);
        _dst = new Pixel[N];
        _src = new Pixel[N];
        for (var i = 0; i < N; i++)
        {
            _dst[i] = new Pixel((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
            _src[i] = new Pixel((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)); // full alpha range
        }

        var reference = (Pixel[])_dst.Clone(); ScalarOver(reference, _src, Blend);
        Check("Vector128", d => Simd128Over(d, _src, Blend), reference);
        Check("Vector256", d => Simd256Over(d, _src, Blend), reference);
    }

    private void Check(string name, Action<Pixel[]> blit, Pixel[] reference)
    {
        var test = (Pixel[])_dst.Clone();
        blit(test);
        for (var i = 0; i < N; i++)
            if (test[i].N != reference[i].N)
                throw new InvalidOperationException($"{name} not bit-exact at {i}: got 0x{test[i].N:X8}, expected 0x{reference[i].N:X8}");
    }

    [Benchmark(Baseline = true)] public uint Scalar_()  { ScalarOver(_dst, _src, Blend);  return _dst[0].N; }
    [Benchmark] public uint Simd128_() { Simd128Over(_dst, _src, Blend); return _dst[0].N; }
    [Benchmark] public uint Simd256_() { Simd256Over(_dst, _src, Blend); return _dst[0].N; }

    // ---- Scalar reference: exactly Draw()'s Alpha math, over a span ----
    private static void ScalarOver(Span<Pixel> dst, ReadOnlySpan<Pixel> src, float blend)
    {
        for (var i = 0; i < dst.Length; i++)
        {
            var s = src[i]; var d = dst[i];
            var a = s.Alpha / 255.0f * blend; var c = 1.0f - a;
            dst[i] = new Pixel((byte)(a * s.Red + c * d.Red), (byte)(a * s.Green + c * d.Green), (byte)(a * s.Blue + c * d.Blue));
        }
    }

    // ---- Vector128: 4 pixels (16 bytes) per iteration; alpha broadcast within each pixel via Shuffle ----
    private static void Simd128Over(Span<Pixel> dst, ReadOnlySpan<Pixel> src, float blend)
    {
        if (Vector128.IsHardwareAccelerated && dst.Length >= 4)
        {
            var v255 = Vector128.Create(255f);
            var vBlend = Vector128.Create(blend);
            var v1 = Vector128.Create(1f);
            var shuf = Vector128.Create(3, 3, 3, 3); // broadcast lane 3 (alpha) of each 1-pixel float vector
            var alphaMask = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var db = MemoryMarshal.AsBytes(dst);
            var sb = MemoryMarshal.AsBytes(src);
            int i = 0, limit = db.Length - (db.Length & 15);

            Vector128<float> Over(Vector128<float> s, Vector128<float> d)
            {
                var a = Vector128.Shuffle(s, shuf) / v255 * vBlend;
                return a * s + (v1 - a) * d;
            }

            for (; i < limit; i += 16)
            {
                var dlo = Vector128.WidenLower(Vector128.Create<byte>(db.Slice(i, 16)));
                var dhi = Vector128.WidenUpper(Vector128.Create<byte>(db.Slice(i, 16)));
                var slo = Vector128.WidenLower(Vector128.Create<byte>(sb.Slice(i, 16)));
                var shi = Vector128.WidenUpper(Vector128.Create<byte>(sb.Slice(i, 16)));
                var o0 = Over(Vector128.ConvertToSingle(Vector128.WidenLower(slo).AsInt32()), Vector128.ConvertToSingle(Vector128.WidenLower(dlo).AsInt32()));
                var o1 = Over(Vector128.ConvertToSingle(Vector128.WidenUpper(slo).AsInt32()), Vector128.ConvertToSingle(Vector128.WidenUpper(dlo).AsInt32()));
                var o2 = Over(Vector128.ConvertToSingle(Vector128.WidenLower(shi).AsInt32()), Vector128.ConvertToSingle(Vector128.WidenLower(dhi).AsInt32()));
                var o3 = Over(Vector128.ConvertToSingle(Vector128.WidenUpper(shi).AsInt32()), Vector128.ConvertToSingle(Vector128.WidenUpper(dhi).AsInt32()));
                var u0 = Vector128.Narrow(Vector128.ConvertToInt32(o0).AsUInt32(), Vector128.ConvertToInt32(o1).AsUInt32());
                var u1 = Vector128.Narrow(Vector128.ConvertToInt32(o2).AsUInt32(), Vector128.ConvertToInt32(o3).AsUInt32());
                (Vector128.Narrow(u0, u1) | alphaMask).CopyTo(db.Slice(i, 16));
            }
            for (var px = i / 4; px < dst.Length; px++)
            {
                var s = src[px]; var d = dst[px];
                var a = s.Alpha / 255.0f * blend; var c = 1.0f - a;
                dst[px] = new Pixel((byte)(a * s.Red + c * d.Red), (byte)(a * s.Green + c * d.Green), (byte)(a * s.Blue + c * d.Blue));
            }
            return;
        }
        ScalarOver(dst, src, blend);
    }

    // ---- Vector256: 8 pixels (32 bytes) per iteration; each float vector holds 2 pixels ----
    private static void Simd256Over(Span<Pixel> dst, ReadOnlySpan<Pixel> src, float blend)
    {
        if (Vector256.IsHardwareAccelerated && dst.Length >= 8)
        {
            var v255 = Vector256.Create(255f);
            var vBlend = Vector256.Create(blend);
            var v1 = Vector256.Create(1f);
            var shuf = Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7); // alpha of pixel0 and pixel1 within each 8-lane vector
            var alpha128 = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var alphaMask = Vector256.Create(alpha128, alpha128);
            var db = MemoryMarshal.AsBytes(dst);
            var sb = MemoryMarshal.AsBytes(src);
            int i = 0, limit = db.Length - (db.Length & 31);

            Vector256<float> Over(Vector256<float> s, Vector256<float> d)
            {
                var a = Vector256.Shuffle(s, shuf) / v255 * vBlend;
                return a * s + (v1 - a) * d;
            }

            for (; i < limit; i += 32)
            {
                var dvec = Vector256.Create<byte>(db.Slice(i, 32));
                var svec = Vector256.Create<byte>(sb.Slice(i, 32));
                var dlo = Vector256.WidenLower(dvec); var dhi = Vector256.WidenUpper(dvec);
                var slo = Vector256.WidenLower(svec); var shi = Vector256.WidenUpper(svec);
                var o0 = Over(Vector256.ConvertToSingle(Vector256.WidenLower(slo).AsInt32()), Vector256.ConvertToSingle(Vector256.WidenLower(dlo).AsInt32()));
                var o1 = Over(Vector256.ConvertToSingle(Vector256.WidenUpper(slo).AsInt32()), Vector256.ConvertToSingle(Vector256.WidenUpper(dlo).AsInt32()));
                var o2 = Over(Vector256.ConvertToSingle(Vector256.WidenLower(shi).AsInt32()), Vector256.ConvertToSingle(Vector256.WidenLower(dhi).AsInt32()));
                var o3 = Over(Vector256.ConvertToSingle(Vector256.WidenUpper(shi).AsInt32()), Vector256.ConvertToSingle(Vector256.WidenUpper(dhi).AsInt32()));
                var u0 = Vector256.Narrow(Vector256.ConvertToInt32(o0).AsUInt32(), Vector256.ConvertToInt32(o1).AsUInt32());
                var u1 = Vector256.Narrow(Vector256.ConvertToInt32(o2).AsUInt32(), Vector256.ConvertToInt32(o3).AsUInt32());
                (Vector256.Narrow(u0, u1) | alphaMask).CopyTo(db.Slice(i, 32));
            }
            for (var px = i / 4; px < dst.Length; px++)
            {
                var s = src[px]; var d = dst[px];
                var a = s.Alpha / 255.0f * blend; var c = 1.0f - a;
                dst[px] = new Pixel((byte)(a * s.Red + c * d.Red), (byte)(a * s.Green + c * d.Green), (byte)(a * s.Blue + c * d.Blue));
            }
            return;
        }
        Simd128Over(dst, src, blend);
    }
}
