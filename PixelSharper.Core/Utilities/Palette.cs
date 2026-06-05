using System.Collections.Generic;
using PixelSharper.Core.Components;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_Palette — an interpolated colour palette sampled continuously over [0..1]
// (smooth, between colour stops) or via a precomputed 256-entry LUT (fast, discrete).
public class Palette
{
    public enum Stock { Empty, Greyscale, ColdHot, Spectrum }

    // Sorted colour stops (normalised location, colour) and a fast indexed lookup table.
    private readonly List<(double Loc, Pixel Col)> _colours = new();
    private readonly Pixel[] _indexed = new Pixel[256];

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

    // Continuous sample, t in [0..1]: lerp between the bracketing colour stops.
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

    // Discrete sample via the precomputed LUT, idx in [0..255]: fast, not smooth.
    public Pixel Index(byte idx) => _indexed[idx];

    public void Clear()
    {
        _colours.Clear();
        for (var i = 0; i < 256; i++) _indexed[i] = Pixel.BLACK;
    }

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

    private void ReconstructIndex()
    {
        for (var i = 0; i < 256; i++)
            _indexed[i] = Sample(i / 255.0);
    }
}
