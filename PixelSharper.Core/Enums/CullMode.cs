namespace PixelSharper.Core.Enums;

/// <summary>
/// Triangle face-culling rule for hardware 3D drawing.
/// </summary>
/// <remarks>Port of olc::CullMode.</remarks>
public enum CullMode : byte
{
    /// <summary>No culling; render both front and back faces.</summary>
    None = 0,

    /// <summary>Cull triangles wound clockwise.</summary>
    ClockWise = 1,

    /// <summary>Cull triangles wound counter-clockwise.</summary>
    CounterClockWise = 2
}
