using System.Runtime.InteropServices;

namespace PixelSharper.Core.Components;

/// <summary>
/// Exactly 4 bytes (matching olc::Pixel): a blittable union of the packed RGBA uint and its
/// component bytes, so a List{Pixel} uploads to a GL texture with no per-frame copy. The
/// per-pixel display mode olc keeps as an enum type, not a field — the engine tracks it separately.
/// </summary>
/// <remarks>
/// <para>
/// The explicit layout overlaps <see cref="N"/> with <see cref="Red"/>/<see cref="Green"/>/
/// <see cref="Blue"/>/<see cref="Alpha"/> at byte offsets 0..3, reproducing the C++
/// <c>union { uint32 n; struct{r,g,b,a}; }</c>. The struct is exactly 4 bytes and blittable.
/// </para>
/// <para>
/// This is load-bearing for performance: the renderer uploads a whole <c>List{Pixel}</c> straight
/// from its backing array with no per-frame copy. Adding a managed field or changing the size would
/// break that zero-copy path.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct Pixel : IEquatable<Pixel>
{

    /// <summary>The whole pixel as a packed 0xAABBGGRR uint, overlapping the component bytes.</summary>
    /// <value>The 32-bit packed colour aliasing <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>/<see cref="Alpha"/>.</value>
    [FieldOffset(0)] public uint N;
    /// <summary>Red channel (byte 0).</summary>
    /// <value>The red component, 0..255.</value>
    [FieldOffset(0)] public byte Red;
    /// <summary>Green channel (byte 1).</summary>
    /// <value>The green component, 0..255.</value>
    [FieldOffset(1)] public byte Green;
    /// <summary>Blue channel (byte 2).</summary>
    /// <value>The blue component, 0..255.</value>
    [FieldOffset(2)] public byte Blue;
    /// <summary>Alpha channel (byte 3).</summary>
    /// <value>The alpha component, 0..255 (255 = opaque).</value>
    [FieldOffset(3)] public byte Alpha;

    /// <summary>Constructs from a packed 0xAABBGGRR uint.</summary>
    /// <param name="pixel">The packed 32-bit colour to assign to <see cref="N"/>.</param>
    public Pixel(uint pixel) : this()
    {
        N = pixel;
    }

    /// <summary>Constructs from normalised 0..1 float channels (olc's PixelF).</summary>
    /// <param name="r">Red channel as a normalised 0..1 value (scaled by 255).</param>
    /// <param name="g">Green channel as a normalised 0..1 value (scaled by 255).</param>
    /// <param name="b">Blue channel as a normalised 0..1 value (scaled by 255).</param>
    /// <param name="a">Alpha channel as a normalised 0..1 value (scaled by 255); defaults to fully opaque.</param>
    /// <seealso cref="Pixel(byte, byte, byte, byte)"/>
    public Pixel(float r, float g, float b, float a = 1.0f) : this()
    {
        Red = (byte)(r * 255.0f);
        Green = (byte)(g * 255.0f);
        Blue = (byte)(b * 255.0f);
        Alpha = (byte)(a * 255.0f);
    }

    /// <summary>Constructs from 0..255 byte channels (alpha defaults to opaque).</summary>
    /// <param name="red">Red channel, 0..255.</param>
    /// <param name="green">Green channel, 0..255.</param>
    /// <param name="blue">Blue channel, 0..255.</param>
    /// <param name="alpha">Alpha channel, 0..255; defaults to <c>0xFF</c> (opaque).</param>
    /// <seealso cref="Pixel(float, float, float, float)"/>
    //TODO: update alpha to use default from CoreConfig Settings
    public Pixel(byte red, byte green, byte blue, byte alpha = 0XFF) : this()
    {
        N = (uint)(red | green << 8 | blue << 16 | alpha << 24);
    }

    /// <summary>Scales RGB by a scalar (clamped 0..255), preserving alpha.</summary>
    /// <param name="a">The pixel whose RGB channels are scaled.</param>
    /// <param name="b">The scalar multiplier applied to each RGB channel.</param>
    /// <returns>A new pixel with RGB scaled and clamped to 0..255, retaining <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator *(Pixel a, float b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red * b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green * b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue * b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    /// <summary>Scales RGB by a scalar (clamped 0..255), preserving alpha.</summary>
    /// <param name="b">The scalar multiplier applied to each RGB channel.</param>
    /// <param name="a">The pixel whose RGB channels are scaled.</param>
    /// <returns>A new pixel with RGB scaled and clamped to 0..255, retaining <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator *(float b, Pixel a)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red * b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green * b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue * b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    /// <summary>Divides RGB by a scalar (clamped 0..255), preserving alpha.</summary>
    /// <param name="a">The pixel whose RGB channels are divided.</param>
    /// <param name="b">The scalar divisor applied to each RGB channel.</param>
    /// <returns>A new pixel with RGB divided and clamped to 0..255, retaining <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator /(Pixel a, float b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red / b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green / b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue / b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }
    
    /// <summary>Divides RGB by a scalar (clamped 0..255), preserving alpha.</summary>
    /// <param name="b">The scalar divisor applied to each RGB channel.</param>
    /// <param name="a">The pixel whose RGB channels are divided.</param>
    /// <returns>A new pixel with RGB divided and clamped to 0..255, retaining <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator /(float b, Pixel a)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red / b));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green / b));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue / b));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    /// <summary>Component-wise normalised multiply including alpha — olc's Pixel::operator*(Pixel).</summary>
    /// <param name="a">The first pixel operand.</param>
    /// <param name="b">The second pixel operand.</param>
    /// <returns>A new pixel where each channel is <c>(a.ch * b.ch) / 255</c>, clamped to 0..255 (used for decal tinting).</returns>
    public static Pixel operator *(Pixel a, Pixel b)
    {
        byte nR = (byte)Math.Min(255.0f, Math.Max(0.0f, a.Red * b.Red / 255.0f));
        byte nG = (byte)Math.Min(255.0f, Math.Max(0.0f, a.Green * b.Green / 255.0f));
        byte nB = (byte)Math.Min(255.0f, Math.Max(0.0f, a.Blue * b.Blue / 255.0f));
        byte nA = (byte)Math.Min(255.0f, Math.Max(0.0f, a.Alpha * b.Alpha / 255.0f));
        return new Pixel(nR, nG, nB, nA);
    }

    /// <summary>Component-wise add of RGB (clamped 0..255), keeping a's alpha.</summary>
    /// <param name="a">The first pixel operand; its alpha is retained in the result.</param>
    /// <param name="b">The second pixel operand.</param>
    /// <returns>A new pixel with summed RGB clamped to 0..255 and <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator +(Pixel a, Pixel b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red + b.Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green + b.Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue + b.Blue));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    /// <summary>Component-wise subtract of RGB (clamped 0..255), keeping a's alpha.</summary>
    /// <param name="a">The minuend pixel; its alpha is retained in the result.</param>
    /// <param name="b">The subtrahend pixel.</param>
    /// <returns>A new pixel with <c>a.ch - b.ch</c> per RGB channel clamped to 0..255 and <paramref name="a"/>'s alpha.</returns>
    public static Pixel operator -(Pixel a, Pixel b)
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, a.Red - b.Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, a.Green - b.Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, a.Blue - b.Blue));
        return new Pixel(nR, nG, nB, a.Alpha);
    }

    /// <summary>Returns the colour-inverted pixel (255 minus each RGB channel), preserving alpha.</summary>
    /// <returns>A new pixel with each RGB channel replaced by <c>255 - channel</c>, retaining this pixel's alpha.</returns>
    public Pixel Inverse()
    {
        byte nR = (byte)Math.Min(255, Math.Max(0, 255 - Red));
        byte nG = (byte)Math.Min(255, Math.Max(0, 255 - Green));
        byte nB = (byte)Math.Min(255, Math.Max(0, 255 - Blue));
        return new Pixel(nR, nG, nB, Alpha);
    }

    /// <summary>Equality by packed value.</summary>
    /// <param name="a">The first pixel operand.</param>
    /// <param name="b">The second pixel operand.</param>
    /// <returns><c>true</c> if both pixels have the same packed <see cref="N"/>; otherwise <c>false</c>.</returns>
    public static bool operator ==(Pixel a, Pixel b)
    {
        return a.N == b.N;
    }

    /// <summary>Inequality by packed value.</summary>
    /// <param name="a">The first pixel operand.</param>
    /// <param name="b">The second pixel operand.</param>
    /// <returns><c>true</c> if the pixels' packed <see cref="N"/> values differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(Pixel a, Pixel b)
    {
        return !(a == b);
    }

    /// <summary>Equality by packed value.</summary>
    /// <param name="p">The pixel to compare against.</param>
    /// <returns><c>true</c> if <paramref name="p"/> has the same packed <see cref="N"/>; otherwise <c>false</c>.</returns>
    public bool Equals(Pixel p)
    {
        return N == p.N;
    }

    /// <summary>Equality against a boxed Pixel.</summary>
    /// <param name="obj">The object to compare against; matched only when it is a boxed <see cref="Pixel"/>.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Pixel"/> with the same packed value; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        return obj is Pixel other && Equals(other);
    }

    /// <summary>Hash of the packed value.</summary>
    /// <returns>The packed <see cref="N"/> reinterpreted as an <c>int</c> hash code.</returns>
    public override int GetHashCode()
    {
        return (int)N;
    }

    /// <summary>Linear blend between two pixels by t in 0..1 (t=0 yields a, t=1 yields b).</summary>
    /// <param name="a">The start pixel, returned when <paramref name="t"/> is 0.</param>
    /// <param name="b">The end pixel, returned when <paramref name="t"/> is 1.</param>
    /// <param name="t">The blend factor in 0..1.</param>
    /// <returns>The interpolated pixel <c>b*t + a*(1-t)</c>.</returns>
    public static Pixel LinearInterpolation(Pixel a, Pixel b, float t)
    {
        return (b * t) + a * (1 - t);
    }


    #region Constants

    /// <summary>Fully transparent (0,0,0,0).</summary>
    public static readonly Pixel BLANK             = new(0  , 0  , 0, 0);
    /// <summary>Light grey (192,192,192).</summary>
    public static readonly Pixel GREY              = new(192, 192, 192);
    /// <summary>Dark grey (128,128,128).</summary>
    public static readonly Pixel DARK_GREY         = new(128, 128, 128);
    /// <summary>Very dark grey (64,64,64).</summary>
    public static readonly Pixel VERY_DARK_GREY    = new(64 , 64 , 64);
    /// <summary>Opaque red (255,0,0).</summary>
    public static readonly Pixel RED               = new(255, 0  , 0);
    /// <summary>Dark red (128,0,0).</summary>
    public static readonly Pixel DARK_RED          = new(128, 0  , 0);
    /// <summary>Very dark red (64,0,0).</summary>
    public static readonly Pixel VERY_DARK_RED     = new(64 , 0  , 0);
    /// <summary>Opaque yellow (255,255,0).</summary>
    public static readonly Pixel YELLOW            = new(255, 255, 0);
    /// <summary>Dark yellow (128,128,0).</summary>
    public static readonly Pixel DARK_YELLOW       = new(128, 128, 0);
    /// <summary>Very dark yellow (64,64,0).</summary>
    public static readonly Pixel VERY_DARK_YELLOW  = new(64 , 64 , 0);
    /// <summary>Opaque green (0,255,0).</summary>
    public static readonly Pixel GREEN             = new(0  , 255, 0);
    /// <summary>Dark green (0,128,0).</summary>
    public static readonly Pixel DARK_GREEN        = new(0  , 128, 0);
    /// <summary>Very dark green (0,64,0).</summary>
    public static readonly Pixel VERY_DARK_GREEN   = new(0  , 64 , 0);
    /// <summary>Opaque cyan (0,255,255).</summary>
    public static readonly Pixel CYAN              = new(0  , 255, 255);
    /// <summary>Dark cyan (0,128,128).</summary>
    public static readonly Pixel DARK_CYAN         = new(0  , 128, 128);
    /// <summary>Very dark cyan (0,64,64).</summary>
    public static readonly Pixel VERY_DARK_CYAN    = new(0  , 64 , 64);
    /// <summary>Opaque blue (0,0,255).</summary>
    public static readonly Pixel BLUE              = new(0  , 0  , 255);
    /// <summary>Dark blue (0,0,128).</summary>
    public static readonly Pixel DARK_BLUE         = new(0  , 0  , 128);
    /// <summary>Very dark blue (0,0,64).</summary>
    public static readonly Pixel VERY_DARK_BLUE    = new(0  , 0  , 64);
    /// <summary>Opaque magenta (255,0,255).</summary>
    public static readonly Pixel MAGENTA           = new(255, 0  , 255);
    /// <summary>Dark magenta (128,0,128).</summary>
    public static readonly Pixel DARK_MAGENTA      = new(128, 0  , 128);
    /// <summary>Very dark magenta (64,0,64).</summary>
    public static readonly Pixel VERY_DARK_MAGENTA = new(64 , 0  , 64);
    /// <summary>Opaque white (255,255,255).</summary>
    public static readonly Pixel WHITE             = new(255, 255, 255);
    /// <summary>Opaque black (0,0,0).</summary>
    public static readonly Pixel BLACK             = new(0  , 0  , 0);
    /// <summary>Opaque tangerine orange (255,165,0).</summary>
    public static readonly Pixel TANGERINE         = new(255, 165, 0);

    #endregion
}
