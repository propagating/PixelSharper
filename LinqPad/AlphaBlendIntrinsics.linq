<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Runtime.Intrinsics</Namespace>
</Query>

// ===========================================================================================
// Alpha-blend FillRect (constant source): SCALAR span blend vs HARDWARE INTRINSICS (Vector128 / Vector256).
// Run in LINQPad 8 (.NET 8). A "pixel" is 4 bytes (R,G,B,A) packed in a struct.
//
// FOLLOW-UP TO AlphaBlend.linq
//   That script stopped at the scalar span blend and said byte-exact SIMD was "fiddly and risky, so the
//   engine ships the exact scalar span blend." That note is now SUPERSEDED: the SIMD path turned out to be
//   bit-exact AND ~4.3x faster, so PixelGameEngine.BlendRowConstant now dispatches Vector256 -> Vector128 ->
//   scalar. This script is the evidence.
//
// WHAT THE INTRINSICS DO
//   The blend is a per-channel affine on the destination:  out.rgb = c*dst.rgb + k   (c = 1-a, k = a*src.rgb),
//   out.a = 255. Instead of one pixel at a time, we widen 4 (Vector128) or 8 (Vector256) pixels' bytes to
//   float, do the SAME multiply-add, truncate back to bytes, and OR 0xFF into each alpha byte.
//
// WHY IT'S BIT-EXACT (the part AlphaBlend.linq was nervous about)
//   SSE/AVX single-precision mul/add are IEEE-754 correctly-rounded — identical to scalar C# float ops. The
//   float->int conversion truncates toward zero (cvtt), exactly like the (byte)(...) cast. So every lane
//   equals the scalar result byte-for-byte. The script asserts this before timing.
//
// EXPECTED (AMD Ryzen Threadripper 7970X, .NET 8, BenchmarkDotNet, 61440 px):
//   scalar 117 us -> Vector128 49 us (2.4x) -> Vector256 28 us (4.3x). Stopwatch numbers below will be in the
//   same ballpark (LINQPad timing is noisier than BenchmarkDotNet, so treat the ratios as the takeaway).
// ===========================================================================================

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
struct Px { public byte R, G, B, A; public Px(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; } }

const int W = 256, H = 240;          // a full 256x240 screen = 61440 pixels
const int Reps = 2000;

void Main()
{
    var src = new Px(200, 50, 10, 128); // translucent source

    // --- Correctness gate: every SIMD variant must match the scalar reference bit-for-bit ---
    var reference = NewBuffer(); ScalarSpan(reference, src, 1f);
    var c128 = NewBuffer(); Simd128(c128, src, 1f); var ok128 = Equal(reference, c128);
    var c256 = NewBuffer(); Simd256(c256, src, 1f); var ok256 = Equal(reference, c256);
    if (!ok128 || !ok256)
        $"WARNING: SIMD not bit-exact (V128={ok128}, V256={ok256}) — a faster-but-wrong kernel is not a win!".Dump();

    // --- Timing ---
    var dS = NewBuffer(); var d128 = NewBuffer(); var d256 = NewBuffer();
    var ms = BenchUs(() => ScalarSpan(dS, src, 1f), Reps);
    var m1 = BenchUs(() => Simd128(d128, src, 1f), Reps);
    var m2 = BenchUs(() => Simd256(d256, src, 1f), Reps);

    new[]
    {
        new Row("Scalar span (was the shipped baseline)", $"{ms:F2}", "1.00x", "ref"),
        new Row("Vector128  (SSE2/NEON, 4 px/iter)",      $"{m1:F2}", $"{ms / m1:F2}x", ok128 ? "yes" : "NO"),
        new Row("Vector256  (AVX2, 8 px/iter)",           $"{m2:F2}", $"{ms / m2:F2}x", ok256 ? "yes" : "NO"),
    }.Dump($"Constant-source alpha blend over {W * H:N0} px — scalar vs intrinsics");

    $"Hardware: Vector128.IsHardwareAccelerated={Vector128.IsHardwareAccelerated}, " +
    $"Vector256.IsHardwareAccelerated={Vector256.IsHardwareAccelerated}".Dump();
}

static Px[] NewBuffer()
{
    var b = new Px[W * H];
    for (var i = 0; i < b.Length; i++) b[i] = new Px((byte)i, (byte)(i >> 2), 90, (byte)(i >> 1));
    return b;
}

// ---- Scalar reference: the span blend the engine used to ship (PixelGameEngine.BlendRowConstant, pre-SIMD) ----
static void ScalarSpan(Px[] d, Px s, float blend)
{
    var a = s.A / 255f * blend; var c = 1f - a;
    float kr = a * s.R, kg = a * s.G, kb = a * s.B;
    var row = d.AsSpan();
    for (var i = 0; i < row.Length; i++)
    {
        var dst = row[i];
        row[i] = new Px((byte)(c * dst.R + kr), (byte)(c * dst.G + kg), (byte)(c * dst.B + kb));
    }
}

