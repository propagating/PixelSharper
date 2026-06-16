using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions.Wire;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;
using Gui = PixelSharper.Core.Extensions.QuickGui;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// QuickGUI — retained-mode widgets (button, checkbox, slider) the user can interact with.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the QuickGUI extension: retained-mode widgets (Label, Button, CheckBox, Slider) the user interacts with.</summary>
/// <remarks>Exercises the QuickGUI PGEX: a <see cref="Gui.Manager"/> drives <see cref="Gui.Label"/>, <see cref="Gui.Button"/>, <see cref="Gui.CheckBox"/>, and <see cref="Gui.Slider"/> widgets, drawn via the GPU decal path.</remarks>
public class QuickGuiScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"QuickGUI"</c>.</value>
    public string Title => "QuickGUI";
    /// <summary>The widget manager driving update/draw.</summary>
    private Gui.Manager _gui = null!;
    /// <summary>A draggable value slider.</summary>
    private Gui.Slider _slider = null!;
    /// <summary>A toggleable checkbox.</summary>
    private Gui.CheckBox _check = null!;
    /// <summary>A clickable button.</summary>
    private Gui.Button _button = null!;

    /// <summary>Builds the GUI manager and its label, button, checkbox, and slider widgets.</summary>
    /// <param name="e">The host showcase engine, queried for canvas metrics to position the widgets.</param>
    public void Initialise(Showcase e)
    {
        _gui = new Gui.Manager();
        _ = new Gui.Label(_gui, "Settings", new Vector2d<float>(20, e.CanvasTop + 8), new Vector2d<float>(110, 12)) { HasBorder = true };
        _button = new Gui.Button(_gui, "Apply", new Vector2d<float>(20, e.CanvasTop + 28), new Vector2d<float>(50, 16));
        _check = new Gui.CheckBox(_gui, "VSync", true, new Vector2d<float>(80, e.CanvasTop + 28), new Vector2d<float>(60, 16));
        _slider = new Gui.Slider(_gui, new Vector2d<float>(20, e.CanvasTop + 58), new Vector2d<float>(160, e.CanvasTop + 58), 0, 100, 50);
    }

    /// <summary>Updates and draws the widgets and reports their current values.</summary>
    /// <param name="e">The host showcase engine used to update/draw the widgets and report their values.</param>
    /// <param name="dt">Seconds since the previous frame (unused; the widgets are driven by input).</param>
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
/// <summary>Finale scene running several systems at once: a wireframe gear, palette-tinted rotating decals, and a palette gradient band.</summary>
/// <remarks>Combines the <see cref="Wireframe"/> extension, the GPU decal path, and the <see cref="Palette"/> utility in one frame.</remarks>
/// <seealso cref="Palette"/>
/// <seealso cref="Wireframe"/>
public class FinaleScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Grand Finale"</c>.</value>
    public string Title => "Grand Finale";
    /// <summary>The checker decal used for the bobbing row.</summary>
    private Decal _decal = null!;
    /// <summary>The spinning wireframe gear.</summary>
    private Model _gear = null!;
    /// <summary>The palette tinting the decals and gradient band.</summary>
    private Palette _pal = null!;
    /// <summary>Accumulated time, driving all animation.</summary>
    private float _t;

    /// <summary>Builds the decal, gear model, and palette.</summary>
    /// <param name="e">The host showcase engine (unused; all resources are built independently of the engine).</param>
    public void Initialise(Showcase e)
    {
        _decal = new Decal(DemoArt.Checker());
        _gear = new Model();
        _gear.SetMesh(Wireframe.MeshGear(12, 26, 17));
        _pal = new Palette(Palette.Stock.ColdHot);
    }

    /// <summary>Draws the palette band, spinning gear, and a row of bobbing palette-tinted decals.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and all drawing.</param>
    /// <param name="dt">Seconds since the previous frame; accumulated into <c>_t</c> to drive every animation in the scene.</param>
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
