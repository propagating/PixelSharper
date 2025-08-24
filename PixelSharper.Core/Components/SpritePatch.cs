using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

public struct SpritePatch
{
    public Sprite Sprite;
    public Vector2d<float>[] Coords;

    public SpritePatch(Sprite sprite)
    {
        Sprite = sprite;
        Coords = new Vector2d<float>[4];
    }
}