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

/// <summary>
/// OpenTK (GLFW) implementation of the Platform abstraction. Collapses olc's split OS/GL
/// threads into one: the engine loop pumps events via HandleSystemEvent each frame, so
/// StartSystemEventLoop is a no-op.
/// </summary>
/// <remarks>
/// <para>
/// olc splits the OS event loop (main thread) from the GL context (engine thread). GLFW is
/// single-threaded-friendly, so this port runs everything on one thread: the engine loop calls
/// <see cref="HandleSystemEvent"/> each frame and <see cref="StartSystemEventLoop"/> does nothing.
/// </para>
/// <para>
/// Input follows a pull model: the engine polls the state members each frame after
/// <see cref="HandleSystemEvent"/>, then resolves pressed/released/held transitions itself.
/// </para>
/// </remarks>
public class PlatformOpenTK : Platform
{
    /// <summary>Backing GLFW window; null until CreateWindowPane runs.</summary>
    private NativeWindow? _window;

    /// <summary>The underlying GLFW window, exposed for the engine loop to query size/close state.</summary>
    /// <value>The GLFW <see cref="NativeWindow"/>, or null before <see cref="CreateWindowPane"/> runs.</value>
    public NativeWindow? Window => _window;

    /// <summary>True once the user has requested close (or no window exists); polled by the engine loop to stop.</summary>
    /// <value><c>true</c> when there is no window or it is exiting; otherwise <c>false</c>.</value>
    public bool ShouldClose => _window == null || _window.IsExiting;

    /// <summary>Current client (drawable) size in pixels; polled each frame to detect user resizes.</summary>
    /// <value>The window client size, or <c>default</c> (zero) when no window exists.</value>
    public Vector2d<int> ClientSize =>
        _window == null ? default : new Vector2d<int>(_window.ClientSize.X, _window.ClientSize.Y);

    /// <summary>Per-application start-up hook; nothing to do for GLFW.</summary>
    /// <returns>Always <see cref="FileReadCode.OK"/>.</returns>
    public override FileReadCode ApplicationStartUp() => FileReadCode.OK;

    /// <summary>Per-application clean-up; disposes the window.</summary>
    /// <returns>Always <see cref="FileReadCode.OK"/>.</returns>
    public override FileReadCode ApplicationCleanUp()
    {
        _window?.Dispose();
        _window = null;
        return FileReadCode.OK;
    }

    /// <summary>Per-thread start-up hook; no-op in the single-threaded model.</summary>
    /// <returns>Always <see cref="FileReadCode.OK"/>.</returns>
    public override FileReadCode ThreadStartUp() => FileReadCode.OK;

    /// <summary>Per-thread clean-up hook; no-op in the single-threaded model.</summary>
    /// <returns>Always <see cref="FileReadCode.OK"/>.</returns>
    public override FileReadCode ThreadCleanUp() => FileReadCode.OK;

    /// <summary>Creates the GLFW window with a legacy/compatibility GL 2.1 context for OGL10 immediate mode.</summary>
    /// <param name="windowPos">Top-left screen position of the window, in pixels.</param>
    /// <param name="windowSize">Client area size of the window, in pixels.</param>
    /// <param name="fullScreen">When true, opens the window full-screen; otherwise normal/windowed.</param>
    /// <returns>Always <see cref="FileReadCode.OK"/> once the window is created.</returns>
    /// <remarks>
    /// <para>
    /// OGL10 immediate mode (<c>glBegin</c>/<c>glEnd</c>) requires a legacy/compatibility context. GLFW only
    /// accepts a profile hint for GL 3.2 or later, so this requests GL 2.1 with <see cref="ContextProfile.Any"/>
    /// and clears the forward-compatible flag (valid only for GL 3.0 or later) to obtain a fixed-function context.
    /// </para>
    /// </remarks>
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

    /// <summary>Files dropped since the last consume; null when none pending.</summary>
    private List<string>? _pendingDropFiles;
    /// <summary>Cursor location at the time of the pending drop.</summary>
    private Vector2d<int> _pendingDropPoint;

    /// <summary>GLFW file-drop callback; records the dropped file names and the cursor position.</summary>
    /// <param name="e">The GLFW file-drop event carrying the dropped file names.</param>
    /// <remarks>
    /// <para>GLFW's drop callback carries no position, so the cursor location at drop time is captured instead.</para>
    /// </remarks>
    private void OnFileDrop(FileDropEventArgs e)
    {
        _pendingDropFiles = new List<string>(e.FileNames);
        // GLFW's drop callback has no position; use the cursor location at drop time.
        _pendingDropPoint = _window == null
            ? default
            : new Vector2d<int>((int)_window.MouseState.X, (int)_window.MouseState.Y);
    }

    /// <summary>Hands the engine any files dropped since the last call (window-space drop point), once.</summary>
    /// <param name="files">Receives the dropped file names when a drop is pending; otherwise null.</param>
    /// <param name="point">Receives the window-space cursor position at drop time; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if a pending drop was consumed; <c>false</c> if none was pending.</returns>
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

