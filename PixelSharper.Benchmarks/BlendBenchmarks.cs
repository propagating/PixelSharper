using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using PixelSharper.Core.Components;

namespace PixelSharper.Benchmarks;

// Constant-source alpha blend over a destination row — the FillRect Alpha-mode kernel
// (PixelGameEngine.BlendRowConstant), which is scalar today despite a comment claiming a "SIMD path".
// Scalar baseline vs portable Vector128 / Vector256 (the JIT lowers these to SSE/AVX on x64, NEON on ARM).
//
// GlobalSetup gates every SIMD variant on BIT-EXACT equality with the scalar reference before any timing
// runs — a faster-but-wrong kernel must fail loudly, not post a misleading number.
[MemoryDiagnoser]
[MinColumn, MaxColumn, MedianColumn]
[RankColumn]
public class BlendBenchmarks
{
    // One screen row (256), a UI band (4096), a full 256x240 screen (61440).
    [Params(256, 4096, 61440)]
    public int N;

    private Pixel[] _buf = null!;
    private static readonly Pixel Src = new(200, 50, 10, 128); // translucent source
    private const float Blend = 1.0f;

    [GlobalSetup]
    public void Setup()
    {
        _buf = new Pixel[N];
        var rnd = new Random(1);
        for (var i = 0; i < N; i++)
            _buf[i] = new Pixel((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));

        // Correctness gate: each SIMD variant must reproduce the scalar reference bit-for-bit.
        var reference = (Pixel[])_buf.Clone();
        Scalar(reference, Src, Blend);
        Check("Vector128", b => Simd128(b, Src, Blend), reference);
        Check("Vector256", b => Simd256(b, Src, Blend), reference);
        Check("Vector512", b => Simd512(b, Src, Blend), reference);
    }

    private void Check(string name, Action<Pixel[]> blend, Pixel[] reference)
    {
        var test = (Pixel[])_buf.Clone();
        blend(test);
        for (var i = 0; i < N; i++)
            if (test[i].N != reference[i].N)
                throw new InvalidOperationException(
                    $"{name} not bit-exact at pixel {i}: got 0x{test[i].N:X8}, expected 0x{reference[i].N:X8}");
    }

    [Benchmark(Baseline = true)] public uint Scalar_()  { Scalar(_buf, Src, Blend);  return _buf[0].N; }
    [Benchmark] public uint Simd128_() { Simd128(_buf, Src, Blend); return _buf[0].N; }
    [Benchmark] public uint Simd256_() { Simd256(_buf, Src, Blend); return _buf[0].N; }
    [Benchmark] public uint Simd512_() { Simd512(_buf, Src, Blend); return _buf[0].N; }

    // ---- Scalar reference: exact copy of PixelGameEngine.BlendRowConstant ----
    private static void Scalar(Span<Pixel> row, Pixel src, float blend)
    {
        var a = src.Alpha / 255.0f * blend;
        var c = 1.0f - a;
        float kr = a * src.Red, kg = a * src.Green, kb = a * src.Blue;
        for (var i = 0; i < row.Length; i++)
        {
            var d = row[i];
            row[i] = new Pixel((byte)(c * d.Red + kr), (byte)(c * d.Green + kg), (byte)(c * d.Blue + kb));
        }
    }

