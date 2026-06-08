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

    [GlobalSetup]
    public void Setup()
    {
        _e.SetDrawTarget(new Sprite(256, 240));
        _sprite = new Sprite(128, 128);
        for (var i = 0; i < 128 * 128; i++) _sprite.PixelData[i] = new Pixel((byte)i, (byte)(i >> 1), 200);
    }

    [Benchmark] public void FillRect_Span()  { _e.SetPixelMode(PixelDisplayMode.Normal); _e.FillRect(0, 0, 256, 240, Pixel.RED); }
    [Benchmark] public void FillRect_PerPixel() { _e.SetPixelMode((x, y, n, o) => n); _e.FillRect(0, 0, 256, 240, Pixel.RED); }

    [Benchmark] public void DrawSprite_Span() { _e.SetPixelMode(PixelDisplayMode.Normal); _e.DrawSprite(10, 10, _sprite); }
    [Benchmark] public void DrawSprite_PerPixel() { _e.SetPixelMode((x, y, n, o) => n); _e.DrawSprite(10, 10, _sprite); }
}
