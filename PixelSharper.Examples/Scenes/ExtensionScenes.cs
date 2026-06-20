using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Extensions.Wire;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Hardware3D;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// TransformedView — a pan/zoom camera; world-space draws stay anchored as you move the view.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the TransformedView extension: a pan/zoom camera with world-space draws via HandlePanAndZoom.</summary>
/// <remarks>Exercises the <see cref="TransformedView"/> PGEX: world-space drawing wrappers anchored under a pannable, zoomable camera.</remarks>
/// <seealso cref="TransformedView"/>
public class TransformedViewScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"TransformedView (pan/zoom)"</c>.</value>
    public string Title => "TransformedView (pan/zoom)";
    /// <summary>The pan/zoom camera view.</summary>
    private TransformedView _view = null!;

    /// <summary>Creates the view sized to the screen.</summary>
    /// <param name="e">The host showcase engine, queried for the screen size used to initialise the view.</param>
    public void Initialise(Showcase e)
    {
        _view = new TransformedView();
        _view.Initialise(new Vector2d<int>(e.ScreenWidth(), e.ScreenHeight()));
    }

    /// <summary>Handles pan/zoom input and draws a world-space grid and shapes.</summary>
    /// <param name="e">The host showcase engine used for the instruction text.</param>
    /// <param name="dt">Seconds since the previous frame (unused; the view is driven by input, not time).</param>
    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "Middle-drag to pan, wheel to zoom. Draws are world-space.", Pixel.WHITE);
        _view.HandlePanAndZoom();

        for (var g = 0; g <= 10; g++)
        {
            _view.DrawLine(new Vector2d<float>(g * 10, 0), new Vector2d<float>(g * 10, 100), Pixel.DARK_GREY);
            _view.DrawLine(new Vector2d<float>(0, g * 10), new Vector2d<float>(100, g * 10), Pixel.DARK_GREY);
        }
        _view.DrawCircle(new Vector2d<float>(50, 50), 25, Pixel.GREEN);
        _view.FillCircle(new Vector2d<float>(20, 20), 8, Pixel.TANGERINE);
        _view.DrawString(new Vector2d<float>(12, 54), "world space", Pixel.WHITE, new Vector2d<float>(1, 1));
    }
}

// ---------------------------------------------------------------------------------------------
// Wireframe — hierarchical 2D wireframe models (a spinning gear).
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the Wireframe extension: a hierarchical 2D model (gear mesh) positioned, rotated, and drawn as line loops.</summary>
/// <remarks>Exercises the <see cref="Wireframe"/> PGEX: a <see cref="Model"/> built from a generated gear mesh and drawn through a <see cref="Matrix2D"/> world transform.</remarks>
/// <seealso cref="Wireframe"/>
/// <seealso cref="Model"/>
public class WireframeScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Wireframe Models"</c>.</value>
    public string Title => "Wireframe Models";
    /// <summary>The gear wireframe model.</summary>
    private Model _gear = null!;
    /// <summary>Accumulated time, driving the rotation.</summary>
    private float _t;

    /// <summary>Builds the gear model from a generated gear mesh.</summary>
    /// <param name="e">The host showcase engine (unused; the model is built independently of the engine).</param>
    public void Initialise(Showcase e)
    {
        _gear = new Model();
        _gear.SetMesh(Wireframe.MeshGear(10, 36, 24));
    }

    /// <summary>Positions, rotates, and draws the spinning gear model.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and to draw the model.</param>
    /// <param name="dt">Seconds since the previous frame; accumulated into <c>_t</c> to drive the rotation.</param>
    public void Update(Showcase e, float dt)
    {
        _t += dt;
        e.DrawString(4, e.CanvasTop + 2, "A gear mesh, positioned + rotated, drawn as line loops.", Pixel.WHITE);
        _gear.SetPosition(new Vector2d<float>(e.ScreenWidth() / 2f, (e.CanvasTop + e.CanvasBottom) / 2f));
        _gear.SetRotation(_t);
        _gear.UpdateInWorld(new Matrix2D());
        Wireframe.DrawModel(e, _gear, Pixel.CYAN);
    }
}

