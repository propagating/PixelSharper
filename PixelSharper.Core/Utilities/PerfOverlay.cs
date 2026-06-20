using PixelSharper.Core.Components;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities;

/// <summary>
/// Draws a <see cref="PerfMonitor"/> snapshot as an on-screen table via the engine's software
/// <see cref="PixelGameEngine.DrawString(int, int, string, Pixel, int)"/> — a lightweight perf HUD, like the
/// built-in FPS counter. Kept separate from <see cref="PerfMonitor"/> so the collector stays engine-free.
/// </summary>
/// <remarks>The columns (name, count, mean, max) align because the embedded font is monospaced. Call this
/// each frame (e.g. behind a toggle key) after the scene has drawn, so it sits on top.</remarks>
public static class PerfOverlay
{
    /// <summary>Draws the slowest tracked methods as a small table at <paramref name="pos"/>.</summary>
    /// <param name="pge">The engine to draw into (uses the current draw target).</param>
    /// <param name="monitor">The monitor whose <see cref="PerfMonitor.Snapshot"/> is displayed.</param>
    /// <param name="pos">Top-left pixel of the overlay; defaults to <c>(0,0)</c>.</param>
    /// <param name="scale">Text scale (1 = 8px glyphs); defaults to <c>1</c>.</param>
    /// <param name="maxRows">Maximum method rows to show (slowest first); defaults to <c>12</c>.</param>
    /// <param name="colour">Row text colour; defaults to <see cref="Pixel.WHITE"/> (the header is always yellow).</param>
    public static void Draw(PixelGameEngine pge, PerfMonitor monitor,
        Vector2d<int> pos = default, int scale = 1, int maxRows = 12, Pixel? colour = null)
    {
        if (pge is null || monitor is null) return;
        var col = colour ?? Pixel.WHITE;
        var snap = monitor.Snapshot();
        var lineH = 8 * scale;
        var x = pos.X;
        var y = pos.Y;

        pge.DrawString(x, y, Row("method", "n", "mean", "max"), Pixel.YELLOW, scale);
        y += lineH;

        var rows = Math.Min(maxRows, snap.Count);
        for (var r = 0; r < rows; r++)
        {
            var s = snap[r];
            pge.DrawString(x, y, Row(Trim(s.Name, 18), s.Count.ToString(), s.MeanMs.ToString("F3"), s.MaxMs.ToString("F2")), col, scale);
            y += lineH;
        }
    }

    /// <summary>Formats one fixed-width row (relies on the monospaced font for column alignment).</summary>
    private static string Row(string name, string n, string mean, string max) => $"{name,-18}{n,5}{mean,8}{max,8}";

    /// <summary>Truncates a name to fit the name column.</summary>
    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
