namespace PixelSharper.Core.Enums;

/// <summary>
/// Out-of-bounds sampling behaviour when reading a sprite.
/// </summary>
/// <remarks>Port of olc::Sprite::Mode.</remarks>
public enum SpriteDisplayMode : byte
{
    /// <summary>Out-of-bounds samples return a blank pixel.</summary>
    Normal   = 1,

    /// <summary>Coordinates wrap (tile) across the sprite.</summary>
    Periodic = 2,

    /// <summary>Coordinates are clamped to the sprite edges.</summary>
    Clamp    = 4
}
