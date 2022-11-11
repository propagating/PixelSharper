namespace PixelSharper.Core;

public abstract class CoreEngine
{
    public string ApplicationName { get; set; } = null!;
    public abstract bool OnCreate();
    public abstract bool OnUpdate();
    public static PixelConfiguration Configuration { get; set; }
}