    /// <summary>Makes the window's GL context current and hands it to the active renderer's CreateDevice.</summary>
    /// <param name="fullScreen">Whether the device is being created for a full-screen window.</param>
    /// <param name="enableVsync">When true, requests vertical-sync presentation.</param>
    /// <param name="viewPos">Top-left of the initial viewport rectangle, in pixels.</param>
    /// <param name="viewSize">Size of the initial viewport rectangle, in pixels.</param>
    /// <returns><see cref="FileReadCode.OK"/> on success; <see cref="FileReadCode.FAIL"/> if there is no window/renderer or device creation fails.</returns>
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

    /// <summary>Sets the window title bar text.</summary>
    /// <param name="title">The text to display in the title bar.</param>
    /// <returns><see cref="FileReadCode.OK"/> on success; <see cref="FileReadCode.FAIL"/> if there is no window.</returns>
    public override FileReadCode SetWindowTitle(string title)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.Title = title;
        return FileReadCode.OK;
    }

    /// <summary>Toggles the window border between a resizable frame and borderless.</summary>
    /// <param name="showFrame">When true, shows a resizable frame; when false, makes the window borderless.</param>
    /// <returns><see cref="FileReadCode.OK"/> on success; <see cref="FileReadCode.FAIL"/> if there is no window.</returns>
    public override FileReadCode ShowWindowFrame(bool showFrame = true)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.WindowBorder = showFrame ? WindowBorder.Resizable : WindowBorder.Hidden;
        return FileReadCode.OK;
    }

    /// <summary>Repositions and resizes the window's client area.</summary>
    /// <param name="windowPos">New top-left screen position of the window, in pixels.</param>
    /// <param name="windowSize">New client area size, in pixels.</param>
    /// <returns><see cref="FileReadCode.OK"/> on success; <see cref="FileReadCode.FAIL"/> if there is no window.</returns>
    public override FileReadCode SetWindowSize(Vector2d<int> windowPos, Vector2d<int> windowSize)
    {
        if (_window == null) return FileReadCode.FAIL;
        _window.Location = new Vector2i(windowPos.X, windowPos.Y);
        _window.ClientSize = new Vector2i(windowSize.X, windowSize.Y);
        return FileReadCode.OK;
    }

    /// <summary>No-op: the engine loop owns the cadence and pumps events via HandleSystemEvent.</summary>
    /// <returns>Always <see cref="FileReadCode.OK"/>.</returns>
    /// <seealso cref="HandleSystemEvent"/>
    public override FileReadCode StartSystemEventLoop()
    {
        // Single-threaded model: the engine loop owns the cadence and pumps via HandleSystemEvent.
        return FileReadCode.OK;
    }

    /// <summary>Snapshots input for the new frame and dispatches pending GLFW events into it.</summary>
    /// <returns><see cref="FileReadCode.OK"/> on success; <see cref="FileReadCode.FAIL"/> if there is no window.</returns>
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

    /// <summary>KeyPress-to-GLFW key lookup table built once at type init.</summary>
    private static readonly Dictionary<KeyPress, Keys> KeyMap = BuildKeyMap();

    /// <summary>True if the given key is currently held; SHIFT/CTRL match either side.</summary>
    /// <param name="key">The logical <see cref="KeyPress"/> to test.</param>
    /// <returns><c>true</c> if the key is down this frame; <c>false</c> if up, unmapped, or there is no window.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="KeyPress.SHIFT"/> and <see cref="KeyPress.CTRL"/> report down if either the left or right
    /// physical key is held, mirroring olc's single-modifier model; other keys resolve through <c>KeyMap</c>.
    /// </para>
    /// </remarks>
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

    /// <summary>True if the given mouse button (0/1/2 = left/right/middle) is currently held.</summary>
    /// <param name="button">Button index: 0 left, 1 right, 2 middle (matching olc's ordering).</param>
    /// <returns><c>true</c> if the button is down this frame; <c>false</c> if up or there is no window.</returns>
    public bool IsMouseDown(int button)
    {
        if (_window == null) return false;
        // GLFW MouseButton: Button1/2/3 == Left/Right/Middle, matching olc's 0/1/2 ordering.
        return _window.MouseState.IsButtonDown((MouseButton)button);
    }

    /// <summary>Current cursor position in window-client pixels.</summary>
    /// <value>The (X, Y) cursor position in client pixels, or (0, 0) when no window exists.</value>
    public (int X, int Y) MousePosition =>
        _window == null ? (0, 0) : ((int)_window.MouseState.X, (int)_window.MouseState.Y);

    /// <summary>Vertical scroll delta this frame, in GLFW notches (~1.0 per detent).</summary>
    /// <value>The vertical scroll delta since the last frame, or 0 when no window exists.</value>
    public float MouseScroll => _window?.MouseState.ScrollDelta.Y ?? 0.0f;

    /// <summary>True while the window has input focus.</summary>
    /// <value><c>true</c> when the window holds input focus; otherwise <c>false</c>.</value>
    public bool IsFocused => _window?.IsFocused ?? false;

    /// <summary>Builds the KeyPress-to-GLFW key map (letters/digits/function/keypad keys plus named keys).</summary>
    /// <returns>A dictionary mapping each supported <see cref="KeyPress"/> to its GLFW key.</returns>
    /// <remarks>
    /// <para>
    /// Letters, digits, function keys, and keypad digits are filled by contiguous-range arithmetic; the
    /// remaining named/OEM keys are mapped explicitly. <see cref="KeyPress.OEM_8"/> has no portable GLFW
    /// equivalent and is left unmapped (so it always reads as not-down).
    /// </para>
    /// </remarks>
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
