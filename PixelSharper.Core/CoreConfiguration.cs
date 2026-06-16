namespace PixelSharper.Core;


/// <summary>
/// Engine-wide constants exposed as <c>PixelGameEngine.Configuration</c>. Replaces olc's
/// scattered <c>#define</c>s with a single immutable config object.
/// </summary>
/// <param name="TotalMouseButtons">The number of mouse buttons the engine tracks.</param>
/// <param name="DefaultAlpha">The default alpha component for pixels (typically fully opaque).</param>
/// <param name="SpacesPerTab">The number of spaces a tab character expands to in text drawing.</param>
/// <param name="MaxVertices">The maximum number of vertices a single decal draw may emit.</param>
public readonly record struct PixelConfiguration(byte TotalMouseButtons,
                                                 byte DefaultAlpha,
                                                 byte SpacesPerTab,
                                                 nuint MaxVertices)
{
    /// <summary>
    /// The default packed RGBA pixel value, built from <see cref="DefaultAlpha"/> shifted into the
    /// high byte (RGB left at zero).
    /// </summary>
    /// <value>The default pixel as a packed 32-bit RGBA value.</value>
    public uint DefaultPixel => unchecked((uint)( DefaultAlpha << 24));
}
