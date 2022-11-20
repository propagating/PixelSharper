namespace PixelSharper.Core;

public abstract class CoreEngine
{
    public string ApplicationName { get; set; }
    public abstract bool OnCreate();
    public abstract bool OnUpdate(float elapsedTime);
    public static PixelConfiguration Configuration { get; set; }

    public bool Construct(byte height, byte width, byte pixelWidth, byte pixelHeight)
    {
        return true;
    }

    public void Start()
    {
        
    }
}
