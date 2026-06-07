using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Examples.Scenes;

internal static class DemoArt
{
    // Shared 8x8 checker sprite used by several scenes.
    public static Sprite Checker()
    {
        var s = new Sprite(8, 8);
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var edge = x == 0 || y == 0 || x == 7 || y == 7;
                s.SetPixel(x, y, edge ? Pixel.MAGENTA : ((x + y) % 2 == 0 ? Pixel.WHITE : Pixel.DARK_GREY));
            }
        return s;
    }
}

// ---------------------------------------------------------------------------------------------
// Decals — GPU-textured quads: scaled, rotated, gradient-filled, plus text decals.
// ---------------------------------------------------------------------------------------------
public class DecalsScene : IExampleScene
{
    public string Title => "Decals (GPU path)";
    private Decal _decal = null!;
    private float _t;

    public void Initialise(Showcase e) => _decal = new Decal(DemoArt.Checker());

    public void Update(Showcase e, float dt)
    {
        _t += dt;
        var y = e.CanvasTop + 4;
        e.DrawString(4, y, "Decals are hardware-textured quads: smooth scaling + rotation.", Pixel.WHITE);

        e.DrawDecal(new Vector2d<float>(20, y + 22), _decal, new Vector2d<float>(4, 4));
        e.DrawRotatedDecal(new Vector2d<float>(120, y + 40), _decal, _t * 2f,
            new Vector2d<float>(4, 4), new Vector2d<float>(4, 4), Pixel.YELLOW);
        e.GradientFillRectDecal(new Vector2d<float>(175, y + 20), new Vector2d<float>(90, 44),
            Pixel.RED, Pixel.GREEN, Pixel.BLUE, Pixel.YELLOW);

        e.DrawStringDecal(new Vector2d<float>(20, y + 72), "DrawDecal  DrawRotatedDecal", Pixel.WHITE);
        e.DrawStringDecal(new Vector2d<float>(20, y + 84), "GradientFillRectDecal  DrawStringDecal", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// Input — poll keyboard + mouse; paint with the mouse.
// ---------------------------------------------------------------------------------------------
public class InputScene : IExampleScene
{
    public string Title => "Input";
    private readonly List<Vector2d<int>> _trail = new();

    public void Initialise(Showcase e) => _trail.Clear();

    public void Update(Showcase e, float dt)
    {
        var y = e.CanvasTop + 4;
        e.DrawString(4, y, "GetKey / GetMouse / GetMousePos / GetMouseWheel — all pull-model.", Pixel.WHITE);

        // WASD state pads.
        Key(e, "W", KeyPress.W, 34, y + 20);
        Key(e, "A", KeyPress.A, 20, y + 34);
        Key(e, "S", KeyPress.S, 34, y + 34);
        Key(e, "D", KeyPress.D, 48, y + 34);

        e.DrawString(110, y + 20, $"Mouse : {e.GetMouseX()}, {e.GetMouseY()}", Pixel.CYAN);
        e.DrawString(110, y + 32, $"Wheel : {e.GetMouseWheel()}", Pixel.CYAN);
        var held = e.GetMouse((int)Mouse.Left).Held;
        e.DrawString(110, y + 44, $"LMB   : {(held ? "down" : "up")}", held ? Pixel.GREEN : Pixel.GREY);

        if (held && e.GetMouseY() > e.CanvasTop && e.GetMouseY() < e.CanvasBottom)
            _trail.Add(new Vector2d<int>(e.GetMouseX(), e.GetMouseY()));
        foreach (var p in _trail) e.FillCircle(p.X, p.Y, 2, Pixel.TANGERINE);

        e.DrawString(4, e.CanvasBottom - 8, "Hold the left mouse button to paint a trail.", Pixel.GREY);
    }

    private static void Key(Showcase e, string label, KeyPress k, int x, int y)
    {
        var down = e.GetKey(k).Held;
        e.FillRect(x, y, 12, 12, down ? Pixel.GREEN : Pixel.DARK_GREY);
        e.DrawString(x + 3, y + 2, label, Pixel.WHITE);
    }
}

// ---------------------------------------------------------------------------------------------
// Pixel modes — Normal (overwrite), Mask (skip fully-transparent), Alpha (blend).
// ---------------------------------------------------------------------------------------------
public class PixelModesScene : IExampleScene
{
    public string Title => "Pixel Modes & Blending";

    public void Initialise(Showcase e) { }

    public void Update(Showcase e, float dt)
    {
        var y = e.CanvasTop + 6;
        e.DrawString(4, y, "SetPixelMode controls how pixels combine with what's there.", Pixel.WHITE);

        // Opaque base bars.
        e.FillRect(20, y + 24, 120, 18, Pixel.DARK_BLUE);
        e.FillRect(20, y + 48, 120, 18, Pixel.DARK_BLUE);

        // Normal: the red box overwrites.
        e.SetPixelMode(PixelDisplayMode.Normal);
        e.FillRect(40, y + 18, 40, 30, new Pixel(220, 60, 60));
        e.DrawString(150, y + 26, "Normal: overwrite", Pixel.WHITE);

        // Alpha: the translucent green box blends.
        e.SetPixelMode(PixelDisplayMode.Alpha);
        e.FillRect(40, y + 42, 40, 30, new Pixel(60, 220, 60, 128));
        e.SetPixelMode(PixelDisplayMode.Normal);
        e.DrawString(150, y + 50, "Alpha: 50% blend", Pixel.WHITE);
    }
}
