using PixelSharper.Core.Components;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// Palette — an interpolated colour ramp, sampled continuously.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the Palette utility: a small stock palette interpolated into a continuous gradient via Sample.</summary>
/// <remarks>Exercises the <see cref="Palette"/> utility: a <see cref="Palette.Stock"/> ramp sampled at continuous <c>t</c> in <c>[0,1]</c>.</remarks>
/// <seealso cref="Palette"/>
public class PaletteScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Palette (interpolated)"</c>.</value>
    public string Title => "Palette (interpolated)";
    /// <summary>The interpolated colour palette being sampled.</summary>
    private Palette _pal = null!;

    /// <summary>Builds a palette from the stock Spectrum ramp.</summary>
    /// <param name="e">The host showcase engine (unused; the palette is built from a stock ramp).</param>
    public void Initialise(Showcase e) => _pal = new Palette(Palette.Stock.Spectrum);

    /// <summary>Draws the palette as a smooth horizontal gradient band.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and drawing the gradient band.</param>
    /// <param name="dt">Seconds since the previous frame (unused; this scene is static).</param>
    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "A small palette interpolated into a smooth gradient.", Pixel.WHITE);
        int x0 = 20, y0 = e.CanvasTop + 26, w = 280, h = 50;
        for (var i = 0; i < w; i++)
            e.DrawLine(x0 + i, y0, x0 + i, y0 + h, _pal.Sample((double)i / w));
        e.DrawRect(x0, y0, w, h, Pixel.WHITE);
        e.DrawString(x0, y0 + h + 8, "Palette.Sample(t)  with Stock.Spectrum", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// Camera2D — a camera that eases to follow a moving target (the view scrolls).
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the Camera2D utility in LazyFollow mode easing to chase a moving target through a scrolling world.</summary>
/// <remarks>Exercises the <see cref="Camera2D"/> utility in <see cref="Camera2D.Mode.LazyFollow"/> mode; world landmarks are projected into screen space relative to the camera's view position.</remarks>
/// <seealso cref="Camera2D"/>
public class Camera2DScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Camera2D (follow)"</c>.</value>
    public string Title => "Camera2D (follow)";
    /// <summary>The follow camera.</summary>
    private Camera2D _cam = null!;
    /// <summary>Static world landmarks rendered relative to the camera.</summary>
    private readonly List<Vector2d<float>> _landmarks = new();
    /// <summary>Accumulated time, driving the target's orbit.</summary>
    private float _t;

    /// <summary>Creates the LazyFollow camera and scatters random landmarks.</summary>
    /// <param name="e">The host showcase engine, queried for screen/canvas size to size the camera and scatter landmarks.</param>
    public void Initialise(Showcase e)
    {
        _cam = new Camera2D(new Vector2d<float>(e.ScreenWidth(), e.CanvasHeight));
        _cam.SetMode(Camera2D.Mode.LazyFollow);
        _landmarks.Clear();
        var rnd = new Random(7);
        for (var i = 0; i < 40; i++) _landmarks.Add(new Vector2d<float>(rnd.Next(-100, 400), rnd.Next(-100, 400)));
    }

    /// <summary>Moves the target, updates the camera, and draws landmarks and target in camera-relative screen space.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and drawing.</param>
    /// <param name="dt">Seconds since the previous frame; accumulated into <c>_t</c> to orbit the target and forwarded to the camera's eased update.</param>
    public void Update(Showcase e, float dt)
    {
        _t += dt;
        e.DrawString(4, e.CanvasTop + 2, "LazyFollow camera chasing a target around a world.", Pixel.WHITE);

        var target = new Vector2d<float>(150 + MathF.Cos(_t) * 130, 150 + MathF.Sin(_t) * 90);
        _cam.SetTarget(target);
        _cam.Update(dt);
        var vp = _cam.GetViewPosition(); // world coordinate at the canvas top-left

        Vector2d<int> ToScreen(Vector2d<float> wp) => new((int)(wp.X - vp.X), (int)(wp.Y - vp.Y) + e.CanvasTop);

        foreach (var l in _landmarks)
        {
            var s = ToScreen(l);
            if (s.Y > e.CanvasTop && s.Y < e.CanvasBottom) e.FillCircle(s.X, s.Y, 2, Pixel.DARK_CYAN);
        }
        var ts = ToScreen(target);
        e.FillCircle(ts.X, ts.Y, 5, Pixel.TANGERINE);
        e.DrawString(4, e.CanvasBottom - 8, "The camera lags, then catches up to the orange target.", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// Geometry2D — shape relations (contains / closest) driven by the mouse.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the Geometry2D utility: Geom2D.Contains and Geom2D.Closest relations driven by the mouse position.</summary>
/// <remarks>Exercises the <see cref="Geom2D"/> shape relations against <see cref="Circle{T}"/> and <see cref="Rect{T}"/> shapes at the live mouse position.</remarks>
/// <seealso cref="Geom2D"/>
public class Geometry2DScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"Geometry2D"</c>.</value>
    public string Title => "Geometry2D";

    /// <summary>No resources to build.</summary>
    /// <param name="e">The host showcase engine (unused; no resources are created).</param>
    public void Initialise(Showcase e) { }

    /// <summary>Tests a circle and rect for mouse containment and draws the closest point on the circle.</summary>
    /// <param name="e">The host showcase engine used to poll the mouse and draw the shapes.</param>
    /// <param name="dt">Seconds since the previous frame (unused; this scene is driven by the mouse).</param>
    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "Contains() tests + Closest() point, against the mouse.", Pixel.WHITE);
        var m = new Vector2d<float>(e.GetMouseX(), e.GetMouseY());

        var circle = new Circle<float>(new Vector2d<float>(100, e.CanvasTop + 60), 32);
        var rect = new Rect<float>(new Vector2d<float>(180, e.CanvasTop + 35), new Vector2d<float>(80, 55));

        e.DrawCircle((int)circle.Pos.X, (int)circle.Pos.Y, (int)circle.Radius, Geom2D.Contains(circle, m) ? Pixel.GREEN : Pixel.CYAN);
        e.DrawRect((int)rect.Pos.X, (int)rect.Pos.Y, (int)rect.Size.X, (int)rect.Size.Y, Geom2D.Contains(rect, m) ? Pixel.GREEN : Pixel.CYAN);

        var cp = Geom2D.Closest(circle, m);
        e.DrawLine(e.GetMouseX(), e.GetMouseY(), (int)cp.X, (int)cp.Y, Pixel.YELLOW);
        e.FillCircle((int)cp.X, (int)cp.Y, 2, Pixel.YELLOW);

        e.DrawString(4, e.CanvasBottom - 8, "Shapes glow green when the mouse is inside them.", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// QuadTree — a spatial index; query the region under the mouse.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the QuadTreeContainer spatial index: inserting points and querying only those within the mouse region.</summary>
/// <remarks>Exercises the <see cref="QuadTreeContainer{T}"/> spatial index: 250 points are inserted, then only those overlapping the mouse query rect are returned.</remarks>
/// <seealso cref="QuadTreeContainer{T}"/>
public class QuadTreeScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"QuadTree (spatial index)"</c>.</value>
    public string Title => "QuadTree (spatial index)";
    /// <summary>The spatial index over the scattered points.</summary>
    private QuadTreeContainer<int> _qt = null!;
    /// <summary>The indexed point positions, keyed by their id.</summary>
    private readonly List<Vector2d<float>> _pts = new();

    /// <summary>Scatters 250 random points and inserts them into the quad tree.</summary>
    /// <param name="e">The host showcase engine, queried for screen/canvas size to bound the quad tree and scatter points.</param>
    public void Initialise(Showcase e)
    {
        _qt = new QuadTreeContainer<int>(new Rect<float>(
            new Vector2d<float>(0, e.CanvasTop), new Vector2d<float>(e.ScreenWidth(), e.CanvasHeight)));
        _pts.Clear();
        var rnd = new Random(1);
        for (var i = 0; i < 250; i++)
        {
            var p = new Vector2d<float>(rnd.Next(e.ScreenWidth()), e.CanvasTop + rnd.Next(e.CanvasHeight));
            _pts.Add(p);
            _qt.Insert(i, new Rect<float>(p, new Vector2d<float>(1, 1)));
        }
    }

    /// <summary>Draws all points and highlights those returned by a quad-tree search of the mouse region.</summary>
    /// <param name="e">The host showcase engine used to poll the mouse, draw the points, and report the hit count.</param>
    /// <param name="dt">Seconds since the previous frame (unused; this scene is driven by the mouse).</param>
    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "250 points indexed; the tree returns only nearby ones.", Pixel.WHITE);
        foreach (var p in _pts) e.Draw((int)p.X, (int)p.Y, Pixel.DARK_GREY);

        var q = new Rect<float>(new Vector2d<float>(e.GetMouseX() - 32, e.GetMouseY() - 32), new Vector2d<float>(64, 64));
        e.DrawRect((int)q.Pos.X, (int)q.Pos.Y, (int)q.Size.X, (int)q.Size.Y, Pixel.YELLOW);
        var hits = _qt.Search(q);
        foreach (var id in hits) e.FillCircle((int)_pts[id].X, (int)_pts[id].Y, 2, Pixel.GREEN);

        e.DrawString(4, e.CanvasBottom - 8, $"Points returned by the query: {hits.Count}", Pixel.GREY);
    }
}

