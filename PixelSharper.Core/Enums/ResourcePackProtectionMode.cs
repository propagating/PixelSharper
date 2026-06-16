namespace PixelSharper.Core.Enums;

/// <summary>
/// Protection applied to data stored in a resource pack.
/// </summary>
public enum ResourcePackProtectionMode : byte
{
    /// <summary>No protection; data stored as-is.</summary>
    None,

    /// <summary>Data is encrypted with a key.</summary>
    Encrypted,

    /// <summary>Data byte order is scrambled.</summary>
    Scrambled
}
