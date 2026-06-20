using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Actions;

/// <summary>
/// One queued decal draw (geometry + texture + state) flushed to the renderer each frame.
/// A reference type so it can be pooled/reused frame-to-frame; Reset() clears the vertex
/// lists without freeing their backing storage. Port of olc::DecalInstance.
/// </summary>
/// <remarks>
/// <para>
/// Instances live in a per-layer pool with a live-count field; submit methods rent an entry
/// and the core loop calls <see cref="Reset"/> to recycle it rather than re-allocating.
/// </para>
/// </remarks>
/// <seealso cref="Decal"/>
public class DecalInstance
{
    /// <summary>The decal (texture) to draw, or null for a solid-colour quad.</summary>
    public Decal? Decal;
    /// <summary>Screen-space vertex positions.</summary>
    public List<Vector2d<float>> Pos = new();
    /// <summary>Per-vertex texture coordinates.</summary>
    public List<Vector2d<float>> Uv = new();
    /// <summary>Per-vertex projective W component (for warped decals).</summary>
    public List<float> W = new();
    /// <summary>Per-vertex depth values.</summary>
    public List<float> Z = new();
    /// <summary>Per-vertex tint colours.</summary>
    public List<Pixel> Tint = new();
    /// <summary>Blend/decal mode for this draw.</summary>
    public DecalMode Mode = DecalMode.Normal;
    /// <summary>Primitive assembly mode (fan/strip/list).</summary>
    public DecalStructure Structure = DecalStructure.Fan;
    /// <summary>Number of vertices in this instance.</summary>
    public uint Points;
    /// <summary>Whether depth testing applies to this draw.</summary>
    public bool Depth;

    /// <summary>Resets for reuse: clears the lists (keeping capacity) and restores default scalar state.</summary>
    public void Reset()
    {
        Decal = null;
        Pos.Clear();
        Uv.Clear();
        W.Clear();
        Z.Clear();
        Tint.Clear();
        Mode = DecalMode.Normal;
        Structure = DecalStructure.Fan;
        Points = 0;
        Depth = false;
    }
}