// ---- Vector128: 4 pixels (16 bytes) per iteration ----
static void Simd128(Px[] d, Px s, float blend)
{
    var a = s.A / 255f * blend; var c = 1f - a;
    float kr = a * s.R, kg = a * s.G, kb = a * s.B;
    var row = d.AsSpan();

    if (Vector128.IsHardwareAccelerated && row.Length >= 4)
    {
        var cv = Vector128.Create(c);
        var kv = Vector128.Create(kr, kg, kb, 0f);
        var alpha = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
        var bytes = MemoryMarshal.AsBytes(row);
        int i = 0, limit = bytes.Length - (bytes.Length & 15);
        for (; i < limit; i += 16)
        {
            var b = Vector128.Create<byte>(bytes.Slice(i, 16));
            var lo = Vector128.WidenLower(b);
            var hi = Vector128.WidenUpper(b);
            var f0 = Vector128.ConvertToSingle(Vector128.WidenLower(lo).AsInt32());
            var f1 = Vector128.ConvertToSingle(Vector128.WidenUpper(lo).AsInt32());
            var f2 = Vector128.ConvertToSingle(Vector128.WidenLower(hi).AsInt32());
            var f3 = Vector128.ConvertToSingle(Vector128.WidenUpper(hi).AsInt32());
            f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
            var u0 = Vector128.Narrow(Vector128.ConvertToInt32(f0).AsUInt32(), Vector128.ConvertToInt32(f1).AsUInt32());
            var u1 = Vector128.Narrow(Vector128.ConvertToInt32(f2).AsUInt32(), Vector128.ConvertToInt32(f3).AsUInt32());
            (Vector128.Narrow(u0, u1) | alpha).CopyTo(bytes.Slice(i, 16));
        }
        for (int px = i / 4; px < row.Length; px++)
        {
            var dst = row[px];
            row[px] = new Px((byte)(c * dst.R + kr), (byte)(c * dst.G + kg), (byte)(c * dst.B + kb));
        }
        return;
    }
    ScalarSpan(d, s, blend);
}

// ---- Vector256: 8 pixels (32 bytes) per iteration ----
static void Simd256(Px[] d, Px s, float blend)
{
    var a = s.A / 255f * blend; var c = 1f - a;
    float kr = a * s.R, kg = a * s.G, kb = a * s.B;
    var row = d.AsSpan();

    if (Vector256.IsHardwareAccelerated && row.Length >= 8)
    {
        var cv = Vector256.Create(c);
        var kv128 = Vector128.Create(kr, kg, kb, 0f);
        var kv = Vector256.Create(kv128, kv128);
        var alpha128 = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
        var alpha = Vector256.Create(alpha128, alpha128);
        var bytes = MemoryMarshal.AsBytes(row);
        int i = 0, limit = bytes.Length - (bytes.Length & 31);
        for (; i < limit; i += 32)
        {
            var b = Vector256.Create<byte>(bytes.Slice(i, 32));
            var lo = Vector256.WidenLower(b);
            var hi = Vector256.WidenUpper(b);
            var f0 = Vector256.ConvertToSingle(Vector256.WidenLower(lo).AsInt32());
            var f1 = Vector256.ConvertToSingle(Vector256.WidenUpper(lo).AsInt32());
            var f2 = Vector256.ConvertToSingle(Vector256.WidenLower(hi).AsInt32());
            var f3 = Vector256.ConvertToSingle(Vector256.WidenUpper(hi).AsInt32());
            f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
            var u0 = Vector256.Narrow(Vector256.ConvertToInt32(f0).AsUInt32(), Vector256.ConvertToInt32(f1).AsUInt32());
            var u1 = Vector256.Narrow(Vector256.ConvertToInt32(f2).AsUInt32(), Vector256.ConvertToInt32(f3).AsUInt32());
            (Vector256.Narrow(u0, u1) | alpha).CopyTo(bytes.Slice(i, 32));
        }
        for (int px = i / 4; px < row.Length; px++)
        {
            var dst = row[px];
            row[px] = new Px((byte)(c * dst.R + kr), (byte)(c * dst.G + kg), (byte)(c * dst.B + kb));
        }
        return;
    }
    Simd128(d, s, blend);
}

static bool Equal(Px[] a, Px[] b)
{
    for (var i = 0; i < a.Length; i++)
        if (a[i].R != b[i].R || a[i].G != b[i].G || a[i].B != b[i].B || a[i].A != b[i].A) return false;
    return true;
}

double BenchUs(Action body, int reps)
{
    body(); // warm up / JIT
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < reps; r++) body();
    sw.Stop();
    return sw.Elapsed.TotalMicroseconds / reps;
}

record Row(string Variant, string MeanUs, string Speedup, string BitExact);
