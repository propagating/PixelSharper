using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

/// <summary>A sprite plus four corner UV coordinates, used to draw textured quads/patches.</summary>
/// <seealso cref="Sprite"/>
/// <seealso cref="Vector2d{T}"/>
public struct SpritePatch
{
    /// <summary>The source sprite to sample.</summary>
    public Sprite Sprite;
    /// <summary>The four corner texture coordinates of the patch.</summary>
    public Vector2d<float>[] Coords;

    /// <summary>Wraps a sprite and allocates its 4-element corner-coordinate array.</summary>
    /// <param name="sprite">The source sprite the patch samples from.</param>
    public SpritePatch(Sprite sprite)
    {
        Sprite = sprite;
        Coords = new Vector2d<float>[4];
    }
}