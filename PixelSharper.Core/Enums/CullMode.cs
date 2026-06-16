namespace PixelSharper.Core.Enums;

/// <summary>
/// Triangle face-culling rule for hardware 3D drawing.
/// </summary>
/// <remarks>Port of olc::CullMode.</remarks>
public enum CullMode : byte
{
    /// <summary>No culling; render both front and back faces.</summary>
    NONE = 0,

    /// <summary>Cull triangles wound clockwise.</summary>
    CW = 1,

    /// <summary>Cull triangles wound counter-clockwise.</summary>
    CCW = 2
}
