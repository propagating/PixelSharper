using System;
using System.Collections.Generic;
using PixelSharper.Core;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Renderers;
using PixelSharper.Core.Types;

namespace PixelSharper.Examples;

/// <summary>
/// A phased performance stress harness (run with <c>--stress</c>). Each frame it pushes a heavy, mixed
/// workload — thousands of GPU decals, CPU-rasterized triangles, alpha sprite blits, and decal text —
/// ramping the load across levels (light → extreme) with vsync off. It records per-frame times and
/// allocation per level, then prints a summary table (avg FPS, frame-time p50/p95/p99/max, alloc/frame,
/// GC counts) and exits. The numbers are hardware-specific; use it to compare changes, not as absolutes.
/// </summary>
public sealed class StressEngine : PixelGameEngine
{
    private readonly record struct Level(string Name, int Decals, int Triangles, int Blits, int Strings);

    // Scales the GPU decal path hardest (it is pooled, so this also tests steady-state allocation),
    // with moderate CPU rasterization alongside it.
    private static readonly Level[] Levels =
    {
        new("light",   1000,  200,  100,  50),
        new("medium",  4000,  500,  250,  120),
        new("heavy",   12000, 1000, 500,  250),
        new("extreme", 25000, 2000, 1000, 500),
    };

    private const int WarmupFrames = 20;   // per level: let the JIT/caches/pool settle before timing
    private const int MeasureFrames = 120; // per level: timed frames

    private readonly bool _useOgl10;
    private readonly Random _rng = new(1234);

    private Renderable _decal = null!;   // a textured decal drawn many times (GPU path)
    private Sprite _blit = null!;        // a semi-transparent sprite (CPU alpha-blend path)

    private int _level;
    private int _levelFrame;
    private readonly List<double>[] _frameMs;
    private readonly long[] _allocPerLevel;
    private long _allocAtMeasureStart;
    private int _gc0Start, _gc1Start, _gc2Start;

    /// <summary>Builds the stress engine.</summary>
    /// <param name="useOgl10">When <c>true</c>, drives the legacy OGL10 backend instead of the default OGL33.</param>
    public StressEngine(bool useOgl10)
    {
        ApplicationName = "PixelSharper Stress";
        _useOgl10 = useOgl10;
        _frameMs = new List<double>[Levels.Length];
        for (var i = 0; i < Levels.Length; i++) _frameMs[i] = new List<double>(MeasureFrames);
        _allocPerLevel = new long[Levels.Length];
    }

    /// <inheritdoc />
    protected override Renderer CreateRenderer() =>
        _useOgl10 ? new RendererOgl10() : base.CreateRenderer();

    /// <inheritdoc />
    public override bool OnCreate()
    {
        // A 32x32 checker decal for the GPU path.
        _decal = new Renderable();
        _decal.Create(32, 32, false, true);
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 32; x++)
            _decal.Sprite.SetPixel(x, y, ((x ^ y) & 4) == 0 ? Pixel.WHITE : new Pixel(60, 120, 200, 255));
        _decal.Decal.Update();

        // A 16x16 half-transparent sprite for the CPU alpha-blend path.
        _blit = new Sprite(16, 16);
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
            _blit.SetPixel(x, y, new Pixel((byte)(x * 16), (byte)(y * 16), 200, 128));