// ---------------------------------------------------------------------------------------------
// DataFile — serialise a key/value tree to disk and read it straight back.
// ---------------------------------------------------------------------------------------------
/// <summary>Demonstrates the DataFile utility: writing a nested key/value tree to disk and reading it straight back.</summary>
/// <remarks>Exercises the <see cref="DataFile"/> utility round-trip via <see cref="DataFile.Write"/> and <see cref="DataFile.Read"/>, including quoting of values containing the list separator.</remarks>
/// <seealso cref="DataFile"/>
public class DataFileScene : IExampleScene
{
    /// <summary>The scene's title.</summary>
    /// <value>The literal <c>"DataFile (serialisation)"</c>.</value>
    public string Title => "DataFile (serialisation)";
    /// <summary>The round-tripped values formatted for display.</summary>
    private string[] _lines = Array.Empty<string>();

    /// <summary>Builds, writes, and re-reads a DataFile, capturing the round-tripped values.</summary>
    /// <param name="e">The host showcase engine (unused; the round-trip uses a temp file independent of the engine).</param>
    public void Initialise(Showcase e)
    {
        var df = new DataFile();
        df["player"]["name"].SetString("Hero");
        df["player"]["score"].SetInt(4200);
        df["player"]["pos"].SetReal(1.5, 0);
        df["player"]["pos"].SetReal(2.5, 1);
        df["title"].SetString("Demo, with a comma");

        var tmp = Path.GetTempFileName();
        DataFile.Write(df, tmp);
        var loaded = new DataFile();
        DataFile.Read(loaded, tmp);
        File.Delete(tmp);

        _lines = new[]
        {
            $"player.name  = {loaded["player"]["name"].GetString()}",
            $"player.score = {loaded["player"]["score"].GetInt()}",
            $"player.pos   = {loaded["player"]["pos"].GetReal(0)}, {loaded["player"]["pos"].GetReal(1)}",
            $"title        = \"{loaded["title"].GetString()}\"",
        };
    }

    /// <summary>Draws the round-tripped key/value lines.</summary>
    /// <param name="e">The host showcase engine used for canvas metrics and drawing the captured lines.</param>
    /// <param name="dt">Seconds since the previous frame (unused; this scene is static).</param>
    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "Wrote a nested key/value tree to disk and read it back:", Pixel.WHITE);
        for (var i = 0; i < _lines.Length; i++) e.DrawString(12, e.CanvasTop + 24 + i * 12, _lines[i], Pixel.CYAN);
        e.DrawString(4, e.CanvasBottom - 8, "Values with the list separator round-trip via quoting.", Pixel.GREY);
    }
}
