using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

/// <summary>A CPU-side image: a flat row-major list of pixels plus sample/mirror modes — olc::Sprite.</summary>
/// <remarks>
/// <para>
/// Pixels are stored row-major in <see cref="PixelData"/> as a blittable <c>List{Pixel}</c>, so the
/// renderer can upload them to a GL texture with no per-frame copy.
/// </para>
/// </remarks>
/// <seealso cref="Decal"/>
public class Sprite
{

    /// <summary>Pixel height of the sprite.</summary>
    /// <value>The sprite's height in pixels.</value>
    public int Height { get; set; }
    /// <summary>Pixel width of the sprite.</summary>
    /// <value>The sprite's width in pixels.</value>
    public int Width { get; set; }
    /// <summary>Row-major pixel storage (length Width*Height); uploaded blittably to GL.</summary>
    /// <value>The backing list of pixels, indexed as <c>y * Width + x</c>.</value>
    public List<Pixel> PixelData { get; set; } = new();
    /// <summary>Injected backend that decodes image files into sprites (test seam).</summary>
    public static ImageLoader ImageLoader;

    /// <summary>Value new sprite cells are initialised to — olc's nDefaultPixel (opaque black, 0xFF000000).</summary>
    public static Pixel DefaultPixel = new(0, 0, 0, 0xFF);

    /// <summary>Out-of-bounds sampling behaviour used by GetPixel/SamplePixel.</summary>
    public SpriteDisplayMode SpriteDisplayMode = SpriteDisplayMode.Normal;
    /// <summary>Horizontal/vertical flip applied when this sprite is drawn.</summary>
    public SpriteMirrorMode SpriteMirrorMode = SpriteMirrorMode.None;

    /// <summary>Creates an empty 0x0 sprite.</summary>
    public Sprite()
    {
        Width = 0;
        Height = 0;
    }
    /// <summary>Creates a sprite by loading an image from a file (optionally within a resource pack).</summary>
    /// <param name="imageFilePath">Path to the image file to load.</param>
    /// <param name="resourcePack">Resource pack to read the file from, or <c>null</c> to read from disk.</param>
    /// <seealso cref="LoadFromFile(string, ResourcePack)"/>
    public Sprite(string imageFilePath, ResourcePack resourcePack)
    {
        LoadFromFile(imageFilePath, resourcePack);
    }

    /// <summary>Creates a blank sprite of the given size, filled with the default pixel.</summary>
    /// <param name="width">Width of the new sprite in pixels.</param>
    /// <param name="height">Height of the new sprite in pixels.</param>
    public Sprite(int width, int height)
    {
        SetSize(width, height);
    }

    /// <summary>Deep-copy constructor (clones pixel data and modes).</summary>
    /// <param name="other">The sprite to clone.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <c>null</c>.</exception>
    public Sprite(Sprite other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        Width = other.Width;
        Height = other.Height;
        SpriteDisplayMode = other.SpriteDisplayMode;
        SpriteMirrorMode = other.SpriteMirrorMode;
        PixelData = new List<Pixel>(other.PixelData);
    }

    /// <summary>Finalizer; releases the pixel list.</summary>
    ~Sprite()
    {
        PixelData.Clear();
    }

    /// <summary>Loads image data from a file (optionally within a resource pack) into this sprite.</summary>
    /// <param name="imageFilePath">Path to the image file to load.</param>
    /// <param name="resourcePack">Resource pack to read the file from, or <c>null</c> to read from disk.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or the failure reason.</returns>
    public FileReadCode LoadFromFile(string imageFilePath, ResourcePack resourcePack)
    {
        return ImageLoader.LoadImageResource(this, imageFilePath, resourcePack);
    }

    /// <summary>Sets the out-of-bounds sampling mode (Normal/Periodic/Clamp).</summary>
    /// <param name="spriteDisplayMode">The out-of-bounds sampling mode to apply; defaults to <see cref="SpriteDisplayMode.Normal"/>.</param>
    /// <seealso cref="GetPixel(int, int)"/>
    public void SetSampleMode(SpriteDisplayMode spriteDisplayMode = SpriteDisplayMode.Normal)
    {
        SpriteDisplayMode = spriteDisplayMode;
    }

    /// <summary>Reads a pixel at integer coords, honouring the current out-of-bounds sample mode.</summary>
    /// <param name="x">The x (column) coordinate.</param>
    /// <param name="y">The y (row) coordinate.</param>
    /// <returns>
    /// The pixel at <paramref name="x"/>,<paramref name="y"/>. Out-of-bounds behaviour follows
    /// <see cref="SpriteDisplayMode"/>: Normal returns transparent black, Periodic wraps, and Clamp
    /// clamps to the edge.
    /// </returns>
    public Pixel GetPixel(int x, int y)
    {
        return SpriteDisplayMode switch
        {
            SpriteDisplayMode.Normal => 
                x >= 0 && x < Width && y >= 0 && y < Height
                ? PixelData[y * Width + x]
                : new Pixel(0, 0, 0, 0),
            SpriteDisplayMode.Periodic => 
                PixelData[int.Abs(y % Height) * Width + int.Abs(x % Width)],
            _ => 
                PixelData[
                Math.Max(0, Math.Min(y, Height - 1)) * Width + Math.Max(0, Math.Min(x, Width - 1))]
        };
    }

