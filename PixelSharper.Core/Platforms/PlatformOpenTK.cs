using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;
// Import only the GLFW input types we need — the full namespace also defines a `Platform`
// type that collides with PixelSharper.Core.Components.Platform (our base class).
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using MouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace PixelSharper.Core.Platforms;

// OpenTK (GLFW) implementation of the Platform abstraction.
//
// olc runs the OS event loop on the main thread and the GL context on a separate engine
// thread. GLFW requires window/event calls on the main thread but allows the GL context to
// be current on another; rather than reproduce that split, we collapse to a single thread:
// the engine loop pumps events via HandleSystemEvent each frame, so StartSystemEventLoop
// (used by olc's GLUT/Emscripten platforms that own the loop) is a no-op here.
public class PlatformOpenTK : Platform
{
    private NativeWindow? _window;

    // Exposed so the engine loop can pump events, query the real client size, and detect
    // a close request (_window.IsExiting) — the abstract Platform contract intentionally
    // stays olc-shaped and doesn't surface these.
    public NativeWindow? Window => _window;

    // True once the user has requested the window close (set during HandleSystemEvent), or if
    // no window exists. The engine loop polls this to know when to stop.
    public bool ShouldClose => _window == null || _window.IsExiting;

    // Current client (drawable) size in pixels. The engine polls this each frame to detect
    // user resizes (our pull-model equivalent of olc's OS resize callback).
    public Vector2d<int> ClientSize =>
        _window == null ? default : new Vector2d<int>(_window.ClientSize.X, _window.ClientSize.Y);

    public override FileReadCode ApplicationStartUp() => FileReadCode.OK;

    public override FileReadCode ApplicationCleanUp()
    {
        _window?.Dispose();
        _window = null;
        return FileReadCode.OK;
    }

    public override FileReadCode ThreadStartUp() => FileReadCode.OK;

    public override FileReadCode ThreadCleanUp() => FileReadCode.OK;

    public override FileReadCode CreateWindowPane(Vector2d<int> windowPos, Vector2d<int> windowSize, bool fullScreen)
    {
        var settings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(windowSize.X, windowSize.Y),
            Location = new Vector2i(windowPos.X, windowPos.Y),
            Title = PtrPGE?.ApplicationName ?? "PixelSharper",
            // OGL10 immediate mode (glBegin/glEnd) needs a legacy/compatibility context. GLFW only
            // accepts a profile hint for GL >= 3.2, so request 2.1 with ContextProfile.Any — that
            // yields a compatibility-capable context that supports the fixed-function pipeline.
            API = ContextAPI.OpenGL,
            APIVersion = new Version(2, 1),
            Profile = ContextProfile.Any,
            // OpenTK defaults Flags to ForwardCompatible (for macOS core contexts); that flag is
            // only valid for GL >= 3.0, so clear it for our legacy 2.1 context.
            Flags = ContextFlags.Default,
            WindowState = fullScreen ? WindowState.Fullscreen : WindowState.Normal,
            StartVisible = true
        };

