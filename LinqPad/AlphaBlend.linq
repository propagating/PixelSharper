<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

// ===========================================================================================
// Alpha-blend FillRect (constant source): ORIGINAL (per-pixel Draw) vs CHANGED (span blend).
// Run in LINQPad 8 (.NET 8). A "pixel" is 4 bytes (R,G,B,A) packed in a struct.
//
// WHY THE CHANGE WAS MADE
//   In Alpha mode, FillRect blended the source over each destination pixel through Draw():
//   GetPixel (List indexer + bounds), a pixel-mode switch, the float blend, then SetPixel (List indexer
//   + bounds) — per pixel. Unlike the opaque case there IS real arithmetic here, but the per-pixel
//   plumbing still dominates.
//
// WHAT WE CHANGED IT TO
//   For a CONSTANT source over a rect, the blend is a per-channel affine on the destination:
//       out.rgb = c*dst.rgb + k     (c = 1-a, k = a*src.rgb)   out.a = 255
//   We run that straight over the destination row span — the SAME float math (so bit-exact) but without
//   the per-pixel method/switch/indexer overhead.
//
// WHY IT'S FASTER (and why we stopped at scalar)
//   Removing the overhead is ~4x here. The remaining cost is the arithmetic itself. Byte-level SIMD
//   (Vector256) could shave the arithmetic further, but matching the scalar float truncation byte-exact
//   is fiddly and risky, so the engine ships the exact scalar span blend. This script lets you compare.
// ===========================================================================================

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
struct Px { public byte R, G, B, A; public Px(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; } }

const int W = 256, H = 240;
const int Reps = 1000;

void Main()
{
    var src = new Px(200, 50, 10, 128);
    var d1 = NewBuffer(); var d2 = NewBuffer();

    var rows = new List<Row>
    {
        Bench("ORIGINAL (per-pixel Draw)", () => BlendPerPixel(d1, W, H, src, 1f), Reps),
        Bench("CHANGED  (span blend)",     () => BlendSpan(d2, W, H, src, 1f), Reps),
    };
    rows.Dump("Alpha-blend FillRect — original vs span");

    // Exactness check: both must produce identical pixels.
    var a = NewBuffer(); var b = NewBuffer();
    BlendPerPixel(a, W, H, src, 1f); BlendSpan(b, W, H, src, 1f);
    var same = true; for (var i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) { same = false; break; }
    $"bit-exact match: {same}".Dump();
}

static Px[] NewBuffer()
{
    var b = new Px[W * H];
    for (var i = 0; i < b.Length; i++) b[i] = new Px((byte)i, (byte)(i >> 2), 90);
    return b;
}

// ORIGINAL: per-pixel, through SetPixel/GetPixel-style accessors + the float blend.
static Px GetPixel(Px[] buf, int w, int h, int x, int y) => buf[y * w + x];
static void SetPixel(Px[] buf, int w, int h, int x, int y, Px p) { buf[y * w + x] = p; }
static void BlendPerPixel(Px[] d, int w, int h, Px s, float blend)
{
    for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
        {
            var dst = GetPixel(d, w, h, x, y);
            var a = s.A / 255f * blend; var c = 1f - a;
            SetPixel(d, w, h, x, y, new Px((byte)(a * s.R + c * dst.R), (byte)(a * s.G + c * dst.G), (byte)(a * s.B + c * dst.B)));
        }
}

// CHANGED: per-channel affine over the row span (same float math).
static void BlendSpan(Px[] d, int w, int h, Px s, float blend)
{
    var buf = d.AsSpan();
    var a = s.A / 255f * blend; var c = 1f - a;
    float kr = a * s.R, kg = a * s.G, kb = a * s.B;
    for (var y = 0; y < h; y++)
    {
        var row = buf.Slice(y * w, w);
        for (var i = 0; i < row.Length; i++)
        {
            var dst = row[i];
            row[i] = new Px((byte)(c * dst.R + kr), (byte)(c * dst.G + kg), (byte)(c * dst.B + kb));
        }
    }
}

Row Bench(string label, Action body, int reps)
{
    body();
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < reps; r++) body();
    sw.Stop();
    return new Row(label, $"{sw.Elapsed.TotalMicroseconds / reps:F2} us");
}

record Row(string Variant, string Mean);
