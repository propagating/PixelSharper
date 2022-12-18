using System.Runtime.InteropServices;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Components;

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 5)] 
public struct Pixel : IEquatable<Pixel>
{

    [FieldOffset(0)] public uint N;
    [FieldOffset(0)] public byte Red;
    [FieldOffset(1)] public byte Green;
    [FieldOffset(2)] public byte Blue;
    [FieldOffset(3)] public byte Alpha;
    [FieldOffset(4)] public PixelDisplayMode DisplayMode;

    public Pixel(uint pixel) : this()
    {
        N = pixel;
        DisplayMode = PixelDisplayMode.Normal;
    }

    public Pixel(float r, float g, float b, float a = 1.0f) : this()
    {
        Red = (byte)(r * 255.0f);
        Green = (byte)(g * 255.0f);
        Blue = (byte)(b * 255.0f);
        Alpha = (byte)(a * 255.0f);
        DisplayMode = PixelDisplayMode.Normal;
    }

    //TODO: update alpha to use default from CoreConfig Settings
    public Pixel(byte red, byte green, byte blue, byte alpha = 0XFF) : this()
    {
        N = (uint)(red | green << 8 | blue << 16 | alpha << 24);
        DisplayMode = PixelDisplayMode.Normal;
    }

    public static Pixel operator *(Pixel a, float b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red * b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green * b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue * b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    public static Pixel operator *(float b, Pixel a)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red * b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green * b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue * b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    public static Pixel operator /(Pixel a, float b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red / b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green / b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue / b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    public static Pixel operator /(float b, Pixel a)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red / b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green / b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue / b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    public static Pixel operator +(Pixel a, Pixel b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red + b.Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green + b.Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue + b.Blue));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    public static Pixel operator -(Pixel a, Pixel b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red - b.Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green - b.Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue - b.Blue));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    public Pixel Inverse()
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, 255 - Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, 255 - Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, 255 - Blue));
        return new Pixel(nR, nG, nB, Alpha);
    }

    public static bool operator ==(Pixel a, Pixel b)
    {
        return a.N == b.N;
    }

    public static bool operator !=(Pixel a, Pixel b)
    {
        return !(a == b);
    }
    
    public bool Equals(Pixel p)
    {
        return N == p.N;
    }

    public override bool Equals(object? obj)
    {
        return obj is Pixel other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(N, (int)DisplayMode);
    }

    public static Pixel LinearInterpolation(Pixel a, Pixel b, float t)
    {
        return (b * t) + a * (1 - t);
    }


    #region Constants

    public static readonly Pixel BLANK             = new(0  , 0  , 0);
    public static readonly Pixel GREY              = new(192, 192, 192);
    public static readonly Pixel DARK_GREY         = new(128, 128, 128);
    public static readonly Pixel VERY_DARK_GREY    = new(64 , 64 , 64);
    public static readonly Pixel RED               = new(255, 0  , 0);
    public static readonly Pixel DARK_RED          = new(128, 0  , 0);
    public static readonly Pixel VERY_DARK_RED     = new(64 , 0  , 0);
    public static readonly Pixel YELLOW            = new(255, 255, 0);
    public static readonly Pixel DARK_YELLOW       = new(128, 128, 0);
    public static readonly Pixel VERY_DARK_YELLOW  = new(64 , 64 , 0);
    public static readonly Pixel GREEN             = new(0  , 255, 0);
    public static readonly Pixel DARK_GREEN        = new(0  , 128, 0);
    public static readonly Pixel VERY_DARK_GREEN   = new(0  , 64 , 0);
    public static readonly Pixel CYAN              = new(0  , 255, 255);
    public static readonly Pixel DARK_CYAN         = new(0  , 128, 128);
    public static readonly Pixel VERY_DARK_CYAN    = new(0  , 64 , 64);
    public static readonly Pixel BLUE              = new(0  , 0  , 255);
    public static readonly Pixel DARK_BLUE         = new(0  , 0  , 128);
    public static readonly Pixel VERY_DARK_BLUE    = new(0  , 0  , 64);
    public static readonly Pixel MAGENTA           = new(255, 0  , 255);
    public static readonly Pixel DARK_MAGENTA      = new(128, 0  , 128);
    public static readonly Pixel VERY_DARK_MAGENTA = new(64 , 0  , 64);
    public static readonly Pixel WHITE             = new(255, 255, 255);
    public static readonly Pixel BLACK             = new(0  , 0  , 0);

    #endregion
}
