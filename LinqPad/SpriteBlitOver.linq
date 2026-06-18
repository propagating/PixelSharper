<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Runtime.Intrinsics</Namespace>
</Query>

// ===========================================================================================
// Sprite blit "source-over" (VARIABLE source): SCALAR span vs HARDWARE INTRINSICS (Vector128 / Vector256).
// Run in LINQPad 8 (.NET 8). A "pixel" is 4 bytes (R,G,B,A) packed in a struct.
//
// WHAT THIS IS
//   This is the variable-source ("source-over") alpha blit behind DrawSprite in Alpha pixel mode: every
//   source pixel carries its OWN alpha. Per pixel the blend is (bit-exact with PixelGameEngine's Draw()
//   Alpha case):
//       a = s.A / 255f * blend;   c = 1f - a;
//       out.R = (byte)(a * s.R + c * dst.R);   (likewise G, B);   out.A = 255
//
// THE TEACHING POINT: INTRINSICS AREN'T ALWAYS FASTER — MEASURE.
//   AlphaBlendIntrinsics.linq showed SIMD WINNING (~4.3x) for a CONSTANT source: the per-pixel blend
//   factors (a, c, k = a*src.rgb) are loop-invariant, computed once, splatted into vector registers, and
//   every iteration is a pure widen -> fma -> narrow over the destination. Memory-light, arithmetic-heavy:
//   SIMD's sweet spot.
//
//   A VARIABLE source flips that. Now a, c, and a*src.rgb change EVERY pixel, so you must:
//     * widen BOTH the src bytes AND the dst bytes to float (two loads + two widen chains, not one), and
//     * shuffle-broadcast each pixel's alpha lane across its own R/G/B lanes before you can multiply.
//   That per-group shuffle + the extra widen/load traffic costs more than the multiply-add it saves. The
//   scalar span, meanwhile, is already essentially memory-bandwidth-bound (it streams src+dst once), so
//   there's little arithmetic headroom for SIMD to reclaim. Net result: SIMD's edge collapses — often to
//   nothing, or to an outright loss — instead of the big constant-source win.
//
// CONCLUSION
//   The engine ships the SCALAR span fast path for sprite alpha blits (not SIMD) — the opposite call from
//   the constant-source FillRect. The Dump below shows the SIMD "Speedup" column at or near (often below)
//   1.00x: nowhere near the ~4.3x of the constant case, so the SIMD complexity isn't worth carrying.
//
// EXPECTED: Scalar 1.00x (ref); Vector128 and Vector256 at or below 1x. The correctness gate proves the
//   SIMD kernels are still bit-exact — they're correct, just (here) not worth it. The exact margin is
//   HARDWARE-DEPENDENT: it hinges on the CPU's memory bandwidth vs its SIMD throughput, so on a machine
//   with abundant FP units and lots of bandwidth you may see SIMD draw level or even edge ahead — which is
//   itself the lesson. The point stands: the win is far smaller than the constant-source case (and often
//   negative), nowhere near the ~4.3x there, so the engine doesn't bother — it ships the scalar fast path.
// ===========================================================================================

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
struct Px { public byte R, G, B, A; public Px(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; } }

const int W = 256, H = 240;          // a full 256x240 screen = 61440 pixels
const int Reps = 2000;

void Main()
{
    var src = NewSource();           // a sprite with a full 0..255 alpha range per pixel

    // --- Correctness gate: every SIMD variant must match the scalar reference bit-for-bit ---
    var reference = NewDest(); ScalarOver(reference, src, 1f);
    var c128 = NewDest(); Simd128Over(c128, src, 1f); var ok128 = Equal(reference, c128);
    var c256 = NewDest(); Simd256Over(c256, src, 1f); var ok256 = Equal(reference, c256);
    if (!ok128 || !ok256)
        throw new Exception($"SIMD not bit-exact (V128={ok128}, V256={ok256}) — a faster-but-wrong kernel is not a win!");

    // --- Timing ---
    var dS = NewDest(); var d128 = NewDest(); var d256 = NewDest();
    var ms = BenchUs(() => ScalarOver(dS, src, 1f), Reps);
    var m1 = BenchUs(() => Simd128Over(d128, src, 1f), Reps);
    var m2 = BenchUs(() => Simd256Over(d256, src, 1f), Reps);

    new[]
    {
        new Row("Scalar span (the shipped fast path)", $"{ms:F2}", "1.00x", "ref"),
        new Row("Vector128  (SSE2/NEON, 4 px/iter)",   $"{m1:F2}", $"{ms / m1:F2}x", ok128 ? "yes" : "NO"),
        new Row("Vector256  (AVX2, 8 px/iter)",        $"{m2:F2}", $"{ms / m2:F2}x", ok256 ? "yes" : "NO"),
    }.Dump($"Variable-source (source-over) sprite blit over {W * H:N0} px — scalar vs intrinsics (SIMD's win collapses)");

    $"Hardware: Vector128.IsHardwareAccelerated={Vector128.IsHardwareAccelerated}, " +
    $"Vector256.IsHardwareAccelerated={Vector256.IsHardwareAccelerated}".Dump();
}

