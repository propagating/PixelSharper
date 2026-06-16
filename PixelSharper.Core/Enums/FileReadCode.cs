namespace PixelSharper.Core.Enums;

/// <summary>
/// Result code for resource/file read operations.
/// </summary>
/// <remarks>Port of olc::rcode.</remarks>
public enum FileReadCode : sbyte
{
    /// <summary>The requested file does not exist.</summary>
    NO_FILE = -1,

    /// <summary>The operation failed.</summary>
    FAIL    = 0,

    /// <summary>The operation succeeded.</summary>
    OK      = 1,
}
