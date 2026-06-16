namespace PixelSharper.Core.Enums;

/// <summary>
/// Per-pixel blend mode used by the software draw primitives.
/// </summary>
/// <remarks>Port of olc::Pixel::Mode.</remarks>
public enum PixelDisplayMode : byte
{
    /// <summary>Overwrites the destination pixel directly.</summary>
    Normal = 0x01,

    /// <summary>Draws only when the source alpha equals 255 (fully opaque).</summary>
    Mask   = 0x02,

    /// <summary>Alpha-blends the source over the destination.</summary>
    Alpha  = 0x04,

    /// <summary>Uses a user-supplied blend function.</summary>
    Custom = 0x08
}