// Source sprite: every pixel carries its own (full-range) alpha.
static Px[] NewSource()
{
    var rng = new Random(1234);
    var b = new Px[W * H];
    for (var i = 0; i < b.Length; i++)
        b[i] = new Px((byte)(i * 7), (byte)(i * 13 >> 2), (byte)(i * 29 >> 1), (byte)rng.Next(0, 256));
    return b;
}

static Px[] NewDest()
{
    var b = new Px[W * H];
    for (var i = 0; i < b.Length; i++) b[i] = new Px((byte)i, (byte)(i >> 2), 90, 255);
    return b;
}

// ---- Scalar reference: the source-over span blit the engine ships (bit-exact with Draw() Alpha case) ----
static void ScalarOver(Px[] d, Px[] s, float blend)
{
    var dst = d.AsSpan();
    var src = s.AsSpan();
    for (var i = 0; i < dst.Length; i++)
    {
        var sp = src[i]; var dp = dst[i];
        var a = sp.A / 255f * blend; var c = 1f - a;
        dst[i] = new Px((byte)(a * sp.R + c * dp.R), (byte)(a * sp.G + c * dp.G), (byte)(a * sp.B + c * dp.B));
    }
}

// ---- Vector128: 4 pixels (16 bytes) per iteration ----
// Per pixel: a = (alphaBroadcast / 255) * blend (divide-THEN-multiply, to match the scalar op order
// exactly for bit-exactness), c = 1 - a, out = a*src + c*dst, narrow to bytes, OR 0xFF into the alpha byte.
static void Simd128Over(Px[] d, Px[] s, float blend)
{
    var dst = d.AsSpan();
    var src = s.AsSpan();

    if (Vector128.IsHardwareAccelerated && dst.Length >= 4)
    {
        var v255 = Vector128.Create(255f);
        var blendV = Vector128.Create(blend);
        var one = Vector128.Create(1f);
        var alphaByte = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
        var dBytes = MemoryMarshal.AsBytes(dst);
        var sBytes = MemoryMarshal.AsBytes(src);
        int i = 0, limit = dBytes.Length - (dBytes.Length & 15);
        for (; i < limit; i += 16)
        {
            var sb = Vector128.Create<byte>(sBytes.Slice(i, 16));
            var db = Vector128.Create<byte>(dBytes.Slice(i, 16));

            var sLo = Vector128.WidenLower(sb); var sHi = Vector128.WidenUpper(sb);
            var dLo = Vector128.WidenLower(db); var dHi = Vector128.WidenUpper(db);

            var sf0 = Vector128.ConvertToSingle(Vector128.WidenLower(sLo).AsInt32());
            var sf1 = Vector128.ConvertToSingle(Vector128.WidenUpper(sLo).AsInt32());
            var sf2 = Vector128.ConvertToSingle(Vector128.WidenLower(sHi).AsInt32());
            var sf3 = Vector128.ConvertToSingle(Vector128.WidenUpper(sHi).AsInt32());
            var df0 = Vector128.ConvertToSingle(Vector128.WidenLower(dLo).AsInt32());
            var df1 = Vector128.ConvertToSingle(Vector128.WidenUpper(dLo).AsInt32());
            var df2 = Vector128.ConvertToSingle(Vector128.WidenLower(dHi).AsInt32());
            var df3 = Vector128.ConvertToSingle(Vector128.WidenUpper(dHi).AsInt32());

            var o0 = Blend128(sf0, df0, v255, blendV, one);
            var o1 = Blend128(sf1, df1, v255, blendV, one);
            var o2 = Blend128(sf2, df2, v255, blendV, one);
            var o3 = Blend128(sf3, df3, v255, blendV, one);

            var u0 = Vector128.Narrow(Vector128.ConvertToInt32(o0).AsUInt32(), Vector128.ConvertToInt32(o1).AsUInt32());
            var u1 = Vector128.Narrow(Vector128.ConvertToInt32(o2).AsUInt32(), Vector128.ConvertToInt32(o3).AsUInt32());
            (Vector128.Narrow(u0, u1) | alphaByte).CopyTo(dBytes.Slice(i, 16));
        }
        for (int px = i / 4; px < dst.Length; px++)
        {
            var sp = src[px]; var dp = dst[px];
            var a = sp.A / 255f * blend; var c = 1f - a;
            dst[px] = new Px((byte)(a * sp.R + c * dp.R), (byte)(a * sp.G + c * dp.G), (byte)(a * sp.B + c * dp.B));
        }
        return;
    }
    ScalarOver(d, s, blend);
}

