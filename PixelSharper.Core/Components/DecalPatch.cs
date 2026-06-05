using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

public class DecalPatch
{
    public Decal Decal { get; set; }
    // olc's DecalPatch holds a fixed std::array<vf2d, 4>; allocate it so callers can index it
    // (Decal.Patch writes Coordinates[0..3]).
    public Vector2d<float>[] Coordinates { get; set; } = new Vector2d<float>[4];
}