        _window = new NativeWindow(settings);
        _window.FileDrop += OnFileDrop;
        return FileReadCode.OK;
    }

    private List<string>? _pendingDropFiles;
    private Vector2d<int> _pendingDropPoint;

    private void OnFileDrop(FileDropEventArgs e)
    {
        _pendingDropFiles = new List<string>(e.FileNames);
        // GLFW's drop callback has no position; use the cursor location at drop time.
        _pendingDropPoint = _window == null
            ? default
            : new Vector2d<int>((int)_window.MouseState.X, (int)_window.MouseState.Y);
    }

    // Hands the engine any files dropped since the last call (window-space drop point), once.
    public bool TryConsumeDroppedFiles(out List<string> files, out Vector2d<int> point)
    {
        if (_pendingDropFiles == null)
        {
            files = null!;
            point = default;
            return false;
        }
        files = _pendingDropFiles;
        point = _pendingDropPoint;
        _pendingDropFiles = null;
        return true;
    }

    public override FileReadCode CreateGraphics(bool fullScreen, bool enableVsync, Vector2d<int> viewPos, Vector2d<int> viewSize)
    {
        if (_window == null || Renderer.Active == null)
            return FileReadCode.FAIL;

        // The NativeWindow created the GL context; make it current on this (engine) thread,
        // then hand it to the active renderer exactly as olc's CreateGraphics hands native
        // handles to renderer->CreateDevice.
        _window.Context.MakeCurrent();
        if (Renderer.Active.CreateDevice(new List<object> { _window.Context }, fullScreen, enableVsync) != FileReadCode.OK)
            return FileReadCode.FAIL;

        Renderer.Active.UpdateViewport(viewPos, viewSize);
        return FileReadCode.OK;
    }

    public override FileReadCode SetWindowTitle(string title)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.Title = title;
        return FileReadCode.OK;
    }

    public override FileReadCode ShowWindowFrame(bool showFrame = true)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.WindowBorder = showFrame ? WindowBorder.Resizable : WindowBorder.Hidden;
        return FileReadCode.OK;
    }

    public override FileReadCode SetWindowSize(Vector2d<int> windowPos, Vector2d<int> windowSize)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.Location = new Vector2i(windowPos.X, windowPos.Y);
        _window.ClientSize = new Vector2i(windowSize.X, windowSize.Y);
        return FileReadCode.OK;
    }

    public override FileReadCode StartSystemEventLoop()
    {
        // Single-threaded model: the engine loop owns the cadence and pumps via HandleSystemEvent.
        return FileReadCode.OK;
    }

    public override FileReadCode HandleSystemEvent()
    {
        if (_window == null) return FileReadCode.FAIL;
        // Snapshot input for the new frame, then dispatch pending GLFW events into it.
        _window.NewInputFrame();
        NativeWindow.ProcessWindowEvents(false);
        return FileReadCode.OK;
    }

    // --- Input (pull model: the engine polls these each frame after HandleSystemEvent, then
    //     resolves pressed/released/held transitions itself, à la olc's ScanHardware). ---

    private static readonly Dictionary<KeyPress, Keys> KeyMap = BuildKeyMap();

    public bool IsKeyDown(KeyPress key)
    {
        if (_window == null) return false;
        var ks = _window.KeyboardState;
        return key switch
        {
            KeyPress.NONE => false,
            // olc's single SHIFT/CTRL keys cover either side.
            KeyPress.SHIFT => ks.IsKeyDown(Keys.LeftShift) || ks.IsKeyDown(Keys.RightShift),
            KeyPress.CTRL => ks.IsKeyDown(Keys.LeftControl) || ks.IsKeyDown(Keys.RightControl),
            _ => KeyMap.TryGetValue(key, out var glfw) && ks.IsKeyDown(glfw)
        };
    }

    public bool IsMouseDown(int button)
    {
        if (_window == null) return false;
        // GLFW MouseButton: Button1/2/3 == Left/Right/Middle, matching olc's 0/1/2 ordering.
        return _window.MouseState.IsButtonDown((MouseButton)button);
    }

    public (int X, int Y) MousePosition =>
        _window == null ? (0, 0) : ((int)_window.MouseState.X, (int)_window.MouseState.Y);

    // GLFW reports scroll in notches (~1.0 per detent); olc uses Windows' WHEEL_DELTA (120).
    public float MouseScroll => _window?.MouseState.ScrollDelta.Y ?? 0.0f;

    public bool IsFocused => _window?.IsFocused ?? false;

    private static Dictionary<KeyPress, Keys> BuildKeyMap()
    {
        var map = new Dictionary<KeyPress, Keys>();
        for (var i = 0; i < 26; i++) map[KeyPress.A + i] = Keys.A + i;
        for (var i = 0; i < 10; i++) map[KeyPress.K0 + i] = Keys.D0 + i;
        for (var i = 0; i < 12; i++) map[KeyPress.F1 + i] = Keys.F1 + i;
        for (var i = 0; i < 10; i++) map[KeyPress.NP0 + i] = Keys.KeyPad0 + i;

        map[KeyPress.UP] = Keys.Up;
        map[KeyPress.DOWN] = Keys.Down;
        map[KeyPress.LEFT] = Keys.Left;
        map[KeyPress.RIGHT] = Keys.Right;
        map[KeyPress.SPACE] = Keys.Space;
        map[KeyPress.TAB] = Keys.Tab;
        map[KeyPress.INS] = Keys.Insert;
        map[KeyPress.DEL] = Keys.Delete;
        map[KeyPress.HOME] = Keys.Home;
        map[KeyPress.END] = Keys.End;
        map[KeyPress.PGUP] = Keys.PageUp;
        map[KeyPress.PGDN] = Keys.PageDown;
        map[KeyPress.BACK] = Keys.Backspace;
        map[KeyPress.ESCAPE] = Keys.Escape;
        map[KeyPress.RETURN] = Keys.Enter;
        map[KeyPress.ENTER] = Keys.KeyPadEnter;
        map[KeyPress.PAUSE] = Keys.Pause;
        map[KeyPress.SCROLL] = Keys.ScrollLock;
        map[KeyPress.NP_MUL] = Keys.KeyPadMultiply;
        map[KeyPress.NP_DIV] = Keys.KeyPadDivide;
        map[KeyPress.NP_ADD] = Keys.KeyPadAdd;
        map[KeyPress.NP_SUB] = Keys.KeyPadSubtract;
        map[KeyPress.NP_DECIMAL] = Keys.KeyPadDecimal;
        map[KeyPress.PERIOD] = Keys.Period;
        map[KeyPress.EQUALS] = Keys.Equal;
        map[KeyPress.COMMA] = Keys.Comma;
        map[KeyPress.MINUS] = Keys.Minus;
        map[KeyPress.OEM_1] = Keys.Semicolon;
        map[KeyPress.OEM_2] = Keys.Slash;
        map[KeyPress.OEM_3] = Keys.GraveAccent;
        map[KeyPress.OEM_4] = Keys.LeftBracket;
        map[KeyPress.OEM_5] = Keys.Backslash;
        map[KeyPress.OEM_6] = Keys.RightBracket;
        map[KeyPress.OEM_7] = Keys.Apostrophe;
        // OEM_8 has no portable GLFW equivalent; left unmapped (reads as not-down).
        map[KeyPress.CAPS_LOCK] = Keys.CapsLock;
        return map;
    }
}
