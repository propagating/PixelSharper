<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
</Query>

// ===========================================================================================
// Opaque FillRect / DrawSprite: ORIGINAL (per-pixel) vs CHANGED (Span.Fill / Span.CopyTo).
// Run in LINQPad 8 (.NET 8). Pixel == uint (our real Pixel is a 4-byte blittable struct).
//
// WHY THE CHANGE WAS MADE
//   FillRect and a 1:1 DrawSprite plotted every pixel through Draw() -> SetPixel(): a method call, a
//   pixel-mode switch, a List<Pixel> indexer, and a bounds check, PER PIXEL. For a 256x240 fill that's
//   ~61,000 iterations of pure overhead around a single memory write.
//
// WHAT WE CHANGED IT TO
//   In the common opaque case (Normal mode), a fill is a pure overwrite of contiguous memory, and a
//   1:1 unflipped blit is a contiguous row copy. So we do them per ROW:
//       FillRect   -> destRow.Fill(pixel)            (vectorised memset)
//       DrawSprite -> srcRow.CopyTo(destRow)         (vectorised memmove)
//   Span.Fill / Span.CopyTo are implemented as SIMD memset/memmove by the runtime — "free SIMD", no
//   intrinsics to write. (Other modes / scale / flip fall back to the per-pixel path.)
//
// WHY IT'S SO MUCH FASTER
//   Almost ALL of the original cost was the per-pixel overhead, not the memory write. Removing it (one
//   bulk operation per row) is ~70-110x here. Same output exactly (Fill/CopyTo are byte copies).
// ===========================================================================================

const int W = 256, H = 240;
const int Reps = 2000;

void Main()
{
    var dst = new uint[W * H];
    var spr = new uint[128 * 128];
    for (var i = 0; i < spr.Length; i++) spr[i] = (uint)i;

    var rows = new List<Row>
    {
        Bench("FillRect ORIGINAL (per-pixel)", () => FillPerPixel(dst, W, H, 0xFFFF0000), Reps),
        Bench("FillRect CHANGED  (Span.Fill)", () => FillSpan(dst, W, H, 0xFFFF0000), Reps),
        Bench("DrawSprite ORIGINAL (per-pixel)", () => BlitPerPixel(dst, W, H, spr, 128, 128, 10, 10), Reps),
        Bench("DrawSprite CHANGED  (Span.CopyTo)", () => BlitSpan(dst, W, H, spr, 128, 128, 10, 10), Reps),
    };
    rows.Dump("Opaque FillRect / DrawSprite — original vs Span");
}

// ---- ORIGINAL: per-pixel, through a SetPixel with a bounds check (mimics Sprite.SetPixel + Draw) ----
static bool SetPixel(uint[] buf, int w, int h, int x, int y, uint p)
{
    if (x < 0 || x >= w || y < 0 || y >= h) return false;
    buf[y * w + x] = p;
    return true;
}
static uint GetPixel(uint[] buf, int w, int h, int x, int y) => (x >= 0 && x < w && y >= 0 && y < h) ? buf[y * w + x] : 0;

static void FillPerPixel(uint[] d, int w, int h, uint p)
{
    for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
            SetPixel(d, w, h, x, y, p);
}
static void BlitPerPixel(uint[] d, int dw, int dh, uint[] s, int sw, int sh, int ox, int oy)
{
    for (var i = 0; i < sw; i++)
        for (var j = 0; j < sh; j++)
            SetPixel(d, dw, dh, ox + i, oy + j, GetPixel(s, sw, sh, i, j));
}

// ---- CHANGED: one bulk Span operation per row ----
static void FillSpan(uint[] d, int w, int h, uint p)
{
    var buf = d.AsSpan();
    for (var y = 0; y < h; y++) buf.Slice(y * w, w).Fill(p);
}
static void BlitSpan(uint[] d, int dw, int dh, uint[] s, int sw, int sh, int ox, int oy)
{
    var dst = d.AsSpan(); var src = s.AsSpan();
    var cx0 = Math.Max(0, ox); var cx1 = Math.Min(dw, ox + sw); var len = cx1 - cx0;
    if (len <= 0) return;
    for (var j = 0; j < sh; j++)
    {
        var dy = oy + j;
        if (dy < 0 || dy >= dh) continue;
        src.Slice(j * sw + (cx0 - ox), len).CopyTo(dst.Slice(dy * dw + cx0, len));
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