// ---------------------------------------------------------------------------------------------
// GFX2D — affine-transform blit (rotate/scale) of a sprite.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the GFX2D extension: an affine Transform2D (rotate + scale) back-sampling a sprite.</summary>
/// <remarks>Exercises the <see cref="GFX2D"/> PGEX: a chained <c>Transform2D</c> (translate, rotate, scale, translate) drives a back-sampling sprite blit.</remarks>
/// <seealso cref="GFX2D"/>
public class Gfx2dScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"GFX2D - Affine Sprites"</c>.</value>
    public string Title => "GFX2D — Affine Sprites";
    /// <summary>The checker sprite being transformed.</summary>
    private Sprite _sprite = null!;
    /// <summary>Accumulated time, driving the rotation.</summary>
    private float _t;

    /// <summary>Loads the shared checker sprite.</summary>
    /// <param name="e">The host showcase engine (unused; the sprite comes from <see cref="DemoArt.Checker"/>).</param>
    public void Initialise(Showcase e) => _sprite = DemoArt.Checker();

    /// <summary>Builds a rotate/scale/translate transform and blits the sprite through it.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and the instruction text.</param>
    /// <param name="dt">Seconds since the previous frame; accumulated into <c>_t</c> to drive the rotation.</param>
    public void Update(Showcase e, float dt)
    {
        _t += dt;
        e.DrawString(4, e.CanvasTop + 2, "A 2D affine transform (rotate + scale) back-samples a sprite.", Pixel.WHITE);
        var tf = new GFX2D.Transform2D();
        tf.Translate(-4, -4);
        tf.Rotate(_t);
        tf.Scale(7, 7);
        tf.Translate(e.ScreenWidth() / 2f, (e.CanvasTop + e.CanvasBottom) / 2f);
        GFX2D.DrawSprite(_sprite, tf);
    }
}

// ---------------------------------------------------------------------------------------------
// HW3D — a hardware-accelerated, textured, depth-tested 3D cube via a Camera3D.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the Hardware3D path: uploading a textured cube mesh and Camera3D matrices for GPU-rasterised, depth-tested 3D.</summary>
/// <remarks>Exercises the <c>HW3D_*</c> engine API together with the <c>Hardware3D</c> <see cref="Mesh"/> (built by <see cref="Hw3d"/>) and <see cref="Camera3D"/> utilities; the GPU performs the depth-tested rasterisation.</remarks>
/// <seealso cref="Camera3D"/>
/// <seealso cref="Mesh"/>
public class Hw3dScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"HW3D - Hardware 3D"</c>.</value>
    public string Title => "HW3D — Hardware 3D";
    /// <summary>The texture decal applied to the cube.</summary>
    private Decal _decal = null!;
    /// <summary>The centred cube mesh.</summary>
    private Mesh _cube = null!;
    /// <summary>The 3D camera supplying projection/view matrices.</summary>
    private Camera3D _cam = null!;
    /// <summary>Accumulated time, driving the rotation.</summary>
    private float _t;

    /// <summary>Builds the cube decal/mesh and configures the 3D camera.</summary>
    /// <param name="e">The host showcase engine, queried for the screen size used to configure the camera.</param>
    public void Initialise(Showcase e)
    {
        _decal = new Decal(DemoArt.Checker());
        _cube = Hw3d.CreateCube(new Vector3d(1, 1, 1), new Vector3d(-0.5f, -0.5f, -0.5f)); // centred
        _cam = new Camera3D();
        _cam.SetFieldOfView(MathF.PI / 2.5f);
        _cam.SetScreenSize(new Vector2d<int>(e.ScreenWidth(), e.ScreenHeight()));
        _cam.SetPosition(0, 0, -3);
        _cam.SetTarget(0, 0, 0);
        _cam.Update();
    }

    /// <summary>Enables depth testing, sets projection/model-view matrices, and draws the rotating cube.</summary>
    /// <param name="e">The host showcase engine used for the instruction text and the <c>HW3D_*</c> draw calls.</param>
    /// <param name="dt">Seconds since the previous frame; accumulated into <c>_t</c> to drive the cube's rotation.</param>
    public void Update(Showcase e, float dt)
    {
        _t += dt;
        e.DrawString(4, e.CanvasTop + 2, "Upload a mesh + camera matrices; the GPU rasterises it.", Pixel.WHITE);

        e.HW3D_EnableDepthTest();
        e.HW3D_SetCullMode(CullMode.None);
        e.HW3D_Projection(_cam.GetProjectionMatrix().ToArray());

        var model = Matrix4x4.RotateY(_t) * Matrix4x4.RotateX(_t * 0.6f);
        var modelView = _cam.GetViewMatrix() * model;
        e.HW3D_DrawObject(modelView.ToArray(), _decal, _cube.Layout, _cube.Pos, _cube.Uv, _cube.Col);
    }
}
