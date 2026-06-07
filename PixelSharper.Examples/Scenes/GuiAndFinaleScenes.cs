using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions.Wire;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;
using Gui = PixelSharper.Core.Extensions.QuickGui;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// QuickGUI — retained-mode widgets (button, checkbox, slider) the user can interact with.
// ---------------------------------------------------------------------------------------------
public class QuickGuiScene : IExampleScene
{
    public string Title => "QuickGUI";
    private Gui.Manager _gui = null!;
    private Gui.Slider _slider = null!;
    private Gui.CheckBox _check = null!;
    private Gui.Button _button = null!;

    public void Initialise(Showcase e)
    {
        _gui = new Gui.Manager();
        _ = new Gui.Label(_gui, "Settings", new Vector2d<float>(20, e.CanvasTop + 8), new Vector2d<float>(110, 12)) { HasBorder = true };
        _button = new Gui.Button(_gui, "Apply", new Vector2d<float>(20, e.CanvasTop + 28), new Vector2d<float>(50, 16));
        _check = new Gui.CheckBox(_gui, "VSync", true, new Vector2d<float>(80, e.CanvasTop + 28), new Vector2d<float>(60, 16));
        _slider = new Gui.Slider(_gui, new Vector2d<float>(20, e.CanvasTop + 58), new Vector2d<float>(160, e.CanvasTop + 58), 0, 100, 50);
    }

    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "Interactive widgets — click the button, drag the slider.", Pixel.WHITE);
        _gui.Update(e);
        _gui.DrawDecal(e);

        e.DrawStringDecal(new Vector2d<float>(190, e.CanvasTop + 54), $"volume: {(int)_slider.Value}", Pixel.WHITE);
        e.DrawStringDecal(new Vector2d<float>(190, e.CanvasTop + 66), $"vsync : {(_check.Checked ? "on" : "off")}", Pixel.WHITE);
        if (_button.Pressed) e.DrawStringDecal(new Vector2d<float>(190, e.CanvasTop + 30), "applied!", Pixel.GREEN);
    }
}

// ---------------------------------------------------------------------------------------------
// Grand finale — several systems running at once: wireframe + decals + palette.
// ---------------------------------------------------------------------------------------------
public class FinaleScene : IExampleScene
{
    public string Title => "Grand Finale";
    private Decal _decal = null!;
    private Model _gear = null!;
    private Palette _pal = null!;
    private float _t;

    public void Initialise(Showcase e)
    {
        _decal = new Decal(DemoArt.Checker());
        _gear = new Model();
        _gear.SetMesh(Wireframe.MeshGear(12, 26, 17));
        _pal = new Palette(Palette.Stock.ColdHot);
    }

    public void Update(Showcase e, float dt)
    {
        _t += dt;

        // A palette band along the bottom of the canvas.
        for (var i = 0; i < e.ScreenWidth(); i++)
            e.DrawLine(i, e.CanvasBottom - 10, i, e.CanvasBottom, _pal.Sample((double)i / e.ScreenWidth()));

        // A spinning wireframe gear.
        _gear.SetPosition(new Vector2d<float>(58, e.CanvasTop + 52));
        _gear.SetRotation(_t);
        _gear.UpdateInWorld(new Matrix2D());
        Wireframe.DrawModel(e, _gear, Pixel.CYAN);

        // A row of bobbing, rotating, palette-tinted decals.
        for (var k = 0; k < 5; k++)
        {
            var x = 150 + k * 30;
            var y = e.CanvasTop + 50 + MathF.Sin(_t * 2f + k) * 22f;
            e.DrawRotatedDecal(new Vector2d<float>(x, y), _decal, _t + k,
                new Vector2d<float>(4, 4), new Vector2d<float>(3, 3), _pal.Sample(k / 5.0));
        }

        e.DrawStringDecal(new Vector2d<float>(8, e.CanvasTop + 4), "PixelSharper: all systems go.", Pixel.YELLOW);
    }
}
