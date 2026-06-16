using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions;

/// <summary>
/// Port of olcPGEX_SplashScreen — an animated OLC splash + copyright notice (for OLC-3 compliance).
/// Auto-hooks (base ctor hook:true) and blocks the user's OnUpdate until the animation completes.
/// </summary>
public class SplashScreen : PGEX
{
    /// <summary>Renderable holding the decoded OLC logo sprite and its decal.</summary>
    private readonly Renderable _spr = new();
    /// <summary>Per-pixel particle state (position + velocity) for the explode animation.</summary>
    private (Vector2d<float> Pos, Vector2d<float> Vel)[] _boom = System.Array.Empty<(Vector2d<float>, Vector2d<float>)>();
    /// <summary>Logo render scale derived from the screen width.</summary>
    private Vector2d<float> _scale;
    /// <summary>Initial top-left position of the logo on screen.</summary>
    private Vector2d<float> _position;
    /// <summary>Elapsed animation time, in seconds.</summary>
    private float _particleTime;
    /// <summary>Screen aspect ratio used to keep the logo square.</summary>
    private float _aspect;
    /// <summary>True once the animation has finished and the splash should stop blocking.</summary>
    private bool _complete;
    /// <summary>Random source for particle jitter and velocities.</summary>
    private readonly Random _rng = new();

    /// <summary>Creates the splash screen and auto-registers it as a hooked extension.</summary>
    /// <remarks>Passes <c>hook:true</c> to the <see cref="PGEX"/> base so the engine drives this
    /// extension's lifecycle hooks (notably <see cref="OnAfterUserCreate"/> and <see cref="OnBeforeUserUpdate"/>).</remarks>
    public SplashScreen() : base(true) { }

    /// <summary>Builds the OLC logo sprite and decal once, after the user's OnCreate; invoked by the engine because this PGEX hooked itself.</summary>
    protected internal override void OnAfterUserCreate()
    {
        const string logo =
            "000000000000000000000000000000000000000000000000000000000000000000005EEEEEEEEEEEEEEEEEEE" +
            "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEED1EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE" +
            "EEEEEEEEEEEEEEEEEEEEEEEEEED5EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE" +
            "EEEEEE@E@0000000000000000000000000000000000000000000000000000000000000001E1D:ZZZZZZZZZZZ" +
            "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ1D5BZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ" +
            "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ5@E:P0002Z002ZZX000000000000ZP00000000000000000000000000" +
            "00ZX000Z002XE1DX?o`o:Poo800SooaE5@E1ED5BX?ol5E@E0E1ED?oo5@E1ED5DE1D5E@ZQEEBPEE2QD5BSoocl" +
            "Z?olQAB?oo5DEEDEEDE:SooaEEAE5DEEDoolEADEEDEAE5AEEBZ5EE:5EE:5@E:?oo?bXoob558o3lEAEEAD5ADZ" +
            "?oo5@E5EEAD5Cl01E5AD5AE5DE5@E:X01DXEEDXE1DXo3lo:Sl0800SooaE1ED5EE5BXo00EEDEEE5EE?oo5EE5E" +
            "E5DEEDEEDZQEEBQD5BQD5BSl?cl0?`0ZZZ?oo5D5E@EEDE03loaEEAEEDEEDoolEED5EDEAEEAEEBZ5EE:5@E:5@" +
            "E:?oo?oloob008o00EAEEAD01EE?co5EE5EEAD03l01DE@05AE5AE5@0:XE000EEDXE1DXooloocoo8DDSlZQE5E" +
            "E5EE5EDoolE1DE4E5EE?oo5AE5EE5DE5DEEDZQEEAAEEBQD5BPoo3oo3olQAB?bZ5DE1D5EDEE@ooaD5AD1D5EDo" +
            "olE1DEE@EAD5@EEBZ5EE51ED:5@E:P000000020080:X000000000000000000000000000000000000000:X000" +
            "0002XE1DZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZQD5@ZZZZZZZZZZZZ" +
            "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZX5@E@00000000000000000000000000000000" +
            "00000000000000000000000000000001E1EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE" +
            "EEEEEEEEEEEED5EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE@5EEEEEE" +
            "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEED0000000000000000000000000000" +
            "0000000000000000000000000000000000000000";

        _spr.Create(203, 24, false, true);
        int px = 0, py = 0;
        for (var b = 0; b < 1624; b += 4)
        {
            var sym1 = (uint)logo[b + 0] - 48;
            var sym2 = (uint)logo[b + 1] - 48;
            var sym3 = (uint)logo[b + 2] - 48;
            var sym4 = (uint)logo[b + 3] - 48;
            var r = (sym1 << 18) | (sym2 << 12) | (sym3 << 6) | sym4;
            for (var i = 0; i < 12; i++)
            {
                var p = ((r & 0xC00000) >> 22) switch
                {
                    0u => new Pixel(0, 0, 0, 255),
                    1u => new Pixel(255, 255, 255, 255),
                    2u => new Pixel(255, 120, 26, 255),
                    _ => new Pixel(79, 193, 255, 255)
                };
                _spr.Sprite.SetPixel(px, py, p);
                if (++px == 203) { py++; px = 0; }
                r <<= 2;
            }
        }

        _spr.Decal.Update();
        var w = _spr.Sprite.Width;
        var h = _spr.Sprite.Height;
        _boom = new (Vector2d<float>, Vector2d<float>)[w * h];
        _scale = new Vector2d<float>(Pge.ScreenWidth() / 500.0f, Pge.ScreenWidth() / 500.0f);
        _aspect = (float)Pge.ScreenWidth() / Pge.ScreenHeight();
        _position = new Vector2d<float>((250 - w) / 2.0f, (250 - h) / 2.0f / _aspect);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            _boom[y * w + x] = (
                new Vector2d<float>(_position.X + x, _position.Y + y),
                new Vector2d<float>((float)_rng.NextDouble() * 10.0f - 5.0f, (float)_rng.NextDouble() * 10.0f - 5.0f));
    }

