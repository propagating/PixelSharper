using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// Welcome — the landing page.
// ---------------------------------------------------------------------------------------------
public class WelcomeScene : IExampleScene
{
    public string Title => "Welcome";

    public void Initialise(Showcase e) { }

    public void Update(Showcase e, float elapsedTime)
    {
        var y = e.CanvasTop + 8;
        e.DrawStringProp(8, y, "PixelSharper", Pixel.WHITE, 3);
        e.DrawStringProp(8, y + 28, "a faithful C# port of the olcPixelGameEngine", Pixel.TANGERINE);
        e.DrawString(8, y + 48, "Each page is a runnable tutorial for one feature.", Pixel.GREY);
        e.DrawString(8, y + 60, "Use <- and -> (or SPACE) to step through them.", Pixel.GREY);
        e.DrawString(8, y + 80, "Engine + every extension + every utility, on screen.", Pixel.CYAN);
    }
}

// ---------------------------------------------------------------------------------------------
// Primitives — the CPU drawing surface: lines, rectangles, circles, triangles.
// ---------------------------------------------------------------------------------------------
public class PrimitivesScene : IExampleScene
{
    public string Title => "Drawing Primitives";
    private float _t;

    public void Initialise(Showcase e) { }

    public void Update(Showcase e, float dt)
    {
        _t += dt;
        var y = e.CanvasTop + 4;
        e.DrawString(4, y, "Software primitives, written straight into the pixel buffer.", Pixel.WHITE);

        // Filled shape + matching outline.
        e.FillRect(14, y + 18, 52, 38, Pixel.DARK_RED);
        e.DrawRect(14, y + 18, 52, 38, Pixel.RED);

        e.FillCircle(118, y + 37, 19, Pixel.DARK_GREEN);
        e.DrawCircle(118, y + 37, 19, Pixel.GREEN);

        e.FillTriangle(170, y + 18, 212, y + 56, 158, y + 56, Pixel.DARK_CYAN);
        e.DrawTriangle(170, y + 18, 212, y + 56, 158, y + 56, Pixel.CYAN);

        // An animated line with a dashed bit-pattern.
        var hx = 256 + (int)(MathF.Cos(_t) * 44);
        var hy = y + 37 + (int)(MathF.Sin(_t) * 22);
        e.DrawLine(256, y + 37, hx, hy, Pixel.YELLOW, 0xF0F0F0F0);

        e.DrawString(4, y + 70, "FillRect/DrawRect  FillCircle/DrawCircle  Fill/DrawTriangle", Pixel.GREY);
        e.DrawString(4, y + 80, "DrawLine takes an optional 32-bit dash pattern.", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// Sprites — build a sprite in code, then blit it (scaled, flipped, partial) and texture a triangle.
// ---------------------------------------------------------------------------------------------
public class SpritesScene : IExampleScene
{
    public string Title => "Sprites & Textures";
    private Sprite _sprite = null!;

    public void Initialise(Showcase e)
    {
        // An 8x8 checker with a magenta border.
        _sprite = new Sprite(8, 8);
        for (var sy = 0; sy < 8; sy++)
            for (var sx = 0; sx < 8; sx++)
            {
                var edge = sx == 0 || sy == 0 || sx == 7 || sy == 7;
                _sprite.SetPixel(sx, sy, edge ? Pixel.MAGENTA : ((sx + sy) % 2 == 0 ? Pixel.WHITE : Pixel.DARK_GREY));
            }
    }

    public void Update(Showcase e, float dt)
    {
        var y = e.CanvasTop + 4;
        e.DrawString(4, y, "An 8x8 sprite built pixel-by-pixel in Initialise().", Pixel.WHITE);

        e.DrawSprite(20, y + 22, _sprite);                                       // 1x
        e.DrawSprite(40, y + 22, _sprite, 4);                                    // 4x
        e.DrawSprite(90, y + 22, _sprite, 4, SpriteMirrorMode.Horizontal);       // flipped
        e.DrawString(20, y + 58, "1x       4x        flipped", Pixel.GREY);

        // A software textured (perspective-correct) triangle sampling the sprite.
        e.FillTexturedTriangle(
            new[] { new Vector2d<float>(170, y + 20), new Vector2d<float>(230, y + 60), new Vector2d<float>(160, y + 60) },
            new[] { new Vector2d<float>(0.5f, 0f), new Vector2d<float>(1f, 1f), new Vector2d<float>(0f, 1f) },
            new[] { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE }, _sprite);
        e.DrawString(160, y + 64, "FillTexturedTriangle", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// Text — monospaced + proportional fonts, scaling, and measuring.
// ---------------------------------------------------------------------------------------------
public class TextScene : IExampleScene
{
    public string Title => "Text";

    public void Initialise(Showcase e) { }

    public void Update(Showcase e, float dt)
    {
        var y = e.CanvasTop + 6;
        e.DrawString(6, y, "DrawString uses a fixed-width 8x8 font.", Pixel.WHITE);
        e.DrawStringProp(6, y + 14, "DrawStringProp is proportionally spaced.", Pixel.TANGERINE);
        e.DrawString(6, y + 30, "Scaled x2", Pixel.CYAN, 2);
        e.DrawString(6, y + 50, "Scaled x3", Pixel.GREEN, 3);

        const string sample = "Measured!";
        var size = e.GetTextSizeProp(sample);
        var bx = 6;
        var by = y + 78;
        e.FillRect(bx, by, size.X + 4, size.Y + 4, Pixel.DARK_BLUE);
        e.DrawRect(bx, by, size.X + 4, size.Y + 4, Pixel.BLUE);
        e.DrawStringProp(bx + 2, by + 2, sample, Pixel.WHITE);
        e.DrawString(bx + size.X + 12, by, "GetTextSizeProp boxes it", Pixel.GREY);
    }
}
