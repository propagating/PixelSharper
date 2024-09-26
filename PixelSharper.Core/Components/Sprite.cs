using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;
using PixelSharper.Core.Types;
namespace PixelSharper.Core.Components;

public class Sprite
{

    public int Height { get; set; }
    public int Width { get; set; }
    public List<Pixel> Pixels { get; set; } = new();
    public static ImageLoader ImageLoader;
    
    public SpriteDisplayMode spriteDisplayMode = SpriteDisplayMode.Normal;
    public SpriteMirrorMode spriteMirrorMode = SpriteMirrorMode.None;
    
    public Sprite()
    {
        
    }
    public Sprite(string imageFilePath, ResourcePack resourcePack)
    {

    }

    public Sprite(int width, int height)
    {
        Width = width;
        Height = height;
    }
    
    private Sprite(Sprite other)
    {
        throw new InvalidOperationException("Copy constructor is not allowed.");
    }

    ~Sprite()
    {
        
    }

    public FileReadCode LoadFromFile(string imageFilePath, ResourcePack resourcePack)
    {
        
    }

    public void SetSampleMode(SpriteDisplayMode spriteDisplayMode = SpriteDisplayMode.Normal)
    {
        this.spriteDisplayMode = spriteDisplayMode;
    }

    public Pixel GetPixel(int x, int y)
    {
        return new Pixel();
    }

    public Pixel GetPixel(Vector2d<double> position)
    {
        
    }

    public bool SetPixel(int x, int y, Pixel pixel)
    {
        
    }

    public bool SetPixel(Vector2d<double> position, Pixel pixel)
    {
        
    }

    public Pixel SamplePixel(float x, float y)
    {
        
    }

    public Pixel SamplePixel(Vector2d<float> position)
    {
        
    }

    public Pixel SampleBL(float u, float v)
    {
        
    }

    public Pixel SampleBl(Vector2d<float> position)
    {
        
    }

    public Pixel GetData()
    {
        
    }

    public Sprite Duplicate()
    {
        return new Sprite();
    }

    public Sprite Duplicate(Vector2d<double> position, Vector2d<double> size)
    {
        return new Sprite();
    }

    public Vector2d<double> Size()
    {
        return new Vector2d<double>();
    }

    public void SetSize(int w, int h)
    {
        this.Height = h;
        this.Width = w;
    }

    

}
