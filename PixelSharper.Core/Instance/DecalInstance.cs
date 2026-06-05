using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Actions;

// A reference type so it can be pooled and reused frame-to-frame (see PixelGameEngine's per-layer
// decal pools): Reset() clears the vertex lists without freeing their backing storage.
public class DecalInstance
{
    public Decal Decal;
    public List<Vector2d<float>> Pos = new();
    public List<Vector2d<float>> Uv = new();
    public List<float> W = new();
    public List<float> Z = new();
    public List<Pixel> Tint = new();
    public DecalMode Mode = DecalMode.Normal;
    public DecalStructure Structure = DecalStructure.Fan;
    public uint Points;
    public bool Depth;

    // Reset for reuse: clear the lists (keeping capacity) and restore default scalar state.
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