    /// <summary>Reads a pixel at a vector position.</summary>
    /// <param name="position">The integer pixel position to read.</param>
    /// <returns>The pixel at <paramref name="position"/>, with out-of-bounds handling per <see cref="SpriteDisplayMode"/>.</returns>
    /// <seealso cref="GetPixel(int, int)"/>
    public Pixel GetPixel(Vector2d<int> position)
    {
        return GetPixel(position.X, position.Y);
    }

    /// <summary>Writes a pixel at integer coords; returns false if out of bounds.</summary>
    /// <param name="x">The x (column) coordinate.</param>
    /// <param name="y">The y (row) coordinate.</param>
    /// <param name="pixel">The pixel value to write.</param>
    /// <returns><c>true</c> if the pixel was written; <c>false</c> if <paramref name="x"/>,<paramref name="y"/> is out of bounds.</returns>
    public bool SetPixel(int x, int y, Pixel pixel)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        PixelData[y * Width + x] = pixel;
        return true;

    }

    /// <summary>Writes a pixel at a vector position; returns false if out of bounds.</summary>
    /// <param name="position">The integer pixel position to write.</param>
    /// <param name="pixel">The pixel value to write.</param>
    /// <returns><c>true</c> if the pixel was written; <c>false</c> if <paramref name="position"/> is out of bounds.</returns>
    /// <seealso cref="SetPixel(int, int, Pixel)"/>
    public bool SetPixel(Vector2d<int> position, Pixel pixel)
    {
        return SetPixel(position.X, position.Y, pixel);

    }

    /// <summary>Nearest-neighbour samples by normalised 0..1 UV coordinates.</summary>
    /// <param name="x">Normalised horizontal coordinate in 0..1.</param>
    /// <param name="y">Normalised vertical coordinate in 0..1.</param>
    /// <returns>The nearest pixel to the given UV.</returns>
    /// <seealso cref="SampleBl(float, float)"/>
    public Pixel SamplePixel(float x, float y)
    {
        var sy = Math.Min((int)(y * Height), Height - 1);
        var sx = Math.Min((int)(x * Width), Width - 1);
        return GetPixel(sx, sy);
    }

    /// <summary>Nearest-neighbour samples by a normalised UV vector.</summary>
    /// <param name="uv">Normalised UV coordinate (each component in 0..1).</param>
    /// <returns>The nearest pixel to the given UV.</returns>
    /// <seealso cref="SamplePixel(float, float)"/>
    public Pixel SamplePixel(Vector2d<float> uv)
    {
        return SamplePixel(uv.X, uv.Y);
    }

    /// <summary>Bilinearly samples by normalised UV coordinates, blending the four nearest texels.</summary>
    /// <param name="u">Normalised horizontal coordinate in 0..1.</param>
    /// <param name="v">Normalised vertical coordinate in 0..1.</param>
    /// <returns>The bilinearly-blended pixel of the four texels surrounding the UV (alpha is opaque).</returns>
    /// <seealso cref="SamplePixel(float, float)"/>
    public Pixel SampleBl(float u, float v)
    {
        u = u * Width - 0.5f;
        v = v * Height - 0.5f;
        var x = (int)Math.Floor(u); 
        var y = (int)Math.Floor(v); 
        var uRatio = u - x;
        var vRatio = v - y;
        var uOpposite = 1 - uRatio;
        var vOpposite = 1 - vRatio;

        var p1 = GetPixel(Math.Max(x, 0), Math.Max(y, 0));
        var p2 = GetPixel(Math.Min(x + 1, Width - 1), Math.Max(y, 0));
        var p3 = GetPixel(Math.Max(x, 0), Math.Min(y + 1, Height - 1));
        var p4 = GetPixel(Math.Min(x + 1, Width - 1), Math.Min(y + 1, Height - 1));

        // Cast to byte (matching olc's uint8_t) so these 0-255 results bind to the byte
        // constructor; an int here would resolve to Pixel(float,...), which re-scales by 255.
        return new Pixel(
            (byte)((p1.Red * uOpposite + p2.Red * uRatio) * vOpposite + (p3.Red * uOpposite + p4.Red * uRatio) * vRatio),
            (byte)((p1.Green * uOpposite + p2.Green * uRatio) * vOpposite + (p3.Green * uOpposite + p4.Green * uRatio) * vRatio),
            (byte)((p1.Blue * uOpposite + p2.Blue * uRatio) * vOpposite + (p3.Blue * uOpposite + p4.Blue * uRatio) * vRatio));
    }

    /// <summary>Bilinearly samples by a normalised UV vector.</summary>
    /// <param name="position">Normalised UV coordinate (each component in 0..1).</param>
    /// <returns>The bilinearly-blended pixel at the given UV.</returns>
    /// <seealso cref="SampleBl(float, float)"/>
    public Pixel SampleBl(Vector2d<float> position)
    {
        return SampleBl(position.X, position.Y);
    }

    /// <summary>Returns the first pixel of the backing store (olc's GetData entry point).</summary>
    /// <returns>The pixel at index 0 of <see cref="PixelData"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="PixelData"/> is empty.</exception>
    public Pixel GetData()
    {
       return PixelData.First();
    }

    /// <summary>Returns a full copy of this sprite.</summary>
    /// <returns>A new sprite with cloned pixel data and the same <see cref="SpriteDisplayMode"/>.</returns>
    /// <seealso cref="Duplicate(Vector2d{int}, Vector2d{int})"/>
    public Sprite Duplicate()
    {
        Sprite newSprite = new(Width, Height);
        for (int i = 0; i < PixelData.Count; i++)
        {
            newSprite.PixelData[i] = PixelData[i];
        }

        newSprite.SpriteDisplayMode = SpriteDisplayMode;
        return newSprite;
    }

    /// <summary>Returns a new sprite copied from a sub-rectangle of this one.</summary>
    /// <param name="position">Top-left pixel of the sub-rectangle to copy.</param>
    /// <param name="size">Width and height of the sub-rectangle.</param>
    /// <returns>A new sprite of <paramref name="size"/> containing the copied region.</returns>
    /// <seealso cref="Duplicate()"/>
    public Sprite Duplicate(Vector2d<int> position, Vector2d<int> size)
    {
        Sprite newSprite = new(size.X, size.Y);
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                newSprite.SetPixel(x,y, GetPixel(position.X+x, position.Y+y));
            }
        }

        return newSprite;
    }

    /// <summary>Returns the sprite dimensions as a vector.</summary>
    /// <returns>A vector of (<see cref="Width"/>, <see cref="Height"/>).</returns>
    public Vector2d<int> Size()
    {
        return new Vector2d<int>(Width, Height);
    }

    /// <summary>Resizes the pixel store (olc's resize): grows with default pixels, truncates when shrinking.</summary>
    /// <param name="w">The new width in pixels.</param>
    /// <param name="h">The new height in pixels.</param>
    public void SetSize(int w, int h)
    {
        Width = w;
        Height = h;

        // Equivalent of olc's pColData.resize(width * height, nDefaultPixel): keep existing
        // cells, append default pixels when growing, truncate when shrinking.
        var count = Math.Max(0, w * h);
        if (count < PixelData.Count)
        {
            PixelData.RemoveRange(count, PixelData.Count - count);
        }
        else if (count > PixelData.Count)
        {
            PixelData.Capacity = count;
            for (var i = PixelData.Count; i < count; i++)
                PixelData.Add(DefaultPixel);
        }
    }
    
    /// <summary>Returns a patch covering the whole sprite (olc's implicit Sprite-to-patch conversion).</summary>
    /// <returns>A <see cref="SpritePatch"/> whose quad spans the entire sprite.</returns>
    /// <seealso cref="Patch(Vector2d{int}, Vector2d{int})"/>
    public SpritePatch ToSpritePatch()
    {
        return Patch(new Vector2d<int>(0, 0), Size());
    }

    /// <summary>Builds a quad patch over a pixel sub-rectangle, with UVs normalised to sprite size.</summary>
    /// <param name="pos">Top-left pixel of the sub-rectangle.</param>
    /// <param name="size">Width and height of the sub-rectangle in pixels.</param>
    /// <returns>A <see cref="SpritePatch"/> with the four corner UVs of the sub-rectangle normalised to sprite size.</returns>
    /// <seealso cref="Patch(Vector2d{float}, Vector2d{float}, Vector2d{float}, Vector2d{float})"/>
    public SpritePatch Patch(Vector2d<int> pos, Vector2d<int> size)
    {
        var patch = new SpritePatch(this);
        var spriteSize = Size().As<float>();
        patch.Coords[0] = new Vector2d<float>(pos.X, pos.Y + size.Y) / spriteSize;
        patch.Coords[1] = new Vector2d<float>(pos.X, pos.Y) / spriteSize;
        patch.Coords[2] = new Vector2d<float>(pos.X + size.X, pos.Y) / spriteSize;
        patch.Coords[3] = new Vector2d<float>(pos.X + size.X, pos.Y + size.Y) / spriteSize;
        return patch;
    }
    
    /// <summary>Builds a patch from four explicit corner UVs (bottom-left, top-left, top-right, bottom-right).</summary>
    /// <param name="pBL">Bottom-left corner UV.</param>
    /// <param name="pTL">Top-left corner UV.</param>
    /// <param name="pTR">Top-right corner UV.</param>
    /// <param name="pBR">Bottom-right corner UV.</param>
    /// <returns>A <see cref="SpritePatch"/> with the four explicit corner UVs.</returns>
    /// <seealso cref="Patch(Vector2d{int}, Vector2d{int})"/>
    public SpritePatch Patch(Vector2d<float> pBL, Vector2d<float> pTL, Vector2d<float> pTR, Vector2d<float> pBR)
    {
        var patch = new SpritePatch(this);
        patch.Coords[0] = pBL;
        patch.Coords[1] = pTL;
        patch.Coords[2] = pTR;
        patch.Coords[3] = pBR;
        return patch;
    }

}
