using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

/// <summary>
/// A decal plus four UV-style corner coordinates used for patch-based decal drawing.
/// Port of olc::DecalStructure-style patch data.
/// </summary>
public class DecalPatch
{
    /// <summary>The <see cref="Components.Decal"/> textured by this patch.</summary>
    /// <value>The decal whose texture supplies the patch's pixels.</value>
    public Decal Decal { get; set; }

    /// <summary>
    /// The four corner coordinates of the patch. olc's DecalPatch holds a fixed
    /// <c>std::array&lt;vf2d, 4&gt;</c>; this is pre-allocated as a 4-element array so callers can
    /// index it directly (<c>Decal.Patch</c> writes <c>Coordinates[0..3]</c>).
    /// </summary>
    /// <value>A 4-element array of float corner positions.</value>
    public Vector2d<float>[] Coordinates { get; set; } = new Vector2d<float>[4];
}