    // ---- Vector128: 4 pixels (16 bytes) per iteration. out = c*dst + k per RGB; alpha forced to 255. ----
    private static void Simd128(Span<Pixel> row, Pixel src, float blend)
    {
        var a = src.Alpha / 255.0f * blend;
        var c = 1.0f - a;
        float kr = a * src.Red, kg = a * src.Green, kb = a * src.Blue;

        if (Vector128.IsHardwareAccelerated && row.Length >= 4)
        {
            var cv = Vector128.Create(c);
            var kv = Vector128.Create(kr, kg, kb, 0f);
            var alpha = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var bytes = MemoryMarshal.AsBytes(row);
            int i = 0, limit = bytes.Length - (bytes.Length & 15);
            for (; i < limit; i += 16)
            {
                var b = Vector128.Create(bytes.Slice(i, 16));
                var lo = Vector128.WidenLower(b);   // ushort x8 (px0,px1)
                var hi = Vector128.WidenUpper(b);   // ushort x8 (px2,px3)
                var f0 = Vector128.ConvertToSingle(Vector128.WidenLower(lo).AsInt32());
                var f1 = Vector128.ConvertToSingle(Vector128.WidenUpper(lo).AsInt32());
                var f2 = Vector128.ConvertToSingle(Vector128.WidenLower(hi).AsInt32());
                var f3 = Vector128.ConvertToSingle(Vector128.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector128.Narrow(Vector128.ConvertToInt32(f0).AsUInt32(), Vector128.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector128.Narrow(Vector128.ConvertToInt32(f2).AsUInt32(), Vector128.ConvertToInt32(f3).AsUInt32());
                var res = Vector128.Narrow(u0, u1) | alpha;
                res.CopyTo(bytes.Slice(i, 16));
            }
            for (int px = i / 4; px < row.Length; px++)
            {
                var d = row[px];
                row[px] = new Pixel((byte)(c * d.Red + kr), (byte)(c * d.Green + kg), (byte)(c * d.Blue + kb));
            }
            return;
        }
        Scalar(row, src, blend);
    }

    // ---- Vector256: 8 pixels (32 bytes) per iteration. ----
    private static void Simd256(Span<Pixel> row, Pixel src, float blend)
    {
        var a = src.Alpha / 255.0f * blend;
        var c = 1.0f - a;
        float kr = a * src.Red, kg = a * src.Green, kb = a * src.Blue;

        if (Vector256.IsHardwareAccelerated && row.Length >= 8)
        {
            var cv = Vector256.Create(c);
            var kv128 = Vector128.Create(kr, kg, kb, 0f);
            var kv = Vector256.Create(kv128, kv128);
            var alpha128 = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var alpha = Vector256.Create(alpha128, alpha128);
            var bytes = MemoryMarshal.AsBytes(row);
            int i = 0, limit = bytes.Length - (bytes.Length & 31);
            for (; i < limit; i += 32)
            {
                var b = Vector256.Create(bytes.Slice(i, 32));
                var lo = Vector256.WidenLower(b);   // ushort x16
                var hi = Vector256.WidenUpper(b);
                var f0 = Vector256.ConvertToSingle(Vector256.WidenLower(lo).AsInt32());
                var f1 = Vector256.ConvertToSingle(Vector256.WidenUpper(lo).AsInt32());
                var f2 = Vector256.ConvertToSingle(Vector256.WidenLower(hi).AsInt32());
                var f3 = Vector256.ConvertToSingle(Vector256.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector256.Narrow(Vector256.ConvertToInt32(f0).AsUInt32(), Vector256.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector256.Narrow(Vector256.ConvertToInt32(f2).AsUInt32(), Vector256.ConvertToInt32(f3).AsUInt32());
                var res = Vector256.Narrow(u0, u1) | alpha;
                res.CopyTo(bytes.Slice(i, 32));
            }
            for (int px = i / 4; px < row.Length; px++)
            {
                var d = row[px];
                row[px] = new Pixel((byte)(c * d.Red + kr), (byte)(c * d.Green + kg), (byte)(c * d.Blue + kb));
            }
            return;
        }
        Simd128(row, src, blend);
    }

    // ---- Vector512: 16 pixels (64 bytes) per iteration (AVX-512 / Zen4). Composes 256-bit halves so it
    // only uses Create overloads that definitely exist. Falls back to Simd256 when not accelerated. ----
    private static void Simd512(Span<Pixel> row, Pixel src, float blend)
    {
        var a = src.Alpha / 255.0f * blend;
        var c = 1.0f - a;
        float kr = a * src.Red, kg = a * src.Green, kb = a * src.Blue;

        if (Vector512.IsHardwareAccelerated && row.Length >= 16)
        {
            var cv = Vector512.Create(c);
            var kv128 = Vector128.Create(kr, kg, kb, 0f);
            var kv = Vector512.Create(Vector256.Create(kv128, kv128), Vector256.Create(kv128, kv128));
            var alpha128 = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var alpha256 = Vector256.Create(alpha128, alpha128);
            var alpha = Vector512.Create(alpha256, alpha256);
            var bytes = MemoryMarshal.AsBytes(row);
            int i = 0, limit = bytes.Length - (bytes.Length & 63);
            for (; i < limit; i += 64)
            {
                var b = Vector512.Create(bytes.Slice(i, 64));
                var lo = Vector512.WidenLower(b);   // ushort x32
                var hi = Vector512.WidenUpper(b);
                var f0 = Vector512.ConvertToSingle(Vector512.WidenLower(lo).AsInt32());
                var f1 = Vector512.ConvertToSingle(Vector512.WidenUpper(lo).AsInt32());
                var f2 = Vector512.ConvertToSingle(Vector512.WidenLower(hi).AsInt32());
                var f3 = Vector512.ConvertToSingle(Vector512.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector512.Narrow(Vector512.ConvertToInt32(f0).AsUInt32(), Vector512.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector512.Narrow(Vector512.ConvertToInt32(f2).AsUInt32(), Vector512.ConvertToInt32(f3).AsUInt32());
                var res = Vector512.Narrow(u0, u1) | alpha;
                res.CopyTo(bytes.Slice(i, 64));
            }
            for (int px = i / 4; px < row.Length; px++)
            {
                var d = row[px];
                row[px] = new Pixel((byte)(c * d.Red + kr), (byte)(c * d.Green + kg), (byte)(c * d.Blue + kb));
            }
            return;
        }
        Simd256(row, src, blend);
    }
}
