namespace PixelSharper.Core.Enums;

/// <summary>
/// Blend/render mode applied when drawing a decal on the GPU.
/// </summary>
/// <remarks>Port of olc::DecalMode.</remarks>
public enum DecalMode : byte
{
    /// <summary>Standard alpha-blended drawing.</summary>
    Normal         = 0x01,

    /// <summary>Source colour is added to the destination.</summary>
    Additive       = 0x02,

    /// <summary>Source colour multiplies the destination.</summary>
    Multiplicative = 0x04,

    /// <summary>Writes to a stencil mask rather than colour.</summary>
    Stencil        = 0x08,

    /// <summary>Modulates destination by the decal as a light source.</summary>
    Illuminate     = 0x10,

    /// <summary>Renders geometry edges only (no fill).</summary>
    Wireframe      = 0x20
}
