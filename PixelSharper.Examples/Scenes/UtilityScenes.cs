using PixelSharper.Core.Components;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Examples.Scenes;

// ---------------------------------------------------------------------------------------------
// Palette — an interpolated colour ramp, sampled continuously.
// ---------------------------------------------------------------------------------------------
public class PaletteScene : IExampleScene
{
    public string Title => "Palette (interpolated)";
    private Palette _pal = null!;

    public void Initialise(Showcase e) => _pal = new Palette(Palette.Stock.Spectrum);

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
public class Camera2DScene : IExampleScene
{
    public string Title => "Camera2D (follow)";
    private Camera2D _cam = null!;
    private readonly List<Vector2d<float>> _landmarks = new();
    private float _t;

    public void Initialise(Showcase e)
    {
        _cam = new Camera2D(new Vector2d<float>(e.ScreenWidth(), e.CanvasHeight));
        _cam.SetMode(Camera2D.Mode.LazyFollow);
        _landmarks.Clear();
        var rnd = new Random(7);
        for (var i = 0; i < 40; i++) _landmarks.Add(new Vector2d<float>(rnd.Next(-100, 400), rnd.Next(-100, 400)));
    }

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
public class Geometry2DScene : IExampleScene
{
    public string Title => "Geometry2D";

    public void Initialise(Showcase e) { }

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
public class QuadTreeScene : IExampleScene
{
    public string Title => "QuadTree (spatial index)";
    private QuadTreeContainer<int> _qt = null!;
    private readonly List<Vector2d<float>> _pts = new();

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
public class DataFileScene : IExampleScene
{
    public string Title => "DataFile (serialisation)";
    private string[] _lines = Array.Empty<string>();

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

    public void Update(Showcase e, float dt)
    {
        e.DrawString(4, e.CanvasTop + 2, "Wrote a nested key/value tree to disk and read it back:", Pixel.WHITE);
        for (var i = 0; i < _lines.Length; i++) e.DrawString(12, e.CanvasTop + 24 + i * 12, _lines[i], Pixel.CYAN);
        e.DrawString(4, e.CanvasBottom - 8, "Values with the list separator round-trip via quoting.", Pixel.GREY);
    }
}
