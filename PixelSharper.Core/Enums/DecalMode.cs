namespace PixelSharper.Core.Enums;

public enum DecalMode : byte
{
    Normal         = 0x01,
    Additive       = 0x02,
    Multiplicative = 0x04,
    Stencil        = 0x08,
    Illuminate     = 0x10,
    Wireframe      = 0x20
}