    /// <summary>Advances and renders the splash animation each frame; returns true to block the user update until complete.</summary>
    /// <param name="elapsedTime">Frame delta in seconds, passed by reference per the <see cref="PGEX"/> hook contract; used to integrate particle motion.</param>
    /// <returns><c>true</c> while the animation is playing (suppressing the user's <c>OnUpdate</c> for the frame);
    /// <c>false</c> once complete so the user update runs normally.</returns>
    /// <remarks>Animation phases keyed off the accumulated <c>_particleTime</c>: hold (&lt; 1s),
    /// jitter (&lt; 2s), explode outward (&lt; 5s), then mark complete. Also draws the OLC-3 copyright
    /// notice via the engine's <c>DrawStringPropDecal</c>.</remarks>
    protected internal override bool OnBeforeUserUpdate(ref float elapsedTime)
    {
        if (_complete) return false;

        _particleTime += elapsedTime;
        var w = _spr.Sprite.Width;
        var h = _spr.Sprite.Height;

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var idx = y * w + x;
            if (_particleTime < 1.0f)
            {
                // hold
            }
            else if (_particleTime < 2.0f)
            {
                _boom[idx].Pos = new Vector2d<float>(
                    (250 - w) / 2.0f + x + ((float)_rng.NextDouble() * 0.5f - 0.25f),
                    (250 - h) / 2.0f / _aspect + y + ((float)_rng.NextDouble() * 0.5f - 0.25f));
            }
            else if (_particleTime < 5.0f)
            {
                var v = _boom[idx].Vel;
                _boom[idx].Pos = new Vector2d<float>(
                    _boom[idx].Pos.X + v.X * elapsedTime * 20.0f,
                    _boom[idx].Pos.Y + v.Y * elapsedTime * 20.0f);
            }
            else
            {
                _complete = true;
            }

            var pos = _boom[idx].Pos;
            var dpos = new Vector2d<float>(_scale.X * pos.X * 2.0f, _scale.Y * pos.Y * 2.0f);
            var dscale = new Vector2d<float>(_scale.X * 2.0f, _scale.Y * 2.0f);
            var alpha = Math.Min(1.0f, Math.Max(4.0f - _particleTime, 0.0f));
            Pge.DrawPartialDecal(dpos, _spr.Decal, new Vector2d<float>(x, y), new Vector2d<float>(1, 1), dscale,
                new Pixel(1f, 1f, 1f, alpha));
        }

        const string copyright = "Copyright OneLoneCoder.com 2025";
        var vSize = Pge.GetTextSizeProp(copyright);
        Pge.DrawStringPropDecal(
            new Vector2d<float>(Pge.ScreenWidth() / 2f - vSize.X / 2f, Pge.ScreenHeight() - vSize.Y * 3.0f),
            copyright, new Pixel(1f, 1f, 1f, 0.5f), new Vector2d<float>(1.0f, 2.0f));
        return true;
    }
}
