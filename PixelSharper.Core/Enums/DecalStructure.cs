namespace PixelSharper.Core.Enums;

/// <summary>
/// Primitive topology used to interpret a decal's vertex list.
/// </summary>
/// <remarks>Port of olc::DecalStructure.</remarks>
public enum DecalStructure : byte
{
    /// <summary>Vertices form line segments.</summary>
    Line  = 0x01,

    /// <summary>Vertices form a triangle fan.</summary>
    Fan   = 0x02,

    /// <summary>Vertices form a triangle strip.</summary>
    Strip = 0x04,

    /// <summary>Vertices form a list of independent triangles.</summary>
    List  = 0x08
}
