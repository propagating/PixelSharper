using System;
using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// Performance Monitor — the PerfMonitor utility: transparent Wrap<IExampleScene> timing + manual
// using-scopes, displayed live via PerfOverlay.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the <see cref="PerfMonitor"/> utility: a wrapped sub-scene timed transparently via
/// <see cref="PerfMonitor.Wrap{T}"/>, two manually-scoped helpers, and the live <see cref="PerfOverlay"/>.</summary>
/// <remarks>TAB toggles the overlay; R resets the stats. Shows both injection styles at once.</remarks>
public class PerfMonitorScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Performance Monitor"</c>.</value>
    public string Title => "Performance Monitor";

    /// <summary>The shared collector this scene feeds and displays.</summary>
    private readonly PerfMonitor _perf = new();
    /// <summary>A real sub-scene wrapped so every call to it is timed transparently (the Wrap demo).</summary>
    private IExampleScene _wrapped = null!;
    /// <summary>Whether the overlay is currently shown (toggled with TAB).</summary>
    private bool _showOverlay = true;
    /// <summary>Accumulated time, used to vary the artificial workload.</summary>
    private float _t;

    /// <summary>Builds the wrapped sub-scene and times its Initialise through the proxy.</summary>
    /// <param name="e">The host showcase engine.</param>
    public void Initialise(Showcase e)
    {
        _perf.Reset();
        // Wrap<IExampleScene>: every call to the returned proxy is timed and recorded as
        // "IExampleScene.<Method>", with zero changes to BouncingBallsScene itself.
        _wrapped = PerfMonitor.Wrap<IExampleScene>(new BouncingBallsScene(), _perf);
        _wrapped.Initialise(e); // recorded as IExampleScene.Initialise
    }

    /// <summary>Runs the wrapped sub-scene (transparently timed) plus two manually-scoped helpers, then draws the overlay.</summary>
    /// <param name="e">The host showcase engine.</param>
    /// <param name="dt">Seconds since the previous frame.</param>
    public void Update(Showcase e, float dt)
    {
        _t += dt;
        if (e.GetKey(KeyPress.TAB).Pressed) _showOverlay = !_showOverlay;
        if (e.GetKey(KeyPress.R).Pressed) _perf.Reset();

        // (1) TRANSPARENT timing via the proxy — recorded as "IExampleScene.Update".
        _wrapped.Update(e, dt);

        // (2) MANUAL timing via using-scopes — each helper auto-names itself with [CallerMemberName].
        SpinCpu();
        BlendBars(e);

        // (3) DISPLAY — the on-screen overlay (software DrawString), plus a controls hint.
        if (_showOverlay)
            PerfOverlay.Draw(e, _perf, new Vector2d<int>(4, e.CanvasTop + 2), 1, 8);

        e.DrawString(4, e.CanvasBottom - 8,
            "TAB overlay   R reset      (Wrap<IExampleScene> + using-scope timing)", Pixel.GREY);
    }

    /// <summary>Artificial CPU work, timed by a manual scope so it shows up as "SpinCpu".</summary>
    private void SpinCpu()
    {
        using var _ = _perf.Measure();
        double acc = 0;
        var iterations = 15000 + (int)(MathF.Sin(_t) * 5000); // vary it so min/max/last differ
        for (var k = 0; k < iterations; k++) acc += Math.Sqrt(k + _t + 1);
        if (double.IsNaN(acc)) throw new InvalidOperationException(); // keep the loop from being optimised away
    }

    /// <summary>Draws translucent bars (exercises the SIMD alpha blend), timed as "BlendBars".</summary>
    /// <param name="e">The host showcase engine.</param>
    private void BlendBars(Showcase e)
    {
        using var _ = _perf.Measure();
        e.SetPixelMode(PixelDisplayMode.Alpha);
        for (var b = 0; b < 6; b++)
        {
            var alpha = (byte)(40 + b * 32);
            e.FillRect(150, e.CanvasTop + 12 + b * 13, 190, 11, new Pixel(80, 160, 255, alpha));
        }
        e.SetPixelMode(PixelDisplayMode.Normal);
    }
}

// ---------------------------------------------------------------------------------------------
// A plain sub-scene used as the Wrap<IExampleScene> demo target — it has NO knowledge of PerfMonitor;
// the proxy times its Initialise/Update transparently.
// ---------------------------------------------------------------------------------------------
/// <summary>A simple bouncing-circles scene used as the wrapped target in <see cref="PerfMonitorScene"/>.</summary>
/// <remarks>Deliberately knows nothing about timing — <see cref="PerfMonitor.Wrap{T}"/> instruments it from the outside.</remarks>
internal sealed class BouncingBallsScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Bouncing Balls"</c>.</value>
    public string Title => "Bouncing Balls";

    /// <summary>The live circles: position, velocity, and colour.</summary>
    private readonly List<Ball> _balls = new();

    /// <summary>Seeds a fixed set of randomly-placed circles.</summary>
    /// <param name="e">The host showcase engine (for screen/canvas bounds).</param>
    public void Initialise(Showcase e)
    {
        _balls.Clear();
        var rng = new Random(7);
        for (var i = 0; i < 28; i++)
            _balls.Add(new Ball(
                (float)(rng.NextDouble() * e.ScreenWidth()),
                e.CanvasTop + 6 + (float)(rng.NextDouble() * 95),
                (float)(rng.NextDouble() * 60 - 30),
                (float)(rng.NextDouble() * 60 - 30),
                new Pixel((byte)rng.Next(80, 256), (byte)rng.Next(80, 256), (byte)rng.Next(80, 256))));
    }

    /// <summary>Integrates and draws every circle, bouncing off the canvas edges.</summary>
    /// <param name="e">The host showcase engine.</param>
    /// <param name="dt">Seconds since the previous frame.</param>
    public void Update(Showcase e, float dt)
    {
        var top = e.CanvasTop + 4;
        var bottom = e.CanvasTop + 105;
        var right = e.ScreenWidth() - 4;
        for (var i = 0; i < _balls.Count; i++)
        {
            var b = _balls[i];
            b.X += b.Vx * dt; b.Y += b.Vy * dt;
            if (b.X < 4 || b.X > right) b.Vx = -b.Vx;
            if (b.Y < top || b.Y > bottom) b.Vy = -b.Vy;
            _balls[i] = b;
            e.FillCircle((int)b.X, (int)b.Y, 3, b.Colour);
        }
    }

    /// <summary>One circle's mutable state.</summary>
    private struct Ball
    {
        public float X, Y, Vx, Vy;
        public Pixel Colour;
        public Ball(float x, float y, float vx, float vy, Pixel colour) { X = x; Y = y; Vx = vx; Vy = vy; Colour = colour; }
    }
}
