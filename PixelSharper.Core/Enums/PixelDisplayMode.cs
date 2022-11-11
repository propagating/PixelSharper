using System.ComponentModel;

namespace PixelSharper.Core.Enums;

public enum PixelDisplayMode : byte
{
    Normal = 0x01,
    Mask   = 0x02,
    Alpha  = 0x04,
    Custom = 0x08
}
