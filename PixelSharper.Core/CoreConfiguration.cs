namespace PixelSharper.Core;


public readonly record struct PixelConfiguration(byte TotalMouseButtons, 
                                                 byte DefaultAlpha, 
                                                 byte SpacesPerTab, 
                                                 nuint MaxVertices)
{ 
    public uint DefaultPixel => unchecked((uint)( DefaultAlpha << 24));
}