// One pixel-group blend for Vector128: broadcast the alpha (lane 3) across R/G/B and combine.
static Vector128<float> Blend128(Vector128<float> sf, Vector128<float> df, Vector128<float> v255, Vector128<float> blendV, Vector128<float> one)
{
    var aBcast = Vector128.Shuffle(sf, Vector128.Create(3, 3, 3, 3)); // [A,A,A,A]
    var a = aBcast / v255 * blendV;     // a = (A / 255) * blend  (divide-THEN-multiply: A/255f differs from
                                        // A*(1f/255f) in IEEE, so divide to stay byte-exact with the scalar)
    var c = one - a;
    return a * sf + c * df;
}

// ---- Vector256: 8 pixels (32 bytes) per iteration ----
static void Simd256Over(Px[] d, Px[] s, float blend)
{
    var dst = d.AsSpan();
    var src = s.AsSpan();

    if (Vector256.IsHardwareAccelerated && dst.Length >= 8)
    {
        var v255 = Vector256.Create(255f);
        var blendV = Vector256.Create(blend);
        var one = Vector256.Create(1f);
        var alpha128 = Vector128.Create((byte)0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
        var alphaByte = Vector256.Create(alpha128, alpha128);
        var dBytes = MemoryMarshal.AsBytes(dst);
        var sBytes = MemoryMarshal.AsBytes(src);
        int i = 0, limit = dBytes.Length - (dBytes.Length & 31);
        for (; i < limit; i += 32)
        {
            var sb = Vector256.Create<byte>(sBytes.Slice(i, 32));
            var db = Vector256.Create<byte>(dBytes.Slice(i, 32));

            var sLo = Vector256.WidenLower(sb); var sHi = Vector256.WidenUpper(sb);
            var dLo = Vector256.WidenLower(db); var dHi = Vector256.WidenUpper(db);

            var sf0 = Vector256.ConvertToSingle(Vector256.WidenLower(sLo).AsInt32());
            var sf1 = Vector256.ConvertToSingle(Vector256.WidenUpper(sLo).AsInt32());
            var sf2 = Vector256.ConvertToSingle(Vector256.WidenLower(sHi).AsInt32());
            var sf3 = Vector256.ConvertToSingle(Vector256.WidenUpper(sHi).AsInt32());
            var df0 = Vector256.ConvertToSingle(Vector256.WidenLower(dLo).AsInt32());
            var df1 = Vector256.ConvertToSingle(Vector256.WidenUpper(dLo).AsInt32());
            var df2 = Vector256.ConvertToSingle(Vector256.WidenLower(dHi).AsInt32());
            var df3 = Vector256.ConvertToSingle(Vector256.WidenUpper(dHi).AsInt32());

            var o0 = Blend256(sf0, df0, v255, blendV, one);
            var o1 = Blend256(sf1, df1, v255, blendV, one);
            var o2 = Blend256(sf2, df2, v255, blendV, one);
            var o3 = Blend256(sf3, df3, v255, blendV, one);

            var u0 = Vector256.Narrow(Vector256.ConvertToInt32(o0).AsUInt32(), Vector256.ConvertToInt32(o1).AsUInt32());
            var u1 = Vector256.Narrow(Vector256.ConvertToInt32(o2).AsUInt32(), Vector256.ConvertToInt32(o3).AsUInt32());
            (Vector256.Narrow(u0, u1) | alphaByte).CopyTo(dBytes.Slice(i, 32));
        }
        for (int px = i / 4; px < dst.Length; px++)
        {
            var sp = src[px]; var dp = dst[px];
            var a = sp.A / 255f * blend; var c = 1f - a;
            dst[px] = new Px((byte)(a * sp.R + c * dp.R), (byte)(a * sp.G + c * dp.G), (byte)(a * sp.B + c * dp.B));
        }
        return;
    }
    Simd128Over(d, s, blend);
}

// One pixel-group blend for Vector256. Vector256.Shuffle is LANE-CROSSING here only within each 128-bit
// half by construction of the index vector ([3,3,3,3] in lo, [7,7,7,7] in hi) — each pixel's alpha stays
// within its own pixel.
static Vector256<float> Blend256(Vector256<float> sf, Vector256<float> df, Vector256<float> v255, Vector256<float> blendV, Vector256<float> one)
{
    var aBcast = Vector256.Shuffle(sf, Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7)); // [A0x4, A1x4]
    var a = aBcast / v255 * blendV;     // a = (A / 255) * blend  (divide-THEN-multiply: A/255f differs from
                                        // A*(1f/255f) in IEEE, so divide to stay byte-exact with the scalar)
    var c = one - a;
    return a * sf + c * df;
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