        _gc0Start = GC.CollectionCount(0);
        _gc1Start = GC.CollectionCount(1);
        _gc2Start = GC.CollectionCount(2);
        return true;
    }

    /// <inheritdoc />
    public override bool OnUpdate(float elapsedTime)
    {
        var lv = Levels[_level];

        // Start of a level's timed window: snapshot allocation.
        if (_levelFrame == WarmupFrames)
            _allocAtMeasureStart = GC.GetTotalAllocatedBytes(precise: true);

        if (_levelFrame >= WarmupFrames)
            _frameMs[_level].Add(elapsedTime * 1000.0);

        DrawLoad(lv);

        _levelFrame++;
        if (_levelFrame >= WarmupFrames + MeasureFrames)
        {
            _allocPerLevel[_level] = GC.GetTotalAllocatedBytes(precise: true) - _allocAtMeasureStart;
            _level++;
            _levelFrame = 0;
            if (_level >= Levels.Length)
            {
                Report();
                return false; // request shutdown
            }
        }
        return true;
    }

    private void DrawLoad(Level lv)
    {
        int w = ScreenWidth(), h = ScreenHeight();
        Clear(Pixel.BLACK);

        // CPU rasterizer: small filled triangles.
        SetPixelMode(PixelDisplayMode.Normal);
        for (var i = 0; i < lv.Triangles; i++)
        {
            int x = _rng.Next(w), y = _rng.Next(h);
            FillTriangle(x, y, x + _rng.Next(-24, 24), y + _rng.Next(8, 32), x + _rng.Next(8, 32), y + _rng.Next(-24, 24), RandomColor());
        }

        // CPU alpha-blend path: half-transparent sprite blits.
        SetPixelMode(PixelDisplayMode.Alpha);
        for (var i = 0; i < lv.Blits; i++)
            DrawSprite(new Vector2d<int>(_rng.Next(w), _rng.Next(h)), _blit);
        SetPixelMode(PixelDisplayMode.Normal);

        // GPU decal path (pooled): the headline load.
        for (var i = 0; i < lv.Decals; i++)
        {
            var pos = new Vector2d<float>(_rng.Next(w), _rng.Next(h));
            DrawDecal(pos, _decal.Decal, new Vector2d<float>(0.5f, 0.5f), RandomColor());
        }

        // Text via decals.
        for (var i = 0; i < lv.Strings; i++)
            DrawStringDecal(new Vector2d<float>(_rng.Next(w), _rng.Next(h)), "STRESS", RandomColor());
    }

    private Pixel RandomColor() =>
        new((byte)_rng.Next(256), (byte)_rng.Next(256), (byte)_rng.Next(256), (byte)_rng.Next(128, 256));

    private void Report()
    {
        var gc0 = GC.CollectionCount(0) - _gc0Start;
        var gc1 = GC.CollectionCount(1) - _gc1Start;
        var gc2 = GC.CollectionCount(2) - _gc2Start;

        Console.WriteLine();
        Console.WriteLine("==================== PixelSharper stress results ====================");
        Console.WriteLine($"Renderer: {(_useOgl10 ? "OGL10 (legacy)" : "OGL33 (default)")}   Screen: {ScreenWidth()}x{ScreenHeight()}   vsync: off");
        Console.WriteLine($"CPU: {Environment.ProcessorCount} logical cores   Measured frames/level: {MeasureFrames} (after {WarmupFrames} warmup)");
        Console.WriteLine();
        Console.WriteLine($"{"Level",-8} {"decals",7} {"tris",6} {"blits",6} {"str",5} | {"avgFPS",7} {"p50ms",7} {"p95ms",7} {"p99ms",7} {"maxms",7} {"alloc/f",9}");
        Console.WriteLine(new string('-', 92));

        for (var i = 0; i < Levels.Length; i++)
        {
            var lv = Levels[i];
            var ms = _frameMs[i];
            ms.Sort();
            double avg = 0;
            foreach (var v in ms) avg += v;
            avg = ms.Count > 0 ? avg / ms.Count : 0;
            var fps = avg > 0 ? 1000.0 / avg : 0;
            var allocPerFrame = _allocPerLevel[i] / (double)MeasureFrames;

            Console.WriteLine(
                $"{lv.Name,-8} {lv.Decals,7} {lv.Triangles,6} {lv.Blits,6} {lv.Strings,5} | " +
                $"{fps,7:F1} {Pct(ms, 0.50),7:F2} {Pct(ms, 0.95),7:F2} {Pct(ms, 0.99),7:F2} {(ms.Count > 0 ? ms[^1] : 0),7:F2} {FormatBytes(allocPerFrame),9}");
        }

        Console.WriteLine(new string('-', 92));
        Console.WriteLine($"GC over run: gen0={gc0}  gen1={gc1}  gen2={gc2}");
        Console.WriteLine("=====================================================================");
    }

    private static double Pct(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var idx = (int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[idx];
    }

    private static string FormatBytes(double b) =>
        b >= 1_048_576 ? $"{b / 1_048_576:F1}MB" : b >= 1024 ? $"{b / 1024:F1}KB" : $"{b:F0}B";
}
