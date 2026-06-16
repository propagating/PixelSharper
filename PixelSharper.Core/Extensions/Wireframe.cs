using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.Wire;

/// <summary>Port of olcPGEX_Wireframe's olc::wire — hierarchical 2D wireframe models built from meshes (lists of local points) and transformed into world space via Matrix2D, with attached children.</summary>
public class Model
{
    /// <summary>Draw flag: render the model's origin marker and axis.</summary>
    public const byte DrawOrigin = 0x01;
    /// <summary>Draw flag: render a node marker at each point.</summary>
    public const byte DrawNodes = 0x02;
    /// <summary>Draw flag: render measure annotations.</summary>
    public const byte DrawMeasures = 0x04;

    /// <summary>Mesh points in local space.</summary>
    private List<Vector2d<float>> _localPoints = new();
    /// <summary>Mesh points transformed into world space.</summary>
    private List<Vector2d<float>> _worldPoints = new();
    /// <summary>Local translation transform.</summary>
    private readonly Matrix2D _matLocalTranslation = new();
    /// <summary>Local rotation transform.</summary>
    private readonly Matrix2D _matLocalRotation = new();
    /// <summary>Combined local rotation*translation transform.</summary>
    private Matrix2D _matLocal = new();
    /// <summary>Combined world transform (local composed with parent).</summary>
    private Matrix2D _matWorld = new();
    /// <summary>Child models attached to this one.</summary>
    private readonly List<Model> _children = new();

    /// <summary>Sets the model's local mesh points and allocates the matching world-point buffer.</summary>
    /// <param name="mesh">The local-space points defining the model outline.</param>
    public void SetMesh(IReadOnlyList<Vector2d<float>> mesh)
    {
        _localPoints = new List<Vector2d<float>>(mesh);
        _worldPoints = new List<Vector2d<float>>(new Vector2d<float>[_localPoints.Count]);
    }

    /// <summary>Sets the model's local rotation and recomposes its local transform.</summary>
    /// <param name="angle">The local rotation in radians.</param>
    public void SetRotation(float angle)
    {
        _matLocalRotation.Rotate(angle);
        _matLocal = _matLocalRotation * _matLocalTranslation;
    }

    /// <summary>Sets the model's local position and recomposes its local transform.</summary>
    /// <param name="position">The local-space translation of the model.</param>
    public void SetPosition(Vector2d<float> position)
    {
        _matLocalTranslation.Translate(position);
        _matLocal = _matLocalRotation * _matLocalTranslation;
    }

    /// <summary>Attaches a child model at the given local position and angle.</summary>
    /// <param name="child">The child model to attach; a <c>null</c> child is ignored.</param>
    /// <param name="position">The child's local position relative to this model.</param>
    /// <param name="angle">The child's local rotation in radians.</param>
    public void Attach(Model child, Vector2d<float> position = default, float angle = 0.0f)
    {
        if (child == null) return;
        child.SetPosition(position);
        child.SetRotation(angle);
        _children.Add(child);
    }

    /// <summary>Transforms a local-space point into world space via the current world matrix.</summary>
    /// <param name="local">The point in this model's local space.</param>
    /// <returns>The point transformed into world space by the current world matrix.</returns>
    public Vector2d<float> LocalToWorld(Vector2d<float> local) => _matWorld * local;
    /// <summary>The model's points in world space (valid after UpdateInWorld).</summary>
    /// <returns>The world-space points, valid after <see cref="UpdateInWorld"/> has run.</returns>
    public IReadOnlyList<Vector2d<float>> GetWorldPoints() => _worldPoints;
    /// <summary>The attached child models.</summary>
    /// <returns>The list of child models attached via <see cref="Attach"/>.</returns>
    public IReadOnlyList<Model> GetChildren() => _children;

    /// <summary>Composes this model's world transform from the parent, projects its points, and recurses into children.</summary>
    /// <param name="matParent">The parent's world transform to compose with this model's local transform.</param>
    public void UpdateInWorld(Matrix2D matParent)
    {
        _matWorld = _matLocal * matParent;
        for (var i = 0; i < _localPoints.Count; i++)
            _worldPoints[i] = _matWorld * _localPoints[i];
        foreach (var child in _children)
            child.UpdateInWorld(_matWorld);
    }
}

/// <summary>Mesh factory helpers + model drawing. A mesh is just a list of local-space points.</summary>
public static class Wireframe
{
    /// <summary>Two pi, used to sweep full circles when generating meshes.</summary>
    private const float TwoPi = 2.0f * 3.14159f;

    /// <summary>Builds a circle mesh of the given radius with the given point count.</summary>
    /// <param name="radius">The circle radius.</param>
    /// <param name="points">The number of points sampled around the circle.</param>
    /// <returns>A list of local-space points forming a circle.</returns>
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

    /// <summary>Builds a rectangle mesh of the given size, offset so the origin sits at the given point.</summary>
    /// <param name="size">The rectangle's width and height.</param>
    /// <param name="offset">The local origin offset (the point within the rectangle that maps to local origin).</param>
    /// <returns>A list of four local-space corner points forming a rectangle.</returns>
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

    /// <summary>Builds a gear mesh alternating between outer and inner radii to form the given number of teeth.</summary>
    /// <param name="teeth">The number of gear teeth.</param>
    /// <param name="outerRadius">The radius at the tooth tips.</param>
    /// <param name="innerRadius">The radius at the tooth roots.</param>
    /// <returns>A list of local-space points forming a toothed gear outline.</returns>
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

    /// <summary>Draws a model's world wireframe straight to the engine (world == screen space), recursing into children. Node/origin markers use a fixed 3-pixel radius.</summary>
    /// <param name="pge">The engine to draw into; world coordinates are treated as screen coordinates.</param>
    /// <param name="model">The model whose world points are drawn.</param>
    /// <param name="col">The line colour; defaults to <see cref="Pixel.BLACK"/> when <c>null</c>.</param>
    /// <param name="flags">Bitmask of draw flags (<see cref="Model.DrawNodes"/>, <see cref="Model.DrawOrigin"/>, <see cref="Model.DrawMeasures"/>); defaults to all set.</param>
    /// <seealso cref="DrawModel(TransformedView, Model, Pixel?, byte)"/>
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

    /// <summary>View-aware overload: draws in world space through a TransformedView, with node/origin radii sized in world units via ScaleToWorld (this is olc's intended target for DrawModel).</summary>
    /// <param name="view">The pan/zoom camera used to map world space to screen.</param>
    /// <param name="model">The model whose world points are drawn.</param>
    /// <param name="col">The line colour; defaults to <see cref="Pixel.BLACK"/> when <c>null</c>.</param>
    /// <param name="flags">Bitmask of draw flags (<see cref="Model.DrawNodes"/>, <see cref="Model.DrawOrigin"/>, <see cref="Model.DrawMeasures"/>); defaults to all set.</param>
    /// <seealso cref="DrawModel(PixelGameEngine, Model, Pixel?, byte)"/>
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
