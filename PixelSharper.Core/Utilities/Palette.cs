using PixelSharper.Core.Components;

namespace PixelSharper.Core.Utilities;

/// <summary>Port of olcUTIL_Palette — an interpolated colour palette sampled continuously over [0..1] (smooth, between colour stops) or via a precomputed 256-entry LUT (fast, discrete).</summary>
public class Palette
{
    /// <summary>Built-in starter palettes.</summary>
    public enum Stock {
        /// <summary>No colour stops.</summary>
        Empty,
        /// <summary>Black to white.</summary>
        Greyscale,
        /// <summary>Cyan to black to yellow.</summary>
        ColdHot,
        /// <summary>Full hue spectrum.</summary>
        Spectrum }

    /// <summary>Sorted colour stops (normalised location, colour).</summary>
    private readonly List<(double Loc, Pixel Col)> _colours = new();
    /// <summary>Precomputed 256-entry lookup table sampled from the stops.</summary>
    private readonly Pixel[] _indexed = new Pixel[256];

    /// <summary>Creates a palette, optionally seeded from a built-in stock set, then builds the LUT.</summary>
    /// <param name="stock">The built-in palette to seed the colour stops from; defaults to <see cref="Stock.Empty"/>.</param>
    public Palette(Stock stock = Stock.Empty)
    {
        switch (stock)
        {
            case Stock.Empty:
                Clear();
                break;
            case Stock.Greyscale:
                _colours.Add((0.0, Pixel.BLACK));
                _colours.Add((1.0, Pixel.WHITE));
                break;
            case Stock.ColdHot:
                _colours.Add((0.0, Pixel.CYAN));
                _colours.Add((0.5, Pixel.BLACK));
                _colours.Add((1.0, Pixel.YELLOW));
                break;
            case Stock.Spectrum:
                _colours.Add((0.0 / 6.0, Pixel.RED));
                _colours.Add((1.0 / 6.0, Pixel.YELLOW));
                _colours.Add((2.0 / 6.0, Pixel.GREEN));
                _colours.Add((3.0 / 6.0, Pixel.CYAN));
                _colours.Add((4.0 / 6.0, Pixel.BLUE));
                _colours.Add((5.0 / 6.0, Pixel.MAGENTA));
                _colours.Add((6.0 / 6.0, Pixel.RED));
                break;
        }

        ReconstructIndex();
    }

    /// <summary>Continuous sample, t in [0..1]: lerps between the bracketing colour stops.</summary>
    /// <param name="t">The normalised position to sample; clamped to <c>[0, 1]</c>.</param>
    /// <returns>The interpolated colour; <see cref="Pixel.BLACK"/> when there are no stops, or the single stop's colour when there is exactly one.</returns>
    /// <seealso cref="Index"/>
    public Pixel Sample(double t)
    {
        if (_colours.Count == 0) return Pixel.BLACK;
        if (_colours.Count == 1) return _colours[0].Col;

        var i = Math.Clamp(t, 0.0, 1.0);
        var idx = 0;
        while (idx < _colours.Count && i > _colours[idx].Loc) idx++;

        if (idx == 0) return _colours[0].Col;
        if (idx >= _colours.Count) return _colours[^1].Col; // beyond the last stop

        var prev = _colours[idx - 1];
        var cur = _colours[idx];
        return Pixel.LinearInterpolation(prev.Col, cur.Col, (float)((i - prev.Loc) / (cur.Loc - prev.Loc)));
    }

    /// <summary>Discrete sample via the precomputed LUT, idx in [0..255]: fast, not smooth.</summary>
    /// <param name="idx">The lookup-table entry to read, in <c>[0, 255]</c>.</param>
    /// <returns>The precomputed colour at the given LUT index.</returns>
    /// <seealso cref="Sample"/>
    public Pixel Index(byte idx) => _indexed[idx];

    /// <summary>Removes all colour stops and resets the LUT to black.</summary>
    public void Clear()
    {
        _colours.Clear();
        for (var i = 0; i < 256; i++) _indexed[i] = Pixel.BLACK;
    }

    /// <summary>Adds or replaces a colour stop at normalised location d, keeping stops sorted, and rebuilds the LUT.</summary>
    /// <param name="d">The normalised location of the stop; clamped to <c>[0, 1]</c>. An existing stop at the same location is replaced.</param>
    /// <param name="col">The colour for the stop.</param>
    public void SetColour(double d, Pixel col)
    {
        var i = Math.Clamp(d, 0.0, 1.0);
        var existing = _colours.FindIndex(p => p.Loc == i);
        if (existing >= 0)
        {
            _colours[existing] = (i, col);
        }
        else
        {
            _colours.Add((i, col));
            _colours.Sort((a, b) => a.Loc.CompareTo(b.Loc));
        }
        ReconstructIndex();
    }

    /// <summary>Rebuilds the 256-entry LUT by sampling the colour stops evenly over [0..1].</summary>
    private void ReconstructIndex()
    {
        for (var i = 0; i < 256; i++)
            _indexed[i] = Sample(i / 255.0);
    }
}
