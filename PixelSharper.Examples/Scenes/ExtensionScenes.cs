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
public class TransformedViewScene : IExampleScene
{
    public string Title => "TransformedView (pan/zoom)";
    private TransformedView _view = null!;

    public void Initialise(Showcase e)
    {
        _view = new TransformedView();
        _view.Initialise(new Vector2d<int>(e.ScreenWidth(), e.ScreenHeight()));
    }

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
public class WireframeScene : IExampleScene
{
    public string Title => "Wireframe Models";
    private Model _gear = null!;
    private float _t;

    public void Initialise(Showcase e)
    {
        _gear = new Model();
        _gear.SetMesh(Wireframe.MeshGear(10, 36, 24));
    }

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
public class Gfx2dScene : IExampleScene
{
    public string Title => "GFX2D — Affine Sprites";
    private Sprite _sprite = null!;
    private float _t;

    public void Initialise(Showcase e) => _sprite = DemoArt.Checker();

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
public class Hw3dScene : IExampleScene
{
    public string Title => "HW3D — Hardware 3D";
    private Decal _decal = null!;
    private Mesh _cube = null!;
    private Camera3D _cam = null!;
    private float _t;

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

    public void Update(Showcase e, float dt)
    {
        _t += dt;
        e.DrawString(4, e.CanvasTop + 2, "Upload a mesh + camera matrices; the GPU rasterises it.", Pixel.WHITE);

        e.HW3D_EnableDepthTest();
        e.HW3D_SetCullMode(CullMode.NONE);
        e.HW3D_Projection(_cam.GetProjectionMatrix().ToArray());

        var model = Matrix4x4.RotateY(_t) * Matrix4x4.RotateX(_t * 0.6f);
        var modelView = _cam.GetViewMatrix() * model;
        e.HW3D_DrawObject(modelView.ToArray(), _decal, _cube.Layout, _cube.Pos, _cube.Uv, _cube.Col);
    }
}
