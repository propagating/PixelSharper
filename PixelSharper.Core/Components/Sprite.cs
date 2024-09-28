using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

public class Sprite
{

    public int Height { get; set; }
    public int Width { get; set; }
    public List<Pixel> PixelData { get; set; } = new();
    public static ImageLoader ImageLoader;
    
    public SpriteDisplayMode SpriteDisplayMode = SpriteDisplayMode.Normal;
    public SpriteMirrorMode SpriteMirrorMode = SpriteMirrorMode.None;
    
    public Sprite()
    {
        Width = 0;
        Height = 0;
    }
    public Sprite(string imageFilePath, ResourcePack resourcePack)
    {
        LoadFromFile(imageFilePath, resourcePack);
    }

    public Sprite(int width, int height)
    {
        SetSize(width, height);
    }
    
    private Sprite(Sprite other)
    {
        throw new InvalidOperationException("Copy constructor is not allowed.");
    }

    ~Sprite()
    {
        PixelData.Clear();
    }

    public FileReadCode LoadFromFile(string imageFilePath, ResourcePack resourcePack)
    {
        return ImageLoader.LoadImageResource(this, imageFilePath, resourcePack);
    }

    public void SetSampleMode(SpriteDisplayMode spriteDisplayMode = SpriteDisplayMode.Normal)
    {
        SpriteDisplayMode = spriteDisplayMode;
    }

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

    public Pixel GetPixel(Vector2d<int> position)
    {
        return GetPixel(position.X, position.Y);
    }

    public bool SetPixel(int x, int y, Pixel pixel)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        PixelData[y * Width + x] = pixel;
        return true;

    }

    public bool SetPixel(Vector2d<int> position, Pixel pixel)
    {
        return SetPixel(position.X, position.Y, pixel);
    
    }

    public Pixel SamplePixel(float x, float y)
    {
        var sy = Math.Min((int)(y * Height), Height - 1);
        var sx = Math.Min((int)(x * Width), Width - 1);
        return GetPixel(sx, sy);
    }

    public Pixel SamplePixel(Vector2d<float> uv)
    {
        return SamplePixel(uv.X, uv.Y);
    }

    public Pixel SampleBl(float u, float v)
    {
        u = u * Width - 0.5f;
        v = v * Height - 0.5f;
        var x = (int)Math.Floor(u); // cast to int rounds toward zero, not downward
        var y = (int)Math.Floor(v); // Thanks @joshinils
        var uRatio = u - x;
        var vRatio = v - y;
        var uOpposite = 1 - uRatio;
        var vOpposite = 1 - vRatio;

        var p1 = GetPixel(Math.Max(x, 0), Math.Max(y, 0));
        var p2 = GetPixel(Math.Min(x + 1, Width - 1), Math.Max(y, 0));
        var p3 = GetPixel(Math.Max(x, 0), Math.Min(y + 1, Height - 1));
        var p4 = GetPixel(Math.Min(x + 1, Width - 1), Math.Min(y + 1, Height - 1));

        return new Pixel(
            (int)((p1.Red * uOpposite + p2.Red * uRatio) * vOpposite + (p3.Red * uOpposite + p4.Red * uRatio) * vRatio),
            (int)((p1.Green * uOpposite + p2.Green * uRatio) * vOpposite + (p3.Green * uOpposite + p4.Green * uRatio) * vRatio),
            (int)((p1.Blue * uOpposite + p2.Blue * uRatio) * vOpposite + (p3.Blue * uOpposite + p4.Blue * uRatio) * vRatio));
    }

    public Pixel SampleBl(Vector2d<float> position)
    {
        return SampleBl(position.X, position.Y);
    }

    public Pixel GetData()
    {
       return PixelData.First();
    }

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

    public Vector2d<int> Size()
    {
        return new Vector2d<int>(Width, Height);
    }

    public void SetSize(int w, int h)
    {
        Width = w;
        Height = h;
    }

    

}
