using BenchmarkDotNet.Attributes;
using PixelSharper.Core;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Benchmarks;

// Span fast-paths vs the per-pixel fallback. "Slow" forces the per-pixel Draw() path by selecting a
// Custom pixel mode (an overwrite lambda) — same output, but pixel-by-pixel — so the delta is purely
// the Span.Fill / Span.CopyTo win.
[MemoryDiagnoser]
public class DrawBenchmarks
{
    private sealed class Engine : PixelGameEngine
    {
        public override bool OnCreate() => true;
        public override bool OnUpdate(float t) => true;
    }

    private readonly Engine _e = new();
    private Sprite _sprite = null!;
    private Sprite _alphaSprite = null!;

    [GlobalSetup]
    public void Setup()
    {
        _e.SetDrawTarget(new Sprite(256, 240));
        _sprite = new Sprite(128, 128);
        for (var i = 0; i < 128 * 128; i++) _sprite.PixelData[i] = new Pixel((byte)i, (byte)(i >> 1), 200);
        _alphaSprite = new Sprite(128, 128);
        for (var i = 0; i < 128 * 128; i++) _alphaSprite.PixelData[i] = new Pixel((byte)i, (byte)(i >> 1), 200, (byte)(i >> 2));
    }

    [Benchmark] public void FillRect_Span()  { _e.SetPixelMode(PixelDisplayMode.Normal); _e.FillRect(0, 0, 256, 240, Pixel.RED); }
    [Benchmark] public void FillRect_PerPixel() { _e.SetPixelMode((x, y, n, o) => n); _e.FillRect(0, 0, 256, 240, Pixel.RED); }

    [Benchmark] public void DrawSprite_Span() { _e.SetPixelMode(PixelDisplayMode.Normal); _e.DrawSprite(10, 10, _sprite); }
    [Benchmark] public void DrawSprite_PerPixel() { _e.SetPixelMode((x, y, n, o) => n); _e.DrawSprite(10, 10, _sprite); }

    // Alpha-mode sprite blit: the new 1:1 span fast path (BlendRowOver) vs the per-pixel path (forced via a
    // Custom-mode lambda doing the same alpha blend). Both produce identical pixels; the delta is overhead.
    [Benchmark] public void DrawSprite_Alpha_Span() { _e.SetPixelMode(PixelDisplayMode.Alpha); _e.DrawSprite(10, 10, _alphaSprite); }
    [Benchmark]
    public void DrawSprite_Alpha_PerPixel()
    {
        _e.SetPixelMode((x, y, n, o) =>
        {
            var a = n.Alpha / 255f; var c = 1f - a;
            return new Pixel((byte)(a * n.Red + c * o.Red), (byte)(a * n.Green + c * o.Green), (byte)(a * n.Blue + c * o.Blue));
        });
        _e.DrawSprite(10, 10, _alphaSprite);
    }

    private static readonly Pixel Translucent = new(200, 50, 10, 128);
    [Benchmark] public void FillRect_Alpha_Span() { _e.SetPixelMode(PixelDisplayMode.Alpha); _e.FillRect(0, 0, 256, 240, Translucent); }
    [Benchmark]
    public void FillRect_Alpha_PerPixel()
    {
        _e.SetPixelMode((x, y, n, o) =>
        {
            var a = n.Alpha / 255f; var c = 1f - a;
            return new Pixel((byte)(a * n.Red + c * o.Red), (byte)(a * n.Green + c * o.Green), (byte)(a * n.Blue + c * o.Blue));
        });
        _e.FillRect(0, 0, 256, 240, Translucent);
    }
}
