using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

// Port of olc::Platform — the OS windowing / event-loop abstraction, sibling to Renderer.
// Concrete implementations (e.g. PlatformOpenTK) own the window and feed the GL context to
// the active Renderer via CreateGraphics. olc returns olc::rcode; we use FileReadCode.
public abstract class Platform
{
    // Mirrors olc::Platform::ptrPGE — the engine this platform serves. Set during engine construction.
    public static PixelGameEngine PtrPGE;

    public abstract FileReadCode ApplicationStartUp();
    public abstract FileReadCode ApplicationCleanUp();
    public abstract FileReadCode ThreadStartUp();
    public abstract FileReadCode ThreadCleanUp();
    public abstract FileReadCode CreateGraphics(bool fullScreen, bool enableVsync, Vector2d<int> viewPos, Vector2d<int> viewSize);
    public abstract FileReadCode CreateWindowPane(Vector2d<int> windowPos, Vector2d<int> windowSize, bool fullScreen);
    public abstract FileReadCode SetWindowTitle(string title);
    public abstract FileReadCode ShowWindowFrame(bool showFrame = true);
    public abstract FileReadCode SetWindowSize(Vector2d<int> windowPos, Vector2d<int> windowSize);
    public abstract FileReadCode StartSystemEventLoop();
    public abstract FileReadCode HandleSystemEvent();
}
