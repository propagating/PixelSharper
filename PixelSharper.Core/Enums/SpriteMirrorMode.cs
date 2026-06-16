namespace PixelSharper.Core.Enums;

/// <summary>
/// Axis along which a sprite is flipped when drawn.
/// </summary>
/// <remarks>Port of olc::Sprite::Flip.</remarks>
public enum SpriteMirrorMode : byte
{
    /// <summary>No flipping.</summary>
    None       = 0,

    /// <summary>Flip horizontally (mirror left-to-right).</summary>
    Horizontal = 1,

    /// <summary>Flip vertically (mirror top-to-bottom).</summary>
    Vertical   = 2
}
