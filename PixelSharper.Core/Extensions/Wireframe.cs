using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.Wire;

// Port of olcPGEX_Wireframe's olc::wire — hierarchical 2D wireframe models built from meshes
// (lists of local points) and transformed into world space via Matrix2D, with attached children.
public class Model
{
    public const byte DrawOrigin = 0x01;
    public const byte DrawNodes = 0x02;
    public const byte DrawMeasures = 0x04;

    private List<Vector2d<float>> _localPoints = new();
    private List<Vector2d<float>> _worldPoints = new();
    private readonly Matrix2D _matLocalTranslation = new();
    private readonly Matrix2D _matLocalRotation = new();
    private Matrix2D _matLocal = new();
    private Matrix2D _matWorld = new();
    private readonly List<Model> _children = new();

    public void SetMesh(IReadOnlyList<Vector2d<float>> mesh)
    {
        _localPoints = new List<Vector2d<float>>(mesh);
        _worldPoints = new List<Vector2d<float>>(new Vector2d<float>[_localPoints.Count]);
    }

    public void SetRotation(float angle)
    {
        _matLocalRotation.Rotate(angle);
        _matLocal = _matLocalRotation * _matLocalTranslation;
    }

    public void SetPosition(Vector2d<float> position)
    {
        _matLocalTranslation.Translate(position);
        _matLocal = _matLocalRotation * _matLocalTranslation;
    }

    public void Attach(Model child, Vector2d<float> position = default, float angle = 0.0f)
    {
        if (child == null) return;
        child.SetPosition(position);
        child.SetRotation(angle);
        _children.Add(child);
    }

    public Vector2d<float> LocalToWorld(Vector2d<float> local) => _matWorld * local;
    public IReadOnlyList<Vector2d<float>> GetWorldPoints() => _worldPoints;
    public IReadOnlyList<Model> GetChildren() => _children;

    public void UpdateInWorld(Matrix2D matParent)
    {
        _matWorld = _matLocal * matParent;
        for (var i = 0; i < _localPoints.Count; i++)
            _worldPoints[i] = _matWorld * _localPoints[i];
        foreach (var child in _children)
            child.UpdateInWorld(_matWorld);
    }
}

// Mesh factory helpers + model drawing. A mesh is just a list of local-space points.
public static class Wireframe
{
    private const float TwoPi = 2.0f * 3.14159f;

    public static List<Vector2d<float>> MeshCircle(float radius, int points = 100)
    {
        var m = new List<Vector2d<float>>(points);
        for (var i = 0; i < points; i++)
        {
            var theta = i / (float)points * TwoPi;
            m.Add(new Vector2d<float>(MathF.Cos(theta) * radius, MathF.Sin(theta) * radius));
        }
        return m;
    }

    public static List<Vector2d<float>> MeshRectangle(Vector2d<float> size, Vector2d<float> offset = default)
    {
        return new List<Vector2d<float>>
        {
            new(-offset.X, -offset.Y),
            new(-offset.X + size.X, -offset.Y),
            new(-offset.X + size.X, -offset.Y + size.Y),
            new(-offset.X, -offset.Y + size.Y)
        };
    }

    public static List<Vector2d<float>> MeshGear(int teeth, float outerRadius, float innerRadius)
    {
        var m = new List<Vector2d<float>>(teeth * 4);
        for (var i = 0; i < teeth * 4; i++)
        {
            var theta = i / (float)(teeth * 4) * TwoPi;
            var rad = 2.0f * (((i / 2) % 2) != 0 ? outerRadius : innerRadius);
            m.Add(new Vector2d<float>(MathF.Cos(theta) * rad, MathF.Sin(theta) * rad));
        }
        return m;
    }

    // Draws a model's world wireframe straight to the engine (world == screen space). When the
    // TransformedView extension lands, a view-aware overload can use its ScaleToWorld for the
    // node/origin radii; here they use a fixed 3-pixel radius.
    public static void DrawModel(PixelGameEngine pge, Model model, Pixel? col = null, byte flags = 0xFF)
    {
        var c = col ?? Pixel.BLACK;
        var points = model.GetWorldPoints();
        for (var i = 0; i < points.Count; i++)
            pge.DrawLine(points[i].As<int>(), points[(i + 1) % points.Count].As<int>(), c);

        if ((flags & Model.DrawNodes) != 0)
            for (var i = 0; i < points.Count; i++)
                pge.FillCircle(points[i].As<int>(), 3, Pixel.RED);

        if ((flags & Model.DrawOrigin) != 0)
        {
            pge.FillCircle(model.LocalToWorld(new Vector2d<float>(0, 0)).As<int>(), 3, Pixel.BLUE);
            pge.DrawLine(model.LocalToWorld(new Vector2d<float>(0, 0)).As<int>(),
                model.LocalToWorld(new Vector2d<float>(10, 0)).As<int>(), Pixel.BLUE);
        }

        foreach (var child in model.GetChildren())
            DrawModel(pge, child, c, flags);
    }

    // View-aware overload: draws in world space through a TransformedView, with node/origin radii
    // sized in world units via ScaleToWorld (this is olc's intended target for DrawModel).
    public static void DrawModel(TransformedView view, Model model, Pixel? col = null, byte flags = 0xFF)
    {
        var c = col ?? Pixel.BLACK;
        var points = model.GetWorldPoints();
        for (var i = 0; i < points.Count; i++)
            view.DrawLine(points[i], points[(i + 1) % points.Count], c);

        if ((flags & Model.DrawNodes) != 0)
            for (var i = 0; i < points.Count; i++)
                view.FillCircle(points[i], view.ScaleToWorld(new Vector2d<float>(3, 3)).X, Pixel.RED);

        if ((flags & Model.DrawOrigin) != 0)
        {
            view.FillCircle(model.LocalToWorld(new Vector2d<float>(0, 0)), view.ScaleToWorld(new Vector2d<float>(3, 3)).X, Pixel.BLUE);
            view.DrawLine(model.LocalToWorld(new Vector2d<float>(0, 0)),
                model.LocalToWorld(view.ScaleToWorld(new Vector2d<float>(10, 0))), Pixel.BLUE);
        }

        foreach (var child in model.GetChildren())
            DrawModel(view, child, c, flags);
    }
}
