using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Platforms;
using PixelSharper.Core.Renderers;
using PixelSharper.Core.Types;

namespace PixelSharper.Core;

/// <summary>Port of olc::PixelGameEngine — the abstract base the user subclasses; owns the window, renderer, layers, input and the single-threaded core frame loop.</summary>
// Port of olc::PixelGameEngine. olc splits work across a main thread (window + OS event loop)
// and an engine thread (GL context + frame loop). Our OpenTK platform is single-threaded, so
// Start() does everything on the calling thread: create window, create graphics, then run the
// core loop until the window is closed.
public abstract class PixelGameEngine
{
    /// <summary>The application name shown in the window title bar.</summary>
    /// <value>The text composed into the window caption alongside the FPS readout each second.</value>
    public string ApplicationName { get; set; } = null!;
    /// <summary>Override: called once after construction; return <c>false</c> to abort startup.</summary>
    /// <returns><c>true</c> to continue into the core loop; <c>false</c> to abort and tear down immediately.</returns>
    /// <remarks>Invoked by <see cref="Start"/> after the window, GL context, font sheet and layer 0 are ready, so GL-dependent setup is safe here.</remarks>
    /// <seealso cref="OnUpdate(float)"/>
    /// <seealso cref="OnDestroy"/>
    public abstract bool OnCreate();
    /// <summary>Override: called every frame to draw/update; return <c>false</c> to request shutdown.</summary>
    /// <param name="elapsedTime">Wall-clock seconds elapsed since the previous frame, used to make motion frame-rate independent.</param>
    /// <returns><c>true</c> to keep running; <c>false</c> to request the core loop to exit.</returns>
    /// <seealso cref="OnCreate"/>
    public abstract bool OnUpdate(float elapsedTime);
    /// <summary>Override: called once on shutdown (olc's OnUserDestroy); return <c>false</c> to veto the close and keep running.</summary>
    /// <returns><c>true</c> to allow the engine to shut down; <c>false</c> to veto the close so the core loop resumes.</returns>
    /// <remarks>The default returns <c>true</c>. Consulted by <see cref="Start"/> when a close is requested.</remarks>
    public virtual bool OnDestroy() => true;
    /// <summary>Engine-wide configuration constants (mirrors <see cref="PixelConfiguration"/>).</summary>
    /// <value>The shared <see cref="PixelConfiguration"/> record exposing engine-wide constants.</value>
    public static PixelConfiguration Configuration { get; set; }

    /// <summary>NDC size of a single screen pixel (olc's vPixel) — 2/screenW by 2/screenH.</summary>
    /// <value>The width and height of one engine pixel in normalised device coordinates (range -1..1).</value>
    public Vector2d<float> VectorPixel { get; set; }
    /// <summary>Whether vertical sync is requested.</summary>
    /// <value><c>true</c> if buffer swaps are synchronised to the display refresh.</value>
    public bool EnableVSYNC { get; set; }
    /// <summary>Whether the window is created full-screen.</summary>
    /// <value><c>true</c> if the window covers the whole display; <c>false</c> for a normal framed window.</value>
    public bool FullScreen { get; set; }
    /// <summary>The window size in OS pixels (screen size times pixel size).</summary>
    /// <value>The client area dimensions in OS pixels, i.e. <see cref="ScreenSize"/> scaled by <see cref="PixelSize"/>.</value>
    public Vector2d<int> WindowSize { get; set; }
    /// <summary>The window position in OS screen coordinates.</summary>
    /// <value>The top-left corner of the window in desktop pixel coordinates.</value>
    public Vector2d<int> WindowPos { get; set; }
    /// <summary>The size in OS pixels of one engine pixel.</summary>
    /// <value>The integer scale factor (width, height) mapping one engine pixel to OS pixels.</value>
    public Vector2d<int> PixelSize { get; set; }
    /// <summary>Reciprocal of the screen size (1/screenW, 1/screenH), cached for NDC transforms.</summary>
    /// <value>The componentwise reciprocal of <see cref="ScreenSize"/>, precomputed to avoid per-pixel division.</value>
    public Vector2d<float> InvScreenSize { get; set; }
    /// <summary>The drawable screen size in engine pixels.</summary>
    /// <value>The logical screen dimensions (width, height) the user draws into.</value>
    public Vector2d<int> ScreenSize { get; set; }
    /// <summary>Whether the engine renders 1:1 into a freely-resizable OS window (no pixel scaling/letterbox).</summary>
    /// <value><c>true</c> to draw directly at the window resolution with no letterbox; <c>false</c> for the scaled/letterboxed pixel-art mode.</value>
    public bool RealWindowMode { get; set; }
    /// <summary>Whether to lock the pixel scale to whole-integer multiples (avoids shimmer).</summary>
    /// <value><c>true</c> to constrain the on-screen pixel size to integer multiples for crisp pixel-art scaling.</value>
    public bool PixelCohesion { get; set; }

    /// <summary>Top-left of the letterboxed region of the window the screen is drawn into.</summary>
    /// <value>The viewport origin in OS pixels; the offset that centres the screen inside the window.</value>
    /// <seealso cref="ViewSize"/>
    public Vector2d<int> ViewPos { get; set; }
    /// <summary>Size of the letterboxed region of the window the screen is drawn into.</summary>
    /// <value>The viewport dimensions in OS pixels, preserving the screen aspect ratio inside the window.</value>
    /// <seealso cref="ViewPos"/>
    public Vector2d<int> ViewSize { get; set; }
    /// <summary>The on-screen pixel size derived under pixel cohesion.</summary>
    /// <value>The actual per-pixel size used for rendering once <see cref="PixelCohesion"/> integer-snapping is applied.</value>
    public Vector2d<int> ScreenPixelSize { get; set; }

    /// <summary>Frames rendered in the last second.</summary>
    /// <value>The most recent frames-per-second count, refreshed once per one-second window.</value>
    public uint LastFps { get; private set; }
    /// <summary>Seconds elapsed during the last frame.</summary>
    /// <value>The wall-clock duration in seconds of the previous frame, mirroring the value passed to <see cref="OnUpdate(float)"/>.</value>
    public float LastElapsed { get; private set; }

    /// <summary>The OS window / event-loop backend.</summary>
    /// <remarks>Created in <see cref="Start"/>; the OpenTK implementation of olc's <c>Platform</c>.</remarks>
    private PlatformOpenTK _platform = null!;
    /// <summary>The active GL rendering device.</summary>
    /// <remarks>Created in <see cref="Start"/> via <see cref="CreateRenderer"/> and also published via <c>Renderer.Active</c>; defaults to the shader-based <see cref="RendererOgl33"/> backend (override <see cref="CreateRenderer"/> for the fixed-function <see cref="RendererOgl10"/> fallback).</remarks>
    private Renderer _renderer = null!;

    /// <summary>
    /// Creates the GL rendering backend used by <see cref="Start"/>. Override to select a different
    /// device — e.g. <c>return new RendererOgl10();</c> for the fixed-function fallback (pre-3.3
    /// hardware) instead of the default shader-based <see cref="RendererOgl33"/>.
    /// </summary>
    /// <returns>The renderer instance to drive this engine; defaults to a new <see cref="RendererOgl33"/> (matching olc's own default).</returns>
    protected virtual Renderer CreateRenderer() => new RendererOgl33();

    /// <summary>The layer stack (layer 0 always exists); rendered back-to-front.</summary>
    /// <remarks>A <see cref="List{T}"/> of <see cref="LayerDesc"/>; <see cref="LayerDesc"/> is a class so layers can be mutated in place.</remarks>
    private readonly List<LayerDesc> _layers = new();
    /// <summary>The sprite that software draw primitives currently write into.</summary>
    private Sprite _drawTarget = null!;
    /// <summary>Index of the layer whose draw target is currently active (for decal/GPU-task pooling).</summary>
    private byte _targetLayer;

    /// <summary>Current pixel blend mode used by Draw (Normal/Mask/Alpha/Custom).</summary>
    /// <remarks>Selects how source pixels combine with the draw target; <c>Custom</c> defers to <see cref="_funcPixelMode"/>.</remarks>
    private PixelDisplayMode _pixelMode = PixelDisplayMode.Normal;
    /// <summary>Global alpha multiplier applied in Alpha pixel mode.</summary>
    private float _blendFactor = 1.0f;
    /// <summary>Current decal blend mode applied to submitted decal instances.</summary>
    private DecalMode _decalMode = DecalMode.Normal;
    /// <summary>Current decal vertex topology (List/Strip/Fan/Line).</summary>
    private DecalStructure _decalStructure = DecalStructure.Fan;
    /// <summary>Whether HW3D draws are depth-tested.</summary>
    private bool _hw3dDepthTest = true;
    /// <summary>Current HW3D face-culling mode.</summary>
    private CullMode _hw3dCullMode = CullMode.None;
    /// <summary>Reusable 4-point scratch for the colour-quad/line decal helpers (filled and consumed synchronously, so sharing is safe).</summary>
    private readonly Vector2d<float>[] _scratchPts4 = new Vector2d<float>[4];
    /// <summary>Reusable 4-colour scratch for the colour-quad decal helpers.</summary>
    private readonly Pixel[] _scratchCol4 = new Pixel[4];
    /// <summary>Reusable 2-point scratch for the line decal helper.</summary>
    private readonly Vector2d<float>[] _scratchPts2 = new Vector2d<float>[2];
    /// <summary>Reusable 3-point position scratch for the textured-triangle path.</summary>
    private readonly Vector2d<float>[] _texTri3 = new Vector2d<float>[3];
    /// <summary>Reusable 3-colour scratch for the textured-triangle path.</summary>
    private readonly Pixel[] _colTri3 = new Pixel[3];
    /// <summary>Reusable 3-position scratch for gathering a triangle from a textured polygon.</summary>
    private readonly Vector2d<float>[] _polyPos3 = new Vector2d<float>[3];
    /// <summary>Reusable 3-UV scratch for gathering a triangle from a textured polygon.</summary>
    private readonly Vector2d<float>[] _polyTex3 = new Vector2d<float>[3];
    /// <summary>Reusable 3-colour scratch for gathering a triangle from a textured polygon.</summary>
    private readonly Pixel[] _polyCol3 = new Pixel[3];
    /// <summary>Four opaque-white colours, the default tint for sprite/decal patch quads.</summary>
    private static readonly Pixel[] WhiteQuad4 = { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE, Pixel.WHITE };
    /// <summary>User-supplied blend function used when the pixel mode is Custom.</summary>
    /// <remarks>Receives (x, y, source, destination) and returns the blended <see cref="Pixel"/>; invoked only while <see cref="_pixelMode"/> is <c>Custom</c>.</remarks>
    private Func<int, int, Pixel, Pixel, Pixel> _funcPixelMode = null!;
    /// <summary>When true, layer draw-target sprites are not re-uploaded to their textures each frame.</summary>
    private bool _suspendTextureTransfer;

    /// <summary>The embedded 8x8 font as a 128x48 renderable sprite/decal.</summary>
    private Renderable _fontRenderable = null!;
    /// <summary>Per-glyph proportional spacing (x offset, width) indexed by char minus 32.</summary>
    /// <remarks>Index 0 corresponds to the space character (ASCII 32); each entry stores the glyph's left offset and advance width in pixels.</remarks>
    private readonly List<Vector2d<int>> _fontSpacing = new();
    /// <summary>Number of spaces a tab character advances when drawing text.</summary>
    private int _tabSizeInSpaces = 4;

    /// <summary>High-resolution clock driving per-frame timing.</summary>
    private readonly Stopwatch _clock = new();
    /// <summary>The clock time (seconds) at the previous frame.</summary>
    private double _lastTime;
    /// <summary>Accumulated time toward the next one-second FPS update.</summary>
    private float _frameTimer;
    /// <summary>Frames counted in the current one-second FPS window.</summary>
    private int _frameCount;
    /// <summary>Whether the core loop is running.</summary>
    private bool _active;
    /// <summary>Whether a screen-buffer resize is pending (real-window mode).</summary>
    private bool _resizeRequested;
    /// <summary>Registered PGEX extensions, called around OnCreate/OnUpdate.</summary>
    /// <remarks>Populated via <see cref="RegisterExtension(PGEX)"/>; their lifecycle hooks bracket <see cref="OnCreate"/> and <see cref="OnUpdate(float)"/>.</remarks>
    private readonly List<PGEX> _extensions = new();

    /// <summary>Sets the static PGEX.Pge back-reference so extensions can find and auto-register with the engine.</summary>
    /// <remarks>Mirrors olc setting <c>PGEX::pge</c> in its constructor, so extensions created in a subclass constructor or <see cref="OnCreate"/> can locate this engine and self-register via <see cref="RegisterExtension(PGEX)"/>.</remarks>
    protected PixelGameEngine()
    {
        // olc sets PGEX::pge in the engine constructor, so extensions constructed by the user
        // (in their subclass ctor / OnCreate) can find the engine and auto-register.
        PGEX.Pge = this;
    }

    /// <summary>Adds an extension to the list the engine drives, if not already present.</summary>
    /// <param name="extension">The PGEX extension to register; ignored if it is already in the list.</param>
    internal void RegisterExtension(PGEX extension)
    {
        if (!_extensions.Contains(extension))
            _extensions.Add(extension);
    }

    /// <summary>Number of tracked mouse buttons (olc's nMouseButtons).</summary>
    /// <remarks>Sizes the mouse-button state arrays; matches olc's <c>nMouseButtons</c> of 5.</remarks>
    private const int MouseButtonCount = 5; // olc's nMouseButtons
    /// <summary>Resolved pressed/released/held state per keyboard key.</summary>
    /// <remarks>Indexed by <c>(int)</c><see cref="KeyPress"/>; the edge state derived each frame from the raw old/new arrays.</remarks>
    private readonly HardwareButton[] _keyboardState = new HardwareButton[(int)KeyPress.EnumEnd];
    /// <summary>Previous-frame raw down state per keyboard key.</summary>
    private readonly bool[] _keyOldState = new bool[(int)KeyPress.EnumEnd];
    /// <summary>Current-frame raw down state per keyboard key.</summary>
    private readonly bool[] _keyNewState = new bool[(int)KeyPress.EnumEnd];
    /// <summary>Resolved pressed/released/held state per mouse button.</summary>
    /// <remarks>Indexed 0..<see cref="MouseButtonCount"/>-1; the edge state derived each frame from the raw old/new arrays.</remarks>
    private readonly HardwareButton[] _mouseButtonState = new HardwareButton[MouseButtonCount];
    /// <summary>Previous-frame raw down state per mouse button.</summary>
    private readonly bool[] _mouseOldState = new bool[MouseButtonCount];
    /// <summary>Current-frame raw down state per mouse button.</summary>
    private readonly bool[] _mouseNewState = new bool[MouseButtonCount];
    /// <summary>Mouse position in screen (pixel) space for this frame.</summary>
    private Vector2d<int> _mousePos;
    /// <summary>Latched mouse position, applied to _mousePos at frame start for consistency.</summary>
    private Vector2d<int> _mousePosCache;
    /// <summary>Raw mouse position in window space.</summary>
    private Vector2d<int> _mouseWindowPos;
    /// <summary>Mouse wheel delta for this frame.</summary>
    private int _mouseWheelDelta;
    /// <summary>Latched mouse wheel delta, applied at frame start then reset.</summary>
    private int _mouseWheelDeltaCache;
    /// <summary>Whether the window currently has input focus.</summary>
    private bool _hasInputFocus;
    /// <summary>Shared empty list returned when no files were dropped.</summary>
    /// <remarks>A single immutable-by-convention empty list, returned to avoid allocating when no files were dropped.</remarks>
    private static readonly List<string> NoFiles = new();
    /// <summary>Files dropped onto the window this frame (or the shared empty list).</summary>
    private List<string> _droppedFiles = NoFiles;
    /// <summary>Pixel-space point at which files were dropped.</summary>
    private Vector2d<int> _droppedFilesPoint;

    /// <summary>Configures screen/pixel/window sizing and flags (olc's Construct); returns Fail on non-positive dimensions.</summary>
    /// <param name="screenW">Drawable screen width in engine pixels; must be positive.</param>
    /// <param name="screenH">Drawable screen height in engine pixels; must be positive.</param>
    /// <param name="pixelW">Width in OS pixels of one engine pixel; must be positive.</param>
    /// <param name="pixelH">Height in OS pixels of one engine pixel; must be positive.</param>
    /// <param name="fullScreen">When <c>true</c>, create the window full-screen.</param>
    /// <param name="vsync">When <c>true</c>, synchronise buffer swaps to the display refresh.</param>
    /// <param name="cohesion">When <c>true</c>, snap the on-screen pixel scale to whole-integer multiples (see <see cref="PixelCohesion"/>).</param>
    /// <param name="realWindow">When <c>true</c>, render 1:1 into a freely-resizable window with no letterbox (see <see cref="RealWindowMode"/>).</param>
    /// <returns><c>FileReadCode.Ok</c> if the configuration is valid; <c>FileReadCode.Fail</c> if any pixel or screen dimension is not positive.</returns>
    /// <remarks>Call before <see cref="Start"/>. Computes <see cref="WindowSize"/>, <see cref="InvScreenSize"/> and <see cref="VectorPixel"/> from the supplied dimensions.</remarks>
    /// <example>
    /// <code>
    /// class App : PixelGameEngine
    /// {
    ///     public override bool OnCreate() => true;
    ///     public override bool OnUpdate(float elapsedTime)
    ///     {
    ///         Clear(Pixel.BLACK);
    ///         return true;
    ///     }
    /// }
    ///
    /// var app = new App();
    /// if (app.Construct(256, 240, 4, 4) == FileReadCode.Ok)
    ///     app.Start();
    /// </code>
    /// </example>
    /// <seealso cref="Start"/>
    public FileReadCode Construct(int screenW, int screenH, int pixelW, int pixelH,
        bool fullScreen = false, bool vsync = false, bool cohesion = false, bool realWindow = false)
    {
        PixelCohesion = cohesion;
        RealWindowMode = realWindow;
        ScreenSize = new Vector2d<int>(screenW, screenH);
        InvScreenSize = new Vector2d<float>(1.0f / screenW, 1.0f / screenH);
        PixelSize = new Vector2d<int>(pixelW, pixelH);
        WindowSize = ScreenSize * PixelSize;
        FullScreen = fullScreen;
        EnableVSYNC = vsync;
        VectorPixel = new Vector2d<float>(2.0f / screenW, 2.0f / screenH);

        if (PixelSize.X <= 0 || PixelSize.Y <= 0 || ScreenSize.X <= 0 || ScreenSize.Y <= 0)
            return FileReadCode.Fail;
        return FileReadCode.Ok;
    }

    /// <summary>Wires the platform/renderer, creates the window + GL context, and runs the core loop until the window closes.</summary>
    /// <returns><c>FileReadCode.Ok</c> on a clean run and shutdown; <c>FileReadCode.Fail</c> if platform startup, window/graphics creation, or cleanup fails.</returns>
    /// <remarks>
    /// <para>Call after a successful <see cref="Construct(int, int, int, int, bool, bool, bool, bool)"/>. Unlike olc, which splits the OS event loop and GL context across two threads, this runs the whole lifecycle on the calling thread: create window, create graphics, then loop <c>CoreUpdate</c> until the window is closed.</para>
    /// <para>Extension <c>OnBeforeUserCreate</c>/<c>OnAfterUserCreate</c> bracket <see cref="OnCreate"/>; a close request is offered to <see cref="OnDestroy"/>, which may veto it.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var app = new App();
    /// if (app.Construct(256, 240, 4, 4) == FileReadCode.Ok)
    ///     app.Start(); // blocks until the window closes
    /// </code>
    /// </example>
    /// <seealso cref="Construct(int, int, int, int, bool, bool, bool, bool)"/>
    public FileReadCode Start()
    {
        // Wire up the platform + renderer and the static back-references olc keeps as globals.
        _platform = new PlatformOpenTK();
        _renderer = CreateRenderer();
        Renderer.Active = _renderer;
        Renderer.PtrPGE = this;
        Platform.PtrPGE = this;

        if (_platform.ApplicationStartUp() != FileReadCode.Ok) return FileReadCode.Fail;

        // Construct the window
        if (_platform.CreateWindowPane(new Vector2d<int>(30, 30), WindowSize, FullScreen) != FileReadCode.Ok)
            return FileReadCode.Fail;
        UpdateWindowSize(WindowSize.X, WindowSize.Y);

        if (_platform.ThreadStartUp() != FileReadCode.Ok) return FileReadCode.Fail;
        PrepareEngine();

        foreach (var ext in _extensions) ext.OnBeforeUserCreate();
        _active = OnCreate();
        foreach (var ext in _extensions) ext.OnAfterUserCreate();

        while (_active)
        {
            CoreUpdate();
            if (_platform.ShouldClose) _active = false;
            // When shutdown is requested, let the user veto it (olc's OnUserDestroy contract).
            if (!_active && !OnDestroy()) _active = true;
        }

        _platform.ThreadCleanUp();
        if (_platform.ApplicationCleanUp() != FileReadCode.Ok) return FileReadCode.Fail;
        return FileReadCode.Ok;
    }

    /// <summary>Creates the GL graphics device, builds the font sheet, and sets up layer 0 and the initial draw target.</summary>
    /// <remarks>
    /// Runs once before the core loop starts. The GL context becomes current on this thread inside
    /// <c>CreateGraphics</c>; if that fails the method returns early without building the font or
    /// layers. Creates the always-present layer 0, marks it shown and updated, sets it as the default
    /// draw target, and starts the frame clock.
    /// </remarks>
    private void PrepareEngine()
    {
        // Start OpenGL; the context becomes current on this thread inside CreateGraphics.
        if (_platform.CreateGraphics(FullScreen, EnableVSYNC, ViewPos, ViewSize) == FileReadCode.Fail)
            return;

        ConstructFontSheet();

        // Create primary layer "0"
        CreateLayer();
        _layers[0].BUpdate = true;
        _layers[0].BShow = true;
        SetDrawTarget(null);

        _clock.Restart();
        _lastTime = 0;
    }

    /// <summary>Runs one frame: timing, event pump, resize/dropped-file handling, input scan, user update, layer/decal/GPU flush, present, and FPS title update.</summary>
    /// <remarks>
    /// <para>
    /// The single-threaded heart of the engine loop. In order: computes elapsed time; pumps OS/window
    /// events; detects a window resize (the pull-model stand-in for olc's OS resize callback); consumes
    /// any dropped files (valid for one frame); polls and scans hardware input into pressed/released/held
    /// edges; builds the per-frame key-press cache and drives text-entry/console; invokes extension hooks
    /// around the user <c>OnUpdate</c>; then, unless manual-render mode is active, flushes each visible
    /// layer's texture, GPU tasks, and decals to the renderer and presents the frame. The window title is
    /// updated with the FPS once per second.
    /// </para>
    /// <para>
    /// Per-frame pooling: GPU tasks and decal instances are drained up to their live counts, after which
    /// the counts are reset to zero rather than the lists cleared, so the pooled objects are reused next
    /// frame with no per-frame allocation.
    /// </para>
    /// </remarks>
    private void CoreUpdate()
    {
        // Handle timing
        var now = _clock.Elapsed.TotalSeconds;
        var elapsedTime = (float)(now - _lastTime);
        _lastTime = now;
        LastElapsed = elapsedTime;

        // Pump OS/window events for this frame
        _platform.HandleSystemEvent();

        // Detect window resize (pull-model equivalent of olc's OS resize callback). Recomputes
        // the letterbox viewport now, and flags a screen-buffer resize for real-window mode.
        var clientSize = _platform.ClientSize;
        if (clientSize.X > 0 && clientSize.Y > 0 && (clientSize.X != WindowSize.X || clientSize.Y != WindowSize.Y))
            UpdateWindowSize(clientSize.X, clientSize.Y);

        // Dropped files are valid for the frame after the drop, then cleared (matches olc).
        if (_platform.TryConsumeDroppedFiles(out var dropped, out var dropPoint))
        {
            _droppedFiles = dropped;
            UpdateDroppedFilesPoint(dropPoint.X, dropPoint.Y);
        }
        else if (_droppedFiles.Count > 0)
        {
            _droppedFiles = NoFiles;
        }

        // Poll hardware input, then resolve this frame's pressed/released/held transitions
        PollInput();
        ScanHardware(_keyboardState, _keyOldState, _keyNewState);
        ScanHardware(_mouseButtonState, _mouseOldState, _mouseNewState);

        // Cache mouse coordinates / wheel so they stay consistent across the frame
        _mousePos = _mousePosCache;
        _mouseWheelDelta = _mouseWheelDeltaCache;
        _mouseWheelDeltaCache = 0;

        // Build this frame's key-press cache (keys that just transitioned down) and feed it to the
        // text-entry / console subsystems.
        _keyPressCache.Clear();
        for (var k = 1; k < _keyboardState.Length; k++)
            if (_keyboardState[k].Pressed) _keyPressCache.Add(k);
        if (_textEntryEnable) UpdateTextEntry();

        // The console can freeze game time while it is open.
        if (_consoleSuspendTime) elapsedTime = 0f;

        // Handle frame update. Extensions may modify the frame time or block the user frame
        // entirely (e.g. a splash screen plays before the game starts).
        var blockFrame = false;
        foreach (var ext in _extensions) blockFrame |= ext.OnBeforeUserUpdate(ref elapsedTime);
        if (!blockFrame)
        {
            if (!OnUpdate(elapsedTime))
                _active = false;
        }
        foreach (var ext in _extensions) ext.OnAfterUserUpdate(elapsedTime);

        if (RealWindowMode)
        {
            PixelSize = new Vector2d<int>(1, 1);
            ViewSize = ScreenSize;
            ViewPos = new Vector2d<int>(0, 0);
        }

        // The automatic render (console overlay + per-layer flush) is skipped in manual-render mode;
        // the user then drives it explicitly via adv_HardwareClip / adv_FlushLayer*.
        if (!_manualRenderEnable)
        {
            // The console overlay draws (as decals) into layer 0, on top of the user's frame.
            if (_consoleShow)
            {
                SetDrawTarget(0);
                UpdateConsole();
            }

            _renderer.UpdateViewport(ViewPos, ViewSize);
            _renderer.ClearBuffer(Pixel.BLACK, true);

            // Layer 0 must always exist and is always shown/updated
            _layers[0].BUpdate = true;
            _layers[0].BShow = true;
            SetDecalMode(DecalMode.Normal); // resets the engine's decal mode for this frame's submissions
            _renderer.PrepareDrawing();

            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                if (!layer.BShow) continue;

                if (layer.FuncHook == null)
                {
                    _renderer.ApplyTexture((uint)layer.PDrawTarget.Decal.Id);
                    if (!_suspendTextureTransfer && layer.BUpdate)
                    {
                        layer.PDrawTarget.Decal.Update();
                        layer.BUpdate = false;
                    }

                    _renderer.DrawLayerQuad(layer.VOffset, layer.VScale, layer.Tint);

                    // GPU tasks (2D/3D objects). Flush the live entries, then reset the count so the
                    // pooled GPUTask objects are reused next frame (no per-frame allocation).
                    for (var k = 0; k < layer.GpuTaskCount; k++)
                        _renderer.DoGPUTask(layer.VecGPUTasks[k]);
                    layer.GpuTaskCount = 0;

                    // Decals, in submission order (same pooling: reset the count, keep the objects).
                    for (var k = 0; k < layer.DecalInstanceCount; k++)
                        _renderer.DrawDecal(layer.VecDecalInstance[k]);
                    layer.DecalInstanceCount = 0;
                }
                else
                {
                    layer.FuncHook();
                }
            }
        }

        // Free textures whose Decals were finalized since the last frame. This runs on the GL thread
        // (where the context is current); ~Decal() only enqueues, because GL is invalid on the GC
        // finalizer thread. Done before DisplayFrame so it always runs, including manual-render mode.
        _renderer.ProcessPendingTextureDeletes();

        // Present to screen
        _renderer.DisplayFrame();

        // In real-window mode a resize rebuilds the screen sprite at the new size.
        if (_resizeRequested)
        {
            _resizeRequested = false;
            SetScreenSize(WindowSize.X, WindowSize.Y);
            _renderer.UpdateViewport(new Vector2d<int>(0, 0), WindowSize);
        }

        // Update title bar with FPS once per second
        _frameTimer += elapsedTime;
        _frameCount++;
        if (_frameTimer >= 1.0f)
        {
            LastFps = (uint)_frameCount;
            _frameTimer -= 1.0f;
            _platform.SetWindowTitle($"PixelSharper - {ApplicationName} - FPS: {_frameCount}");
            _frameCount = 0;
        }
    }

    // O-------------------------------------------------------------------O
    // | Layers                                                            |
    // O-------------------------------------------------------------------O

    /// <summary>Creates a new layer with its own screen-sized draw target and returns its index.</summary>
    /// <returns>The index of the newly created layer (its position in the layer list).</returns>
    public uint CreateLayer()
    {
        var layer = new LayerDesc();
        layer.PDrawTarget.Create((uint)ScreenSize.X, (uint)ScreenSize.Y, false, true);
        _layers.Add(layer);
        return (uint)_layers.Count - 1;
    }

    /// <summary>Returns the live layer list (olc's GetLayers).</summary>
    /// <returns>The engine's mutable backing layer list; modifying it affects rendering directly.</returns>
    public List<LayerDesc> GetLayers() => _layers;

    /// <summary>Shows or hides a layer.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="show"><c>true</c> to render the layer; <c>false</c> to hide it.</param>
    public void EnableLayer(byte layer, bool show)
    {
        if (layer < _layers.Count) _layers[layer].BShow = show;
    }

    /// <summary>Sets a layer's screen-quad offset.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="offset">Offset applied to the layer's screen quad, in screen (pixel) space.</param>
    /// <seealso cref="SetLayerOffset(byte, float, float)"/>
    public void SetLayerOffset(byte layer, Vector2d<float> offset)
    {
        if (layer < _layers.Count) _layers[layer].VOffset = offset;
    }

    /// <summary>Scalar overload of <see cref="SetLayerOffset(byte, Vector2d{float})"/>.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="x">X component of the offset, in screen (pixel) space.</param>
    /// <param name="y">Y component of the offset, in screen (pixel) space.</param>
    /// <seealso cref="SetLayerOffset(byte, Vector2d{float})"/>
    public void SetLayerOffset(byte layer, float x, float y) => SetLayerOffset(layer, new Vector2d<float>(x, y));

    /// <summary>Sets a layer's screen-quad scale.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="scale">Per-axis scale factor applied to the layer's screen quad.</param>
    /// <seealso cref="SetLayerScale(byte, float, float)"/>
    public void SetLayerScale(byte layer, Vector2d<float> scale)
    {
        if (layer < _layers.Count) _layers[layer].VScale = scale;
    }

    /// <summary>Scalar overload of <see cref="SetLayerScale(byte, Vector2d{float})"/>.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="x">Horizontal scale factor.</param>
    /// <param name="y">Vertical scale factor.</param>
    /// <seealso cref="SetLayerScale(byte, Vector2d{float})"/>
    public void SetLayerScale(byte layer, float x, float y) => SetLayerScale(layer, new Vector2d<float>(x, y));

    /// <summary>Sets a layer's tint colour.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="tint">Colour multiplied into the layer's quad when it is drawn.</param>
    public void SetLayerTint(byte layer, Pixel tint)
    {
        if (layer < _layers.Count) _layers[layer].Tint = tint;
    }

    /// <summary>Sets a custom render callback that fully replaces the engine's rendering of a layer.</summary>
    /// <param name="layer">Index of the layer to update. Out-of-range indices are ignored.</param>
    /// <param name="f">
    /// Callback invoked in place of the engine's default per-layer flush (texture, GPU tasks, decals).
    /// Pass <c>null</c> to restore default rendering for the layer.
    /// </param>
    public void SetLayerCustomRenderFunction(byte layer, Action f)
    {
        if (layer < _layers.Count) _layers[layer].FuncHook = f;
    }

    // O-------------------------------------------------------------------O
    // | Hardware input                                                    |
    // O-------------------------------------------------------------------O

    /// <summary>Returns the pressed/released/held state of a keyboard key.</summary>
    /// <param name="key">The key to query.</param>
    /// <returns>The key's <see cref="HardwareButton"/> state for this frame.</returns>
    public HardwareButton GetKey(KeyPress key) => _keyboardState[(int)key];
    /// <summary>Returns the pressed/released/held state of a mouse button (default if out of range).</summary>
    /// <param name="button">Zero-based mouse button index.</param>
    /// <returns>
    /// The button's <see cref="HardwareButton"/> state for this frame, or <c>default</c> (all flags
    /// clear) when <paramref name="button"/> is outside the valid range.
    /// </returns>
    public HardwareButton GetMouse(int button) =>
        button >= 0 && button < MouseButtonCount ? _mouseButtonState[button] : default;
    /// <summary>Mouse X in screen (pixel) space.</summary>
    /// <returns>The mouse X coordinate in screen (pixel) space.</returns>
    public int GetMouseX() => _mousePos.X;
    /// <summary>Mouse Y in screen (pixel) space.</summary>
    /// <returns>The mouse Y coordinate in screen (pixel) space.</returns>
    public int GetMouseY() => _mousePos.Y;
    /// <summary>Mouse position in screen (pixel) space.</summary>
    /// <returns>The mouse position in screen (pixel) space.</returns>
    public Vector2d<int> GetMousePos() => _mousePos;
    /// <summary>Mouse position in raw window space.</summary>
    /// <returns>The mouse position in raw window space (OS pixels, before letterbox/pixel scaling).</returns>
    public Vector2d<int> GetWindowMouse() => _mouseWindowPos;
    /// <summary>Mouse wheel delta for this frame.</summary>
    /// <returns>The accumulated mouse wheel delta for this frame (positive scrolls up).</returns>
    public int GetMouseWheel() => _mouseWheelDelta;
    /// <summary>Whether the window currently has input focus.</summary>
    /// <returns><c>true</c> if the window has input focus; otherwise <c>false</c>.</returns>
    public bool IsFocused() => _hasInputFocus;

    /// <summary>Frames rendered in the last second (olc-named accessor for <see cref="LastFps"/>).</summary>
    /// <returns>The number of frames rendered during the most recent one-second window.</returns>
    /// <seealso cref="LastFps"/>
    public uint GetFPS() => LastFps;
    /// <summary>Screen size in engine pixels.</summary>
    /// <returns>The screen size in engine pixels.</returns>
    public Vector2d<int> GetScreenSize() => ScreenSize;
    /// <summary>On-screen pixel size (under pixel cohesion).</summary>
    /// <returns>The on-screen pixel size, in OS pixels (accounts for pixel cohesion).</returns>
    public Vector2d<int> GetScreenPixelSize() => ScreenPixelSize;
    /// <summary>Size in OS pixels of one engine pixel.</summary>
    /// <returns>The size, in OS pixels, of a single engine pixel.</returns>
    public Vector2d<int> GetPixelSize() => PixelSize;
    /// <summary>Window size in OS pixels.</summary>
    /// <returns>The window size in OS pixels.</returns>
    public Vector2d<int> GetWindowSize() => WindowSize;
    /// <summary>Window position in OS screen coordinates.</summary>
    /// <returns>The window position in OS screen coordinates.</returns>
    public Vector2d<int> GetWindowPos() => WindowPos;
    /// <summary>Files dropped onto the window this frame (empty otherwise).</summary>
    /// <returns>
    /// The paths of files dropped onto the window this frame; empty when no drop occurred. Valid only
    /// for the frame following the drop.
    /// </returns>
    public IReadOnlyList<string> GetDroppedFiles() => _droppedFiles;
    /// <summary>Pixel-space point at which files were dropped this frame.</summary>
    /// <returns>The screen (pixel) space point at which the most recent file drop occurred.</returns>
    public Vector2d<int> GetDroppedFilesPoint() => _droppedFilesPoint;

    /// <summary>Reads raw key/mouse/wheel/focus state from the platform into the new-state arrays and the mouse caches.</summary>
    /// <remarks>
    /// The producer half of the pull-model input pipeline: each frame it snapshots the platform's raw
    /// down/up state into <c>_keyNewState</c>/<c>_mouseNewState</c> (and caches mouse position, wheel,
    /// and focus). <see cref="ScanHardware(HardwareButton[], bool[], bool[])"/> then derives the
    /// pressed/released/held edges from those raw states.
    /// </remarks>
    private void PollInput()
    {
        for (var k = 0; k < _keyNewState.Length; k++)
            _keyNewState[k] = _platform.IsKeyDown((KeyPress)k);
        for (var b = 0; b < _mouseNewState.Length; b++)
            _mouseNewState[b] = _platform.IsMouseDown(b);

        var (mx, my) = _platform.MousePosition;
        UpdateMouse(mx, my);
        _mouseWheelDeltaCache = (int)(_platform.MouseScroll * 120.0f);
        _hasInputFocus = _platform.IsFocused;
    }

    /// <summary>Port of olc's per-frame ScanHardware: derives Pressed/Released/Held edges from new vs. previous raw button states, in place.</summary>
    /// <param name="buttons">
    /// The per-key/per-button <see cref="HardwareButton"/> states to update in place; each element's
    /// Pressed/Released/Held flags are recomputed for this frame.
    /// </param>
    /// <param name="oldState">Raw down/up state from the previous frame; overwritten with <paramref name="newState"/> on return.</param>
    /// <param name="newState">Raw down/up state for the current frame (as filled by <see cref="PollInput"/>).</param>
    /// <remarks>
    /// The consumer half of the pull-model input pipeline. Pressed fires only on a transition to down
    /// that was not already Held; Released fires on a transition to up. All three arrays are indexed in
    /// parallel and are expected to share a length.
    /// </remarks>
    private static void ScanHardware(HardwareButton[] buttons, bool[] oldState, bool[] newState)
    {
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Pressed = false;
            buttons[i].Released = false;
            if (newState[i] != oldState[i])
            {
                if (newState[i])
                {
                    buttons[i].Pressed = !buttons[i].Held;
                    buttons[i].Held = true;
                }
                else
                {
                    buttons[i].Released = true;
                    buttons[i].Held = false;
                }
            }
            oldState[i] = newState[i];
        }
    }

    // O-------------------------------------------------------------------O
    // | Text entry (port of olc's TextEntry* + UpdateTextEntry)           |
    // O-------------------------------------------------------------------O

    /// <summary>Whether text-entry mode is active.</summary>
    private bool _textEntryEnable;
    /// <summary>The current text-entry buffer.</summary>
    private string _textEntryString = "";
    /// <summary>Caret position within the text-entry buffer.</summary>
    private int _textEntryCursor;
    /// <summary>History of accepted console commands.</summary>
    private readonly List<string> _commandHistory = new();
    /// <summary>Index into the command history while browsing (equals count when not browsing).</summary>
    private int _commandHistoryIndex; // == count when not browsing history
    /// <summary>Keys (as int) that transitioned to Pressed this frame.</summary>
    private readonly List<int> _keyPressCache = new();
    /// <summary>Lazily-built table mapping a key + modifier to the character(s) it yields.</summary>
    private Dictionary<KeyPress, (string Normal, string Shift, string Ctrl, string Alt)> _keyboardMap = null!;
    /// <summary>Whether the built-in console overlay is showing.</summary>
    private bool _consoleShow;
    /// <summary>Whether game time is frozen while the console is open.</summary>
    private bool _consoleSuspendTime;

    /// <summary>Begins or ends text-entry mode, seeding the buffer and cursor with the supplied text.</summary>
    /// <param name="enable"><c>true</c> to start text entry; <c>false</c> to stop it.</param>
    /// <param name="text">Initial buffer contents when enabling; the cursor is placed at its end. Ignored when disabling.</param>
    public void TextEntryEnable(bool enable, string text = "")
    {
        if (enable)
        {
            _textEntryCursor = text.Length;
            _textEntryString = text;
            _textEntryEnable = true;
        }
        else
        {
            _textEntryEnable = false;
        }
    }

    /// <summary>The current text-entry buffer contents.</summary>
    /// <returns>The text typed so far in the active (or last) text-entry session.</returns>
    public string TextEntryGetString() => _textEntryString;
    /// <summary>The current text-entry caret position.</summary>
    /// <returns>The caret's character index within the text-entry buffer.</returns>
    public int TextEntryGetCursor() => _textEntryCursor;
    /// <summary>Whether text-entry mode is active.</summary>
    /// <returns><c>true</c> while text entry is active; otherwise <c>false</c>.</returns>
    public bool IsTextEntryEnabled() => _textEntryEnable;

    /// <summary>[ADVANCED] Keys that transitioned to Pressed this frame (<see cref="KeyPress"/> values cast to int).</summary>
    /// <returns>A read-only view of the per-frame key-press cache; each entry is a <see cref="KeyPress"/> value as an int.</returns>
    /// <remarks>[ADVANCED] Rebuilt every frame from the Pressed edges; feed entries through <see cref="ConvertKeycode"/> to recover the <see cref="KeyPress"/>.</remarks>
    public IReadOnlyList<int> GetKeyPressCache() => _keyPressCache;

    /// <summary>[ADVANCED] Range-checked cast of a raw keycode to a <see cref="KeyPress"/> (our keycodes are <see cref="KeyPress"/> values).</summary>
    /// <param name="keycode">The raw keycode (a <see cref="KeyPress"/> value as an int) to convert.</param>
    /// <returns>The matching <see cref="KeyPress"/> when <paramref name="keycode"/> is in range; otherwise <c>KeyPress.None</c>.</returns>
    /// <remarks>[ADVANCED]</remarks>
    public KeyPress ConvertKeycode(int keycode)
        => keycode > 0 && keycode < (int)KeyPress.EnumEnd ? (KeyPress)keycode : KeyPress.None;

    /// <summary>[ADVANCED] Character(s) a key yields under the given modifiers; navigation keys return command symbols.</summary>
    /// <param name="key">The key to look up in the keyboard symbol table.</param>
    /// <param name="shift">Whether SHIFT is held; selects the shifted symbol.</param>
    /// <param name="ctrl">Whether CTRL is held; selects the control symbol.</param>
    /// <param name="alt">Whether ALT is held; selects the alt symbol.</param>
    /// <returns>The character(s) for the key under the active modifier, or an empty string when the key has no mapping.</returns>
    /// <remarks>
    /// [ADVANCED] Reads the table built by <see cref="BuildKeyboardMap"/>. Navigation keys return command symbols rather than literal characters:
    /// <list type="table">
    /// <item><term><c>"_L"</c></term><description>move caret left.</description></item>
    /// <item><term><c>"_R"</c></term><description>move caret right.</description></item>
    /// <item><term><c>"_U"</c></term><description>recall an older history command.</description></item>
    /// <item><term><c>"_D"</c></term><description>recall a newer history command.</description></item>
    /// <item><term><c>"_X"</c></term><description>delete the character at the caret.</description></item>
    /// </list>
    /// Backspace yields <c>"\b"</c> and ENTER yields <c>"\n"</c>.
    /// </remarks>
    public string GetKeySymbol(KeyPress key, bool shift = false, bool ctrl = false, bool alt = false)
    {
        _keyboardMap ??= BuildKeyboardMap();
        if (!_keyboardMap.TryGetValue(key, out var e)) return "";
        if (shift) return e.Shift;
        if (ctrl) return e.Ctrl;
        if (alt) return e.Alt;
        return e.Normal;
    }

    /// <summary>Accumulates this frame's typed keys into the text-entry buffer, handling cursor/history/backspace/delete/enter (port of UpdateTextEntry).</summary>
    /// <remarks>
    /// Iterates the per-frame key-press cache, resolving each key via <see cref="GetKeySymbol"/>. The navigation command symbols are interpreted here:
    /// <c>"_L"</c>/<c>"_R"</c> move the caret, <c>"_U"</c>/<c>"_D"</c> browse command history, <c>"_X"</c> deletes forward, backspace deletes backward,
    /// and ENTER either runs the console command (via <see cref="OnConsoleCommand"/>) or completes entry (via <see cref="OnTextEntryComplete"/>).
    /// </remarks>
    private void UpdateTextEntry()
    {
        var shift = GetKey(KeyPress.Shift).Held;
        var ctrl = GetKey(KeyPress.Control).Held;
        foreach (var keycode in _keyPressCache)
        {
            var sym = GetKeySymbol(ConvertKeycode(keycode), shift, ctrl);
            switch (sym)
            {
                case "_L": _textEntryCursor = Math.Max(0, _textEntryCursor - 1); break;
                case "_R": _textEntryCursor = Math.Min(_textEntryString.Length, _textEntryCursor + 1); break;
                case "\b" when _textEntryCursor > 0:
                    _textEntryString = _textEntryString.Remove(_textEntryCursor - 1, 1);
                    _textEntryCursor = Math.Max(0, _textEntryCursor - 1);
                    break;
                case "_X" when _textEntryCursor < _textEntryString.Length:
                    _textEntryString = _textEntryString.Remove(_textEntryCursor, 1);
                    break;
                case "_U": // recall an older command from history
                    if (_commandHistory.Count > 0)
                    {
                        if (_commandHistoryIndex > 0) _commandHistoryIndex--;
                        _textEntryString = _commandHistory[_commandHistoryIndex];
                        _textEntryCursor = _textEntryString.Length;
                    }
                    break;
                case "_D": // recall a newer command from history (or clear past the end)
                    if (_commandHistory.Count > 0 && _commandHistoryIndex < _commandHistory.Count)
                    {
                        _commandHistoryIndex++;
                        if (_commandHistoryIndex < _commandHistory.Count)
                        {
                            _textEntryString = _commandHistory[_commandHistoryIndex];
                            _textEntryCursor = _textEntryString.Length;
                        }
                        else { _textEntryString = ""; _textEntryCursor = 0; }
                    }
                    break;
                case "\n":
                    if (_consoleShow)
                    {
                        if (OnConsoleCommand(_textEntryString))
                        {
                            _commandHistory.Add(_textEntryString);
                            _commandHistoryIndex = _commandHistory.Count;
                        }
                        _textEntryString = "";
                        _textEntryCursor = 0;
                    }
                    else
                    {
                        OnTextEntryComplete(_textEntryString);
                        TextEntryEnable(false);
                    }
                    break;
                default:
                    if (sym.Length == 1)
                    {
                        _textEntryString = _textEntryString.Insert(_textEntryCursor, sym);
                        _textEntryCursor++;
                    }
                    break;
            }
        }
    }

    /// <summary>Override: called with the final string when the user presses ENTER to finish text entry.</summary>
    /// <param name="text">The completed text-entry buffer contents.</param>
    protected virtual void OnTextEntryComplete(string text) { }
    /// <summary>Override: called when a console command is entered; return true to record it in history.</summary>
    /// <param name="command">The command line the user submitted in the console.</param>
    /// <returns><c>true</c> to record <paramref name="command"/> in the command history; <c>false</c> to discard it. The base implementation returns <c>false</c>.</returns>
    protected virtual bool OnConsoleCommand(string command) => false;

    /// <summary>Builds the olc keyboard symbol table mapping each key to its normal/shift/ctrl/alt character(s).</summary>
    /// <returns>A dictionary keyed by <see cref="KeyPress"/> whose value is the (normal, shift, ctrl, alt) symbol tuple consumed by <see cref="GetKeySymbol"/>.</returns>
    private static Dictionary<KeyPress, (string, string, string, string)> BuildKeyboardMap()
    {
        var m = new Dictionary<KeyPress, (string, string, string, string)>();
        for (var k = KeyPress.A; k <= KeyPress.Z; k++)
        {
            var lower = ((char)('a' + (k - KeyPress.A))).ToString();
            m[k] = (lower, lower.ToUpperInvariant(), lower, lower);
        }
        var digitShift = new[] { ")", "!", "\"", "#", "$", "%", "^", "&", "*", "(" };
        for (var i = 0; i < 10; i++)
            m[KeyPress.K0 + i] = (i.ToString(), digitShift[i], i.ToString(), i.ToString());
        for (var i = 0; i < 10; i++)
            m[KeyPress.Num0 + i] = (i.ToString(), i.ToString(), i.ToString(), i.ToString());
        m[KeyPress.NumMul] = ("*", "*", "", "");
        m[KeyPress.NumDiv] = ("/", "/", "", "");
        m[KeyPress.NumAdd] = ("+", "+", "", "");
        m[KeyPress.NumSub] = ("-", "-", "", "");
        m[KeyPress.NumDecimal] = (".", ".", "", "");
        m[KeyPress.Period] = (".", ">", "", "");
        m[KeyPress.EqualsKey] = ("=", "+", "", "");
        m[KeyPress.Comma] = (",", "<", "", "");
        m[KeyPress.Minus] = ("-", "_", "", "");
        m[KeyPress.Space] = (" ", " ", "", "");
        m[KeyPress.Enter] = ("\n", "\n ", "\n", "\n");
        m[KeyPress.Oem1] = (";", ":", "", "");
        m[KeyPress.Oem2] = ("/", "?", "", "");
        m[KeyPress.Oem3] = ("'", "@", "", "");
        m[KeyPress.Oem4] = ("[", "{", "", "");
        m[KeyPress.Oem5] = ("\\", "|", "", "");
        m[KeyPress.Oem6] = ("]", "}", "", "");
        m[KeyPress.Oem7] = ("#", "~", "", "");
        m[KeyPress.Tab] = ("\t", "\t", "\t", "\t");
        m[KeyPress.Back] = ("\b", "\b", "\b", "\b");
        m[KeyPress.Delete] = ("_X", "_X", "_X", "_X");
        m[KeyPress.Left] = ("_L", "_L", "_L", "_L");
        m[KeyPress.Right] = ("_R", "_R", "_R", "_R");
        m[KeyPress.Up] = ("_U", "_U", "_U", "_U");
        m[KeyPress.Down] = ("_D", "_D", "_D", "_D");
        return m;
    }

    // O-------------------------------------------------------------------O
    // | Built-in console (port of olc's Console* + UpdateConsole)         |
    // O-------------------------------------------------------------------O

    /// <summary>Key that closes the console.</summary>
    private KeyPress _consoleExitKey = KeyPress.Escape;
    /// <summary>The wrapped/scrolled lines of console output.</summary>
    private readonly List<string> _consoleLines = new();
    /// <summary>Write cursor (column, row) within the console line buffer.</summary>
    private Vector2d<int> _consoleCursor;
    /// <summary>Per-character text scale used when drawing the console overlay.</summary>
    private Vector2d<float> _consoleCharacterScale = new(1, 1);
    /// <summary>Console size in characters (columns, rows).</summary>
    private Vector2d<int> _consoleSize;
    /// <summary>Pending console output awaiting drain into the line buffer.</summary>
    private readonly StringBuilder _consoleOutputBuffer = new();
    /// <summary>Lazily-created writer over the output buffer.</summary>
    private TextWriter _consoleOutputWriter = null!;
    /// <summary>Saved System.Console.Out, restored when stdout capture is disabled.</summary>
    private TextWriter? _originalConsoleOut;

    /// <summary>Returns a TextWriter that feeds the console buffer (drained by UpdateConsole).</summary>
    /// <returns>A lazily-created <see cref="TextWriter"/> whose output is queued and drained into the console line buffer by <see cref="UpdateConsole"/>.</returns>
    /// <remarks>Pass this to <see cref="ConsoleCaptureStdOut"/> to redirect <c>System.Console.Out</c> into the overlay.</remarks>
    public TextWriter ConsoleOut() => _consoleOutputWriter ??= new StringWriter(_consoleOutputBuffer);
    /// <summary>Whether the console overlay is showing.</summary>
    /// <returns><c>true</c> while the built-in console overlay is visible; otherwise <c>false</c>.</returns>
    public bool IsConsoleShowing() => _consoleShow;

    /// <summary>Opens the console (enabling text entry); keyExit closes it and suspendTime freezes game time.</summary>
    /// <param name="keyExit">The key that closes the console.</param>
    /// <param name="suspendTime">When <c>true</c>, game time is frozen while the console is open.</param>
    /// <remarks>Enables text entry and swallows the toggle key's edge so it doesn't immediately close the console. No-op if the console is already showing.</remarks>
    public void ConsoleShow(KeyPress keyExit, bool suspendTime = true)
    {
        if (_consoleShow) return;
        _consoleShow = true;
        _consoleSuspendTime = suspendTime;
        TextEntryEnable(true);
        _consoleExitKey = keyExit;
        // Swallow the toggle key's edge so it doesn't immediately close the console.
        _keyboardState[(int)keyExit].Held = false;
        _keyboardState[(int)keyExit].Pressed = false;
        _keyboardState[(int)keyExit].Released = true;
    }

    /// <summary>Clears the console line buffer.</summary>
    public void ConsoleClear() => _consoleLines.Clear();

    /// <summary>Redirects System.Console output into the console buffer, or restores the original output.</summary>
    /// <param name="capture">When <c>true</c>, redirects <c>System.Console.Out</c> to <see cref="ConsoleOut"/>; when <c>false</c>, restores the previously saved output.</param>
    /// <remarks>Drained into the overlay by <see cref="UpdateConsole"/>.</remarks>
    public void ConsoleCaptureStdOut(bool capture)
    {
        if (capture)
        {
            _originalConsoleOut = Console.Out;
            Console.SetOut(ConsoleOut());
        }
        else if (_originalConsoleOut != null)
        {
            Console.SetOut(_originalConsoleOut);
        }
    }

    /// <summary>Per-frame console logic: handles the exit key, sizing, draining queued output, and drawing the overlay (shadow, lines, input line + cursor) as decals.</summary>
    /// <remarks>Drains the buffer fed by <see cref="ConsoleOut"/>/<see cref="ConsoleCaptureStdOut"/> via <see cref="TypeCharacter"/>, then draws the overlay.</remarks>
    private void UpdateConsole()
    {
        if (GetKey(_consoleExitKey).Pressed)
        {
            TextEntryEnable(false);
            _consoleSuspendTime = false;
            _consoleShow = false;
            return;
        }

        // Size the console in screen characters (kept in real screen dimensions).
        _consoleCharacterScale = new Vector2d<float>(1f / (ViewSize.X * InvScreenSize.X), 2f / (ViewSize.Y * InvScreenSize.Y));
        _consoleSize = new Vector2d<int>(ViewSize.X / 8 - 2, ViewSize.Y / 16 - 4);

        // If the console changed size, reset the line buffer.
        if (_consoleSize.Y != _consoleLines.Count)
        {
            _consoleCursor = new Vector2d<int>(0, 0);
            _consoleLines.Clear();
            for (var i = 0; i < _consoleSize.Y; i++) _consoleLines.Add("");
        }

        // Drain queued output into the console lines.
        if (_consoleOutputBuffer.Length > 0)
        {
            var output = _consoleOutputBuffer.ToString();
            _consoleOutputBuffer.Clear();
            foreach (var c in output) TypeCharacter(c);
        }

        // Draw the dim shadow, the buffered lines, and the input line with a cursor.
        GradientFillRectDecal(new Vector2d<float>(0, 0), new Vector2d<float>(ScreenSize.X, ScreenSize.Y),
            new Pixel(0, 0, 128, 128), new Pixel(0, 0, 64, 128), new Pixel(0, 0, 64, 128), new Pixel(0, 0, 64, 128));
        SetDecalMode(DecalMode.Normal);
        for (var line = 0; line < _consoleSize.Y && line < _consoleLines.Count; line++)
            DrawStringDecal(new Vector2d<float>(1 * _consoleCharacterScale.X * 8f, (1 + line) * _consoleCharacterScale.Y * 8f),
                _consoleLines[line], Pixel.WHITE, _consoleCharacterScale);

        FillRectDecal(new Vector2d<float>((TextEntryGetCursor() + 2) * _consoleCharacterScale.X * 8f, _consoleSize.Y * _consoleCharacterScale.Y * 8f),
            new Vector2d<float>(8f * _consoleCharacterScale.X, 8f * _consoleCharacterScale.Y), Pixel.DARK_CYAN);
        DrawStringDecal(new Vector2d<float>(1 * _consoleCharacterScale.X * 8f, _consoleSize.Y * _consoleCharacterScale.Y * 8f),
            ">" + TextEntryGetString(), Pixel.YELLOW, _consoleCharacterScale);
    }

    /// <summary>Appends a character to the console line buffer, wrapping at the right edge and scrolling when full.</summary>
    /// <param name="c">The character to append; a newline forces a line break, and printable characters advance the write cursor.</param>
    private void TypeCharacter(char c)
    {
        if (c >= 32 && c < 127)
        {
            _consoleLines[_consoleCursor.Y] += c;
            _consoleCursor.X++;
        }
        if (c == '\n' || _consoleCursor.X >= _consoleSize.X)
        {
            _consoleCursor.Y++;
            _consoleCursor.X = 0;
        }
        if (_consoleCursor.Y >= _consoleSize.Y)
        {
            _consoleCursor.Y = _consoleSize.Y - 1;
            for (var i = 1; i < _consoleSize.Y; i++) _consoleLines[i - 1] = _consoleLines[i];
            _consoleLines[_consoleCursor.Y] = "";
        }
    }

    // O-------------------------------------------------------------------O
    // | [ADVANCED] Manual render controls (port of olc's adv_*) + window  |
    // O-------------------------------------------------------------------O

    /// <summary>Whether automatic layer rendering in CoreUpdate is suppressed in favour of manual adv_* calls.</summary>
    private bool _manualRenderEnable;

    /// <summary>When enabled, CoreUpdate stops auto-rendering layers; drive rendering yourself with the adv_* calls.</summary>
    /// <param name="enable"><c>true</c> to suppress the automatic per-frame layer render and take over manually; <c>false</c> to restore automatic rendering.</param>
    /// <remarks>[ADVANCED] While enabled, drive the screen with <see cref="adv_FlushLayer"/>, <see cref="adv_FlushLayerDecals"/>, and <see cref="adv_FlushLayerGPUTasks"/>.</remarks>
    public void adv_ManualRenderEnable(bool enable) => _manualRenderEnable = enable;

    /// <summary>Clips/scales the hardware viewport to a sub-region of the screen (optionally clearing it).</summary>
    /// <param name="clipAndScale">When <c>true</c>, decal coordinates keep mapping over the full screen; when <c>false</c>, they map over the clipped sub-region.</param>
    /// <param name="viewPos">Top-left of the sub-region, in screen pixels.</param>
    /// <param name="viewSize">Size of the sub-region, in screen pixels.</param>
    /// <param name="clear">When <c>true</c>, clears the clipped region to black.</param>
    /// <remarks>[ADVANCED] Also updates <c>InvScreenSize</c> so subsequent decal coordinate math matches the chosen mapping (full screen vs. <paramref name="viewSize"/>).</remarks>
    public void adv_HardwareClip(bool clipAndScale, Vector2d<int> viewPos, Vector2d<int> viewSize, bool clear = false)
    {
        var newPosX = (float)viewPos.X / ScreenSize.X;
        var newPosY = (float)viewPos.Y / ScreenSize.Y;
        var newSizeX = (float)viewSize.X / ScreenSize.X;
        var newSizeY = (float)viewSize.Y / ScreenSize.Y;
        _renderer.UpdateViewport(
            new Vector2d<int>(ViewPos.X + (int)(newPosX * ViewSize.X), ViewPos.Y + (int)(newPosY * ViewSize.Y)),
            new Vector2d<int>((int)(newSizeX * ViewSize.X), (int)(newSizeY * ViewSize.Y)));

        if (clear) _renderer.ClearBuffer(Pixel.BLACK, true);

        SetDecalMode(DecalMode.Normal);
        _renderer.PrepareDrawing();

        InvScreenSize = clipAndScale
            ? new Vector2d<float>(1f / ScreenSize.X, 1f / ScreenSize.Y)
            : new Vector2d<float>(1f / viewSize.X, 1f / viewSize.Y);
    }

    /// <summary>Manually renders a layer's draw target as a textured screen quad in NDC space.</summary>
    /// <param name="layerId">Index of the layer whose draw target is rendered.</param>
    /// <remarks>[ADVANCED] Manual counterpart to the automatic layer render in <see cref="CoreUpdate"/>; use after <see cref="adv_ManualRenderEnable"/>.</remarks>
    public void adv_FlushLayer(int layerId)
    {
        var layer = _layers[layerId];
        if (!layer.BShow) return;
        if (layer.FuncHook != null) { layer.FuncHook(); return; }

        _renderer.ApplyTexture((uint)layer.PDrawTarget.Decal.Id);
        if (!_suspendTextureTransfer)
        {
            layer.PDrawTarget.Decal.Update();
            layer.BUpdate = false;
        }

        var posX = layer.VOffset.X * InvScreenSize.X * 2f - 1f;
        var posY = (layer.VOffset.Y * InvScreenSize.Y * 2f - 1f) * -1f;
        var dimX = posX + 2f * (layer.PDrawTarget.Sprite.Width * InvScreenSize.X) * layer.VScale.X;
        var dimY = posY - 2f * (layer.PDrawTarget.Sprite.Height * InvScreenSize.Y) * layer.VScale.Y;

        var di = new DecalInstance
        {
            Decal = layer.PDrawTarget.Decal, Points = 4, Mode = DecalMode.Normal, Structure = DecalStructure.Fan
        };
        di.Pos.Add(new Vector2d<float>(posX, posY));
        di.Pos.Add(new Vector2d<float>(posX, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, posY));
        di.Uv.Add(new Vector2d<float>(0, 0));
        di.Uv.Add(new Vector2d<float>(0, 1));
        di.Uv.Add(new Vector2d<float>(1, 1));
        di.Uv.Add(new Vector2d<float>(1, 0));
        for (var i = 0; i < 4; i++) { di.W.Add(1f); di.Tint.Add(Pixel.WHITE); }
        _renderer.DrawDecal(di);
    }

    /// <summary>Manually flushes a layer's queued decal instances to the renderer.</summary>
    /// <param name="layerId">Index of the layer whose queued decal instances are flushed.</param>
    /// <remarks>[ADVANCED] Manual counterpart to the automatic decal flush in <see cref="CoreUpdate"/>; resets the layer's decal-instance count to zero.</remarks>
    public void adv_FlushLayerDecals(int layerId)
    {
        var layer = _layers[layerId];
        for (var k = 0; k < layer.DecalInstanceCount; k++)
            _renderer.DrawDecal(layer.VecDecalInstance[k]);
        layer.DecalInstanceCount = 0;
    }

    /// <summary>Manually flushes a layer's queued GPU tasks (2D/3D objects) to the renderer.</summary>
    /// <param name="layerId">Index of the layer whose queued GPU tasks are flushed.</param>
    /// <remarks>[ADVANCED] Manual counterpart to the automatic GPU-task flush in <see cref="CoreUpdate"/>; resets the layer's GPU-task count to zero.</remarks>
    public void adv_FlushLayerGPUTasks(int layerId)
    {
        var layer = _layers[layerId];
        for (var k = 0; k < layer.GpuTaskCount; k++)
            _renderer.DoGPUTask(layer.VecGPUTasks[k]);
        layer.GpuTaskCount = 0;
    }

    /// <summary>Sets the window position and size (platform passthrough).</summary>
    /// <param name="pos">New window position, in screen coordinates.</param>
    /// <param name="size">New window size, in pixels.</param>
    /// <returns><c>FileReadCode.Ok</c> on success; <c>FileReadCode.Fail</c> if the platform could not apply the change.</returns>
    public FileReadCode SetWindowSize(Vector2d<int> pos, Vector2d<int> size) => _platform.SetWindowSize(pos, size);
    /// <summary>Shows or hides the window frame/decoration (platform passthrough).</summary>
    /// <param name="showFrame"><c>true</c> to show the window frame/decoration; <c>false</c> to hide it (borderless).</param>
    /// <returns><c>FileReadCode.Ok</c> on success; <c>FileReadCode.Fail</c> if the platform could not apply the change.</returns>
    public FileReadCode ShowWindowFrame(bool showFrame = true) => _platform.ShowWindowFrame(showFrame);

    /// <summary>Transforms a window-space drop location into clamped pixel space (same transform as the mouse).</summary>
    /// <param name="x">Window-space X of the drop location, in pixels.</param>
    /// <param name="y">Window-space Y of the drop location, in pixels.</param>
    private void UpdateDroppedFilesPoint(int x, int y)
    {
        x -= ViewPos.X;
        y -= ViewPos.Y;
        var denomX = WindowSize.X - ViewPos.X * 2;
        var denomY = WindowSize.Y - ViewPos.Y * 2;
        var px = denomX != 0 ? (int)((float)x / denomX * ScreenSize.X) : 0;
        var py = denomY != 0 ? (int)((float)y / denomY * ScreenSize.Y) : 0;
        _droppedFilesPoint = new Vector2d<int>(
            Math.Clamp(px, 0, ScreenSize.X - 1),
            Math.Clamp(py, 0, ScreenSize.Y - 1));
    }

    /// <summary>Transforms window-space mouse coordinates into clamped pixel (screen) space and caches them.</summary>
    /// <param name="x">Window-space X of the mouse, in pixels.</param>
    /// <param name="y">Window-space Y of the mouse, in pixels.</param>
    private void UpdateMouse(int x, int y)
    {
        _mouseWindowPos = new Vector2d<int>(x, y);
        x -= ViewPos.X;
        y -= ViewPos.Y;
        var denomX = WindowSize.X - ViewPos.X * 2;
        var denomY = WindowSize.Y - ViewPos.Y * 2;
        var mx = denomX != 0 ? (int)((float)x / denomX * ScreenSize.X) : 0;
        var my = denomY != 0 ? (int)((float)y / denomY * ScreenSize.Y) : 0;
        _mousePosCache = new Vector2d<int>(
            Math.Clamp(mx, 0, ScreenSize.X - 1),
            Math.Clamp(my, 0, ScreenSize.Y - 1));
    }

    // O-------------------------------------------------------------------O
    // | Draw target + software drawing                                    |
    // O-------------------------------------------------------------------O

    /// <summary>Sets the software draw target to a sprite, or to layer 0 when null.</summary>
    /// <param name="target">The sprite to draw into; pass <c>null</c> to revert the draw target to layer <c>0</c>.</param>
    /// <seealso cref="SetDrawTarget(byte, bool)"/>
    public void SetDrawTarget(Sprite? target)
    {
        if (target != null)
        {
            _drawTarget = target;
        }
        else
        {
            _targetLayer = 0;
            if (_layers.Count > 0)
                _drawTarget = _layers[0].PDrawTarget.Sprite;
        }
    }

    /// <summary>Sets the software draw target to a layer's sprite, optionally marking it dirty for re-upload.</summary>
    /// <param name="layer">Index of the layer whose draw-target sprite becomes the active target; ignored if out of range.</param>
    /// <param name="dirty">When <c>true</c>, flags the layer for re-upload to its texture this frame.</param>
    /// <seealso cref="SetDrawTarget(Sprite)"/>
    public void SetDrawTarget(byte layer, bool dirty = true)
    {
        if (layer >= _layers.Count) return;
        _drawTarget = _layers[layer].PDrawTarget.Sprite;
        _layers[layer].BUpdate = dirty;
        _targetLayer = layer;
    }

    /// <summary>The current software draw-target sprite.</summary>
    /// <returns>The active draw-target <see cref="Sprite"/>.</returns>
    public Sprite GetDrawTarget() => _drawTarget;
    /// <summary>Width of the current draw target (0 if none).</summary>
    /// <returns>The draw target's width in pixels, or <c>0</c> if no draw target is set.</returns>
    public int GetDrawTargetWidth() => _drawTarget?.Width ?? 0;
    /// <summary>Height of the current draw target (0 if none).</summary>
    /// <returns>The draw target's height in pixels, or <c>0</c> if no draw target is set.</returns>
    public int GetDrawTargetHeight() => _drawTarget?.Height ?? 0;
    /// <summary>Screen width in engine pixels.</summary>
    /// <returns>The screen width in engine pixels.</returns>
    public int ScreenWidth() => ScreenSize.X;
    /// <summary>Screen height in engine pixels.</summary>
    /// <returns>The screen height in engine pixels.</returns>
    public int ScreenHeight() => ScreenSize.Y;
    /// <summary>Frames rendered in the last second.</summary>
    /// <returns>The frame count measured over the previous second.</returns>
    public uint GetFps() => LastFps;
    /// <summary>Seconds elapsed during the last frame.</summary>
    /// <returns>The duration of the last frame in seconds.</returns>
    public float GetElapsedTime() => LastElapsed;

    /// <summary>Sets the pixel blend mode (Normal/Mask/Alpha/Custom) used by Draw.</summary>
    /// <param name="mode">The <see cref="PixelDisplayMode"/> that <see cref="Draw(int, int, Pixel)"/> applies to plotted pixels.</param>
    /// <seealso cref="SetPixelMode(Func{int, int, Pixel, Pixel, Pixel})"/>
    /// <seealso cref="SetPixelBlend(float)"/>
    public void SetPixelMode(PixelDisplayMode mode) => _pixelMode = mode;
    /// <summary>The current pixel blend mode.</summary>
    /// <returns>The active <see cref="PixelDisplayMode"/>.</returns>
    public PixelDisplayMode GetPixelMode() => _pixelMode;
    /// <summary>Sets a custom per-pixel blend function and switches to Custom pixel mode.</summary>
    /// <param name="pixelMode">A function taking the pixel's x, y, the source pixel, and the existing destination pixel, returning the blended result; selecting it sets the mode to <see cref="PixelDisplayMode.Custom"/>.</param>
    /// <seealso cref="SetPixelMode(PixelDisplayMode)"/>
    public void SetPixelMode(Func<int, int, Pixel, Pixel, Pixel> pixelMode)
    {
        _funcPixelMode = pixelMode;
        _pixelMode = PixelDisplayMode.Custom;
    }
    /// <summary>Sets the global alpha multiplier used in Alpha pixel mode (clamped to 0..1).</summary>
    /// <param name="blend">The blend factor, clamped to the range <c>0</c>..<c>1</c>, applied to source alpha in <see cref="PixelDisplayMode.Alpha"/> mode.</param>
    /// <seealso cref="SetPixelMode(PixelDisplayMode)"/>
    public void SetPixelBlend(float blend) => _blendFactor = Math.Clamp(blend, 0.0f, 1.0f);

    /// <summary>Plots a pixel into the current draw target according to the active pixel mode; the critical low-level draw primitive.</summary>
    /// <param name="x">Destination x coordinate in the draw target.</param>
    /// <param name="y">Destination y coordinate in the draw target.</param>
    /// <param name="p">The source pixel to plot.</param>
    /// <returns><c>false</c> if there is no draw target or the pixel was rejected (e.g. by the mask in <see cref="PixelDisplayMode.Mask"/> mode); otherwise the result of writing to the draw target.</returns>
    /// <remarks>
    /// All software primitives funnel through this method. The active <see cref="PixelDisplayMode"/> selects the blend:
    /// <list type="bullet">
    /// <item><description><see cref="PixelDisplayMode.Normal"/> — overwrites the destination pixel.</description></item>
    /// <item><description><see cref="PixelDisplayMode.Mask"/> — writes only when the source alpha is fully opaque (<c>255</c>).</description></item>
    /// <item><description><see cref="PixelDisplayMode.Alpha"/> — alpha-blends source over destination, scaled by the global blend factor from <see cref="SetPixelBlend(float)"/>.</description></item>
    /// <item><description><see cref="PixelDisplayMode.Custom"/> — defers to the function set via <see cref="SetPixelMode(Func{int, int, Pixel, Pixel, Pixel})"/>.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Draw(Vector2d{int}, Pixel)"/>
    public virtual bool Draw(int x, int y, Pixel p)
    {
        if (_drawTarget == null) return false;

        switch (_pixelMode)
        {
            case PixelDisplayMode.Normal:
                return _drawTarget.SetPixel(x, y, p);

            case PixelDisplayMode.Mask:
                if (p.Alpha == 255)
                    return _drawTarget.SetPixel(x, y, p);
                return false;

            case PixelDisplayMode.Alpha:
            {
                var d = _drawTarget.GetPixel(x, y);
                var a = p.Alpha / 255.0f * _blendFactor;
                var c = 1.0f - a;
                var r = a * p.Red + c * d.Red;
                var g = a * p.Green + c * d.Green;
                var b = a * p.Blue + c * d.Blue;
                return _drawTarget.SetPixel(x, y, new Pixel((byte)r, (byte)g, (byte)b));
            }

            case PixelDisplayMode.Custom:
                return _drawTarget.SetPixel(x, y, _funcPixelMode(x, y, p, _drawTarget.GetPixel(x, y)));

            default:
                return false;
        }
    }

    /// <summary>Vector2d overload of <see cref="Draw(int, int, Pixel)"/>.</summary>
    /// <param name="pos">Destination coordinate in the draw target.</param>
    /// <param name="p">The source pixel to plot.</param>
    /// <returns>The result of <see cref="Draw(int, int, Pixel)"/>: <c>false</c> if there is no draw target or the pixel was rejected.</returns>
    public bool Draw(Vector2d<int> pos, Pixel p) => Draw(pos.X, pos.Y, p);

    /// <summary>Fills the entire current draw target with a colour via a vectorised span fill.</summary>
    /// <param name="p">The colour to fill the draw target with.</param>
    /// <remarks>Fills the backing array with a single vectorised <c>Span.Fill</c> rather than a per-pixel loop; no-op if there is no draw target.</remarks>
    public void Clear(Pixel p)
    {
        var target = GetDrawTarget();
        if (target == null) return;
        // Vectorised fill over the backing array rather than a per-element List indexer loop.
        CollectionsMarshal.AsSpan(target.PixelData).Fill(p);
    }

    /// <summary>Clears the renderer's framebuffer (and optionally depth) to a colour.</summary>
    /// <param name="p">The colour to clear the framebuffer to.</param>
    /// <param name="depth">When <c>true</c>, the depth buffer is also cleared.</param>
    public void ClearBuffer(Pixel p, bool depth = true) => _renderer.ClearBuffer(p, depth);

    /// <summary>Enables/disables per-frame re-upload of layer draw-target sprites to their textures.</summary>
    /// <param name="enable">When <c>true</c>, dirty layer draw targets are uploaded to their textures each frame; <c>false</c> suspends the transfer.</param>
    public void EnablePixelTransfer(bool enable = true) => _suspendTextureTransfer = !enable;

    // O-------------------------------------------------------------------O
    // | Drawing primitives (software, into the current draw target)       |
    // O-------------------------------------------------------------------O

    /// <summary>Vector2d overload of <see cref="DrawLine(int, int, int, int, Pixel, uint)"/>.</summary>
    /// <param name="pos1">Start point of the line.</param>
    /// <param name="pos2">End point of the line.</param>
    /// <param name="p">The line colour.</param>
    /// <param name="pattern">A 32-bit dash bit-pattern (<c>0xFFFFFFFF</c> draws a solid line).</param>
    public void DrawLine(Vector2d<int> pos1, Vector2d<int> pos2, Pixel p, uint pattern = 0xFFFFFFFF)
        => DrawLine(pos1.X, pos1.Y, pos2.X, pos2.Y, p, pattern);

    /// <summary>Draws a clipped Bresenham line with an optional rotating dash bit-pattern.</summary>
    /// <param name="x1">Start x coordinate.</param>
    /// <param name="y1">Start y coordinate.</param>
    /// <param name="x2">End x coordinate.</param>
    /// <param name="y2">End y coordinate.</param>
    /// <param name="p">The line colour.</param>
    /// <param name="pattern">A 32-bit dash bit-pattern (<c>0xFFFFFFFF</c> draws a solid line).</param>
    /// <remarks>The line is first clipped to the draw target via <see cref="ClipLineToDrawTarget(ref Vector2d{int}, ref Vector2d{int})"/>. The <paramref name="pattern"/> is rotated left one bit per plotted step, and a pixel is drawn only when the rotated-in bit is set — producing dashed lines.</remarks>
    public void DrawLine(int x1, int y1, int x2, int y2, Pixel p, uint pattern = 0xFFFFFFFF)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;

        var pat = pattern;
        bool Rol()
        {
            pat = (pat << 1) | (pat >> 31);
            return (pat & 1) != 0;
        }

        var pp1 = new Vector2d<int>(x1, y1);
        var pp2 = new Vector2d<int>(x2, y2);
        if (!ClipLineToDrawTarget(ref pp1, ref pp2)) return;
        x1 = pp1.X; y1 = pp1.Y;
        x2 = pp2.X; y2 = pp2.Y;

        if (dx == 0) // vertical
        {
            if (y2 < y1) (y1, y2) = (y2, y1);
            for (var y = y1; y <= y2; y++) if (Rol()) Draw(x1, y, p);
            return;
        }

        if (dy == 0) // horizontal
        {
            if (x2 < x1) (x1, x2) = (x2, x1);
            for (var x = x1; x <= x2; x++) if (Rol()) Draw(x, y1, p);
            return;
        }

        // Diagonal (Bresenham)
        var dx1 = Math.Abs(dx);
        var dy1 = Math.Abs(dy);
        var px = 2 * dy1 - dx1;
        var py = 2 * dx1 - dy1;
        int xe, ye;
        if (dy1 <= dx1)
        {
            int xv;
            int yv;
            if (dx >= 0) { xv = x1; yv = y1; xe = x2; }
            else { xv = x2; yv = y2; xe = x1; }

            if (Rol()) Draw(xv, yv, p);

            while (xv < xe)
            {
                xv += 1;
                if (px < 0)
                {
                    px += 2 * dy1;
                }
                else
                {
                    if ((dx < 0 && dy < 0) || (dx > 0 && dy > 0)) yv += 1; else yv -= 1;
                    px += 2 * (dy1 - dx1);
                }
                if (Rol()) Draw(xv, yv, p);
            }
        }
        else
        {
            int xv;
            int yv;
            if (dy >= 0) { xv = x1; yv = y1; ye = y2; }
            else { xv = x2; yv = y2; ye = y1; }

            if (Rol()) Draw(xv, yv, p);

            while (yv < ye)
            {
                yv += 1;
                if (py <= 0)
                {
                    py += 2 * dx1;
                }
                else
                {
                    if ((dx < 0 && dy < 0) || (dx > 0 && dy > 0)) xv += 1; else xv -= 1;
                    py += 2 * (dx1 - dy1);
                }
                if (Rol()) Draw(xv, yv, p);
            }
        }
    }

    /// <summary>Cohen-Sutherland clip of a line segment to the draw-target rectangle; returns false if fully outside.</summary>
    /// <param name="p1">First endpoint; clamped in place to the draw-target rectangle.</param>
    /// <param name="p2">Second endpoint; clamped in place to the draw-target rectangle.</param>
    /// <returns><c>false</c> if the segment lies wholly outside the draw target; otherwise <c>true</c> with the endpoints clipped to the visible region.</returns>
    public bool ClipLineToDrawTarget(ref Vector2d<int> p1, ref Vector2d<int> p2)
    {
        var w = GetDrawTargetWidth();
        var h = GetDrawTargetHeight();
        const int segI = 0b0000, segL = 0b0001, segR = 0b0010, segB = 0b0100, segT = 0b1000;

        int Segment(Vector2d<int> v)
        {
            var i = segI;
            if (v.X < 0) i |= segL; else if (v.X > w) i |= segR;
            if (v.Y < 0) i |= segB; else if (v.Y > h) i |= segT;
            return i;
        }

        var s1 = Segment(p1);
        var s2 = Segment(p2);

        while (true)
        {
            if ((s1 | s2) == 0) return true;
            if ((s1 & s2) != 0) return false;

            var s3 = s2 > s1 ? s2 : s1;
            Vector2d<int> n;
            if ((s3 & segT) != 0) n = new Vector2d<int>(p1.X + (p2.X - p1.X) * (h - p1.Y) / (p2.Y - p1.Y), h);
            else if ((s3 & segB) != 0) n = new Vector2d<int>(p1.X + (p2.X - p1.X) * (0 - p1.Y) / (p2.Y - p1.Y), 0);
            else if ((s3 & segR) != 0) n = new Vector2d<int>(w, p1.Y + (p2.Y - p1.Y) * (w - p1.X) / (p2.X - p1.X));
            else n = new Vector2d<int>(0, p1.Y + (p2.Y - p1.Y) * (0 - p1.X) / (p2.X - p1.X));

            if (s3 == s1) { p1 = n; s1 = Segment(p1); }
            else { p2 = n; s2 = Segment(p2); }
        }
    }

    /// <summary>Vector2d overload of <see cref="DrawCircle(int, int, int, Pixel, byte)"/>.</summary>
    /// <param name="pos">Centre of the circle.</param>
    /// <param name="radius">Circle radius in pixels.</param>
    /// <param name="p">The outline colour.</param>
    /// <param name="mask">Octant selection bitmask (<c>0xFF</c> draws the full circle).</param>
    /// <seealso cref="FillCircle(Vector2d{int}, int, Pixel)"/>
    public void DrawCircle(Vector2d<int> pos, int radius, Pixel p, byte mask = 0xFF)
        => DrawCircle(pos.X, pos.Y, radius, p, mask);

    /// <summary>Draws a circle outline via the midpoint algorithm; mask selects which of the 8 octants to draw.</summary>
    /// <param name="x">Centre x coordinate.</param>
    /// <param name="y">Centre y coordinate.</param>
    /// <param name="radius">Circle radius in pixels.</param>
    /// <param name="p">The outline colour.</param>
    /// <param name="mask">Octant selection bitmask, one bit per octant (<c>0xFF</c> draws all eight, the default).</param>
    /// <seealso cref="FillCircle(int, int, int, Pixel)"/>
    public void DrawCircle(int x, int y, int radius, Pixel p, byte mask = 0xFF)
    {
        if (radius < 0 || x < -radius || y < -radius ||
            x - GetDrawTargetWidth() > radius || y - GetDrawTargetHeight() > radius)
            return;

        if (radius > 0)
        {
            int x0 = 0, y0 = radius, d = 3 - 2 * radius;
            while (y0 >= x0)
            {
                if ((mask & 0x01) != 0) Draw(x + x0, y - y0, p);
                if ((mask & 0x04) != 0) Draw(x + y0, y + x0, p);
                if ((mask & 0x10) != 0) Draw(x - x0, y + y0, p);
                if ((mask & 0x40) != 0) Draw(x - y0, y - x0, p);
                if (x0 != 0 && x0 != y0)
                {
                    if ((mask & 0x02) != 0) Draw(x + y0, y - x0, p);
                    if ((mask & 0x08) != 0) Draw(x + x0, y + y0, p);
                    if ((mask & 0x20) != 0) Draw(x - y0, y + x0, p);
                    if ((mask & 0x80) != 0) Draw(x - x0, y - y0, p);
                }

                if (d < 0) d += 4 * x0++ + 6;
                else d += 4 * (x0++ - y0--) + 10;
            }
        }
        else
        {
            Draw(x, y, p);
        }
    }

    /// <summary>Vector2d overload of <see cref="FillCircle(int, int, int, Pixel)"/>.</summary>
    /// <param name="pos">Centre of the circle.</param>
    /// <param name="radius">Circle radius in pixels.</param>
    /// <param name="p">The fill colour.</param>
    /// <seealso cref="DrawCircle(Vector2d{int}, int, Pixel, byte)"/>
    public void FillCircle(Vector2d<int> pos, int radius, Pixel p) => FillCircle(pos.X, pos.Y, radius, p);

    /// <summary>Fills a solid circle by scanning horizontal spans of the midpoint circle.</summary>
    /// <param name="x">Centre x coordinate.</param>
    /// <param name="y">Centre y coordinate.</param>
    /// <param name="radius">Circle radius in pixels.</param>
    /// <param name="p">The fill colour.</param>
    /// <seealso cref="DrawCircle(int, int, int, Pixel, byte)"/>
    public void FillCircle(int x, int y, int radius, Pixel p)
    {
        if (radius < 0 || x < -radius || y < -radius ||
            x - GetDrawTargetWidth() > radius || y - GetDrawTargetHeight() > radius)
            return;

        if (radius > 0)
        {
            int x0 = 0, y0 = radius, d = 3 - 2 * radius;

            void Scan(int sx, int ex, int ny) { for (var px = sx; px <= ex; px++) Draw(px, ny, p); }

            while (y0 >= x0)
            {
                Scan(x - y0, x + y0, y - x0);
                if (x0 > 0) Scan(x - y0, x + y0, y + x0);

                if (d < 0)
                {
                    d += 4 * x0++ + 6;
                }
                else
                {
                    if (x0 != y0)
                    {
                        Scan(x - x0, x + x0, y - y0);
                        Scan(x - x0, x + x0, y + y0);
                    }
                    d += 4 * (x0++ - y0--) + 10;
                }
            }
        }
        else
        {
            Draw(x, y, p);
        }
    }

    /// <summary>Vector2d overload of <see cref="DrawRect(int, int, int, int, Pixel)"/>.</summary>
    /// <param name="pos">Top-left corner of the rectangle.</param>
    /// <param name="size">Width and height of the rectangle.</param>
    /// <param name="p">The outline colour.</param>
    /// <seealso cref="FillRect(Vector2d{int}, Vector2d{int}, Pixel)"/>
    public void DrawRect(Vector2d<int> pos, Vector2d<int> size, Pixel p) => DrawRect(pos.X, pos.Y, size.X, size.Y, p);

    /// <summary>Draws a rectangle outline as four lines.</summary>
    /// <param name="x">Top-left x coordinate.</param>
    /// <param name="y">Top-left y coordinate.</param>
    /// <param name="w">Rectangle width.</param>
    /// <param name="h">Rectangle height.</param>
    /// <param name="p">The outline colour.</param>
    /// <seealso cref="FillRect(int, int, int, int, Pixel)"/>
    public void DrawRect(int x, int y, int w, int h, Pixel p)
    {
        DrawLine(x, y, x + w, y, p);
        DrawLine(x + w, y, x + w, y + h, p);
        DrawLine(x + w, y + h, x, y + h, p);
        DrawLine(x, y + h, x, y, p);
    }

    /// <summary>Vector2d overload of <see cref="FillRect(int, int, int, int, Pixel)"/>.</summary>
    /// <param name="pos">Top-left corner of the rectangle.</param>
    /// <param name="size">Width and height of the rectangle.</param>
    /// <param name="p">The fill colour.</param>
    /// <seealso cref="DrawRect(Vector2d{int}, Vector2d{int}, Pixel)"/>
    public void FillRect(Vector2d<int> pos, Vector2d<int> size, Pixel p) => FillRect(pos.X, pos.Y, size.X, size.Y, p);

    /// <summary>Fills a clamped rectangle, using vectorised row fills/blends in Normal/Mask/Alpha modes and per-pixel Draw otherwise.</summary>
    /// <param name="x">Top-left x coordinate.</param>
    /// <param name="y">Top-left y coordinate.</param>
    /// <param name="w">Rectangle width.</param>
    /// <param name="h">Rectangle height.</param>
    /// <param name="p">The fill colour.</param>
    /// <remarks>
    /// <para>The rectangle is clamped to the draw target. In <see cref="PixelDisplayMode.Normal"/> mode (or <see cref="PixelDisplayMode.Mask"/> with a fully-opaque pixel) each row is filled with a single vectorised <c>Span.Fill</c>.</para>
    /// <para>In <see cref="PixelDisplayMode.Alpha"/> mode each row is blended via <see cref="BlendRowConstant(Span{Pixel}, Pixel, float)"/>, which takes a SIMD path when supported. Other modes fall back to per-pixel <see cref="Draw(int, int, Pixel)"/>.</para>
    /// </remarks>
    /// <seealso cref="DrawRect(int, int, int, int, Pixel)"/>
    public void FillRect(int x, int y, int w, int h, Pixel p)
    {
        var x2 = x + w;
        var y2 = y + h;
        var dw = GetDrawTargetWidth();
        var dh = GetDrawTargetHeight();

        x = Math.Clamp(x, 0, dw);
        x2 = Math.Clamp(x2, 0, dw);
        y = Math.Clamp(y, 0, dh);
        y2 = Math.Clamp(y2, 0, dh);

        var rowW = x2 - x;
        if (rowW <= 0 || y2 <= y) return;

        // Fast path: in Normal mode (or Mask with a fully-opaque pixel) a fill is a pure overwrite, so
        // fill each destination row in one vectorised Span.Fill instead of per-pixel Draw().
        var target = GetDrawTarget();
        if (target != null && (_pixelMode == PixelDisplayMode.Normal ||
                               (_pixelMode == PixelDisplayMode.Mask && p.Alpha == 255)))
        {
            var buf = CollectionsMarshal.AsSpan(target.PixelData);
            for (var j = y; j < y2; j++)
                buf.Slice(j * dw + x, rowW).Fill(p);
            return;
        }

        // Fast path: Alpha mode with a constant source is a per-channel affine on the destination
        // (out = c*dst + k). Done directly over the row span — same float math as Draw()'s Alpha case
        // (so bit-exact), but without the per-pixel method/switch/indexer overhead. BlendRowConstant
        // also takes the SIMD path internally when the hardware supports it.
        if (target != null && _pixelMode == PixelDisplayMode.Alpha)
        {
            var buf = CollectionsMarshal.AsSpan(target.PixelData);
            for (var j = y; j < y2; j++)
                BlendRowConstant(buf.Slice(j * dw + x, rowW), p, _blendFactor);
            return;
        }

        for (var i = x; i < x2; i++)
            for (var j = y; j < y2; j++)
                Draw(i, j, p);
    }

    /// <summary>Alpha-blends a constant source pixel over a row of destination pixels (bit-exact with Draw's Alpha case, without per-pixel overhead).</summary>
    /// <param name="row">The destination pixel row, blended in place.</param>
    /// <param name="src">The constant source pixel to blend over the row.</param>
    /// <param name="blend">The global blend factor scaling the source alpha.</param>
    /// <remarks>Computes the per-channel affine <c>out = c*dst + k</c> once and applies it across the row, matching <see cref="Draw(int, int, Pixel)"/>'s Alpha case bit-for-bit. Picks the widest hardware-accelerated path the JIT reports — <see cref="Vector512{T}"/> (16 pixels/iter, AVX-512), <see cref="Vector256{T}"/> (8/iter, AVX2), or <see cref="Vector128{T}"/> (4/iter) — falling back to the scalar loop otherwise; all produce identical bytes. (Measured ~5.2x for the 256-wide path over scalar, and a further ~1.37x for the 512-wide path over 256, on a Zen 4 Threadripper; the 512 path is simply skipped where AVX-512 is absent.)</remarks>
    private static void BlendRowConstant(Span<Pixel> row, Pixel src, float blend)
    {
        var a = src.Alpha / 255.0f * blend;
        var c = 1.0f - a;
        float kr = a * src.Red, kg = a * src.Green, kb = a * src.Blue;

        var byteOffset = 0;
        var bytes = MemoryMarshal.AsBytes(row);

        // Vector512: 16 pixels (64 bytes) per iteration on AVX-512 (skipped where unavailable). Same
        // widen -> float -> affine -> narrow as the 256 path, just twice as wide; bit-identical output.
        // Measured ~1.37x over the 256 path on Zen 4. Composes 256-bit halves for Create portability.
        if (Vector512.IsHardwareAccelerated && row.Length >= 16)
        {
            var cv = Vector512.Create(c);
            var kv128 = Vector128.Create(kr, kg, kb, 0f);
            var kv = Vector512.Create(Vector256.Create(kv128, kv128), Vector256.Create(kv128, kv128));
            var alpha128 = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var alpha256 = Vector256.Create(alpha128, alpha128);
            var alphaMask = Vector512.Create(alpha256, alpha256);
            var limit = bytes.Length - (bytes.Length & 63);
            for (; byteOffset < limit; byteOffset += 64)
            {
                var b = Vector512.Create(bytes.Slice(byteOffset, 64));
                var lo = Vector512.WidenLower(b);
                var hi = Vector512.WidenUpper(b);
                var f0 = Vector512.ConvertToSingle(Vector512.WidenLower(lo).AsInt32());
                var f1 = Vector512.ConvertToSingle(Vector512.WidenUpper(lo).AsInt32());
                var f2 = Vector512.ConvertToSingle(Vector512.WidenLower(hi).AsInt32());
                var f3 = Vector512.ConvertToSingle(Vector512.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector512.Narrow(Vector512.ConvertToInt32(f0).AsUInt32(), Vector512.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector512.Narrow(Vector512.ConvertToInt32(f2).AsUInt32(), Vector512.ConvertToInt32(f3).AsUInt32());
                (Vector512.Narrow(u0, u1) | alphaMask).CopyTo(bytes.Slice(byteOffset, 64));
            }
        }
        // Vector256: 8 pixels (32 bytes) per iteration. The float ops are IEEE single-precision and the
        // int conversion truncates toward zero, so each lane equals the scalar (byte)(c*dst+k); alpha is
        // forced to 255 (matching the Pixel(byte,byte,byte) ctor) by ORing 0xFF into each alpha byte.
        else if (Vector256.IsHardwareAccelerated && row.Length >= 8)
        {
            var cv = Vector256.Create(c);
            var kv128 = Vector128.Create(kr, kg, kb, 0f);
            var kv = Vector256.Create(kv128, kv128);
            var alpha128 = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var alphaMask = Vector256.Create(alpha128, alpha128);
            var limit = bytes.Length - (bytes.Length & 31);
            for (; byteOffset < limit; byteOffset += 32)
            {
                var b = Vector256.Create(bytes.Slice(byteOffset, 32));
                var lo = Vector256.WidenLower(b);
                var hi = Vector256.WidenUpper(b);
                var f0 = Vector256.ConvertToSingle(Vector256.WidenLower(lo).AsInt32());
                var f1 = Vector256.ConvertToSingle(Vector256.WidenUpper(lo).AsInt32());
                var f2 = Vector256.ConvertToSingle(Vector256.WidenLower(hi).AsInt32());
                var f3 = Vector256.ConvertToSingle(Vector256.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector256.Narrow(Vector256.ConvertToInt32(f0).AsUInt32(), Vector256.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector256.Narrow(Vector256.ConvertToInt32(f2).AsUInt32(), Vector256.ConvertToInt32(f3).AsUInt32());
                (Vector256.Narrow(u0, u1) | alphaMask).CopyTo(bytes.Slice(byteOffset, 32));
            }
        }
        // Vector128: 4 pixels (16 bytes) per iteration (also handles the Vector256 tail).
        else if (Vector128.IsHardwareAccelerated && row.Length >= 4)
        {
            var cv = Vector128.Create(c);
            var kv = Vector128.Create(kr, kg, kb, 0f);
            var alphaMask = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
            var limit = bytes.Length - (bytes.Length & 15);
            for (; byteOffset < limit; byteOffset += 16)
            {
                var b = Vector128.Create(bytes.Slice(byteOffset, 16));
                var lo = Vector128.WidenLower(b);
                var hi = Vector128.WidenUpper(b);
                var f0 = Vector128.ConvertToSingle(Vector128.WidenLower(lo).AsInt32());
                var f1 = Vector128.ConvertToSingle(Vector128.WidenUpper(lo).AsInt32());
                var f2 = Vector128.ConvertToSingle(Vector128.WidenLower(hi).AsInt32());
                var f3 = Vector128.ConvertToSingle(Vector128.WidenUpper(hi).AsInt32());
                f0 = f0 * cv + kv; f1 = f1 * cv + kv; f2 = f2 * cv + kv; f3 = f3 * cv + kv;
                var u0 = Vector128.Narrow(Vector128.ConvertToInt32(f0).AsUInt32(), Vector128.ConvertToInt32(f1).AsUInt32());
                var u1 = Vector128.Narrow(Vector128.ConvertToInt32(f2).AsUInt32(), Vector128.ConvertToInt32(f3).AsUInt32());
                (Vector128.Narrow(u0, u1) | alphaMask).CopyTo(bytes.Slice(byteOffset, 16));
            }
        }

        // Tail: the SIMD block above advanced byteOffset to the last whole-block boundary; this finishes
        // the leftover pixels (length % blockSize). When no SIMD path ran, byteOffset is still 0, so this
        // does the whole row. Either way each pixel is written exactly once. (4 bytes per pixel.)
        for (var px = byteOffset / 4; px < row.Length; px++)
        {
            var d = row[px];
            row[px] = new Pixel((byte)(c * d.Red + kr), (byte)(c * d.Green + kg), (byte)(c * d.Blue + kb));
        }
    }

    /// <summary>Alpha-blends a variable source row over a destination row (the per-pixel-alpha "source-over" used by Alpha-mode sprite blits), bit-exact with <see cref="Draw(int, int, Pixel)"/>'s Alpha case.</summary>
    /// <param name="dst">The destination pixel row, blended in place.</param>
    /// <param name="src">The source pixel row (each pixel carries its own alpha).</param>
    /// <param name="blend">The global blend factor scaling each source alpha.</param>
    /// <remarks>Scalar by design: unlike the constant-source case, SIMD here must widen both src and dst and broadcast each pixel's alpha, which measured slower than this loop (see BlitBenchmarks). Output alpha is forced to 255, matching the Pixel(byte,byte,byte) ctor used by Draw.</remarks>
    private static void BlendRowOver(Span<Pixel> dst, ReadOnlySpan<Pixel> src, float blend)
    {
        for (var i = 0; i < dst.Length; i++)
        {
            var s = src[i];
            var d = dst[i];
            var a = s.Alpha / 255.0f * blend;
            var c = 1.0f - a;
            dst[i] = new Pixel((byte)(a * s.Red + c * d.Red), (byte)(a * s.Green + c * d.Green), (byte)(a * s.Blue + c * d.Blue));
        }
    }

    /// <summary>Copies a source row over a destination row only where the source pixel is fully opaque, bit-exact with <see cref="Draw(int, int, Pixel)"/>'s Mask case.</summary>
    /// <param name="dst">The destination pixel row, overwritten in place where the source is opaque.</param>
    /// <param name="src">The source pixel row; only pixels with alpha 255 are written.</param>
    private static void MaskRowOver(Span<Pixel> dst, ReadOnlySpan<Pixel> src)
    {
        for (var i = 0; i < dst.Length; i++)
            if (src[i].Alpha == 255) dst[i] = src[i];
    }

    /// <summary>Vector2d overload of <see cref="DrawTriangle(int, int, int, int, int, int, Pixel)"/>.</summary>
    /// <param name="pos1">First vertex.</param>
    /// <param name="pos2">Second vertex.</param>
    /// <param name="pos3">Third vertex.</param>
    /// <param name="p">The outline colour.</param>
    /// <seealso cref="FillTriangle(Vector2d{int}, Vector2d{int}, Vector2d{int}, Pixel)"/>
    public void DrawTriangle(Vector2d<int> pos1, Vector2d<int> pos2, Vector2d<int> pos3, Pixel p)
        => DrawTriangle(pos1.X, pos1.Y, pos2.X, pos2.Y, pos3.X, pos3.Y, p);

    /// <summary>Draws a triangle outline as three lines.</summary>
    /// <param name="x1">First vertex x coordinate.</param>
    /// <param name="y1">First vertex y coordinate.</param>
    /// <param name="x2">Second vertex x coordinate.</param>
    /// <param name="y2">Second vertex y coordinate.</param>
    /// <param name="x3">Third vertex x coordinate.</param>
    /// <param name="y3">Third vertex y coordinate.</param>
    /// <param name="p">The outline colour.</param>
    /// <seealso cref="FillTriangle(int, int, int, int, int, int, Pixel)"/>
    public void DrawTriangle(int x1, int y1, int x2, int y2, int x3, int y3, Pixel p)
    {
        DrawLine(x1, y1, x2, y2, p);
        DrawLine(x2, y2, x3, y3, p);
        DrawLine(x3, y3, x1, y1, p);
    }

    /// <summary>Vector2d overload of <see cref="FillTriangle(int, int, int, int, int, int, Pixel)"/>.</summary>
    /// <param name="pos1">First vertex.</param>
    /// <param name="pos2">Second vertex.</param>
    /// <param name="pos3">Third vertex.</param>
    /// <param name="p">The fill colour.</param>
    /// <seealso cref="DrawTriangle(Vector2d{int}, Vector2d{int}, Vector2d{int}, Pixel)"/>
    public void FillTriangle(Vector2d<int> pos1, Vector2d<int> pos2, Vector2d<int> pos3, Pixel p)
        => FillTriangle(pos1.X, pos1.Y, pos2.X, pos2.Y, pos3.X, pos3.Y, p);

    /// <summary>Fills a solid triangle via olc's scanline algorithm (triangles.c), with the goto structure preserved verbatim.</summary>
    /// <param name="x1">First vertex x coordinate.</param>
    /// <param name="y1">First vertex y coordinate.</param>
    /// <param name="x2">Second vertex x coordinate.</param>
    /// <param name="y2">Second vertex y coordinate.</param>
    /// <param name="x3">Third vertex x coordinate.</param>
    /// <param name="y3">Third vertex y coordinate.</param>
    /// <param name="p">The fill colour.</param>
    /// <remarks>A faithful, goto-for-goto port of olc's triangles.c scanline fill: vertices are sorted by y and the two halves are rasterised as horizontal spans.</remarks>
    /// <seealso cref="DrawTriangle(int, int, int, int, int, int, Pixel)"/>
    public void FillTriangle(int x1, int y1, int x2, int y2, int x3, int y3, Pixel p)
    {
        void Scan(int sx, int ex, int ny) { for (var i = sx; i <= ex; i++) Draw(i, ny, p); }

        int t1x, t2x, y, minx = 0, maxx = 0, t1xp = 0, t2xp = 0;
        bool changed1 = false, changed2 = false;
        int signx1, signx2, dx1, dy1, dx2, dy2, e1 = 0, e2;

        // Sort vertices by y
        if (y1 > y2) { (y1, y2) = (y2, y1); (x1, x2) = (x2, x1); }
        if (y1 > y3) { (y1, y3) = (y3, y1); (x1, x3) = (x3, x1); }
        if (y2 > y3) { (y2, y3) = (y3, y2); (x2, x3) = (x3, x2); }

        t1x = t2x = x1; y = y1;
        dx1 = x2 - x1; if (dx1 < 0) { dx1 = -dx1; signx1 = -1; } else signx1 = 1;
        dy1 = y2 - y1;
        dx2 = x3 - x1; if (dx2 < 0) { dx2 = -dx2; signx2 = -1; } else signx2 = 1;
        dy2 = y3 - y1;

        if (dy1 > dx1) { (dx1, dy1) = (dy1, dx1); changed1 = true; }
        if (dy2 > dx2) { (dy2, dx2) = (dx2, dy2); changed2 = true; }

        e2 = dx2 >> 1;
        if (y1 == y2) goto next;
        e1 = dx1 >> 1;

        for (var i = 0; i < dx1;)
        {
            t1xp = 0; t2xp = 0;
            if (t1x < t2x) { minx = t1x; maxx = t2x; } else { minx = t2x; maxx = t1x; }
            while (i < dx1)
            {
                i++;
                e1 += dy1;
                while (e1 >= dx1)
                {
                    e1 -= dx1;
                    if (changed1) t1xp = signx1; else goto next1;
                }
                if (changed1) break;
                t1x += signx1;
            }
            next1:
            while (true)
            {
                e2 += dy2;
                while (e2 >= dx2)
                {
                    e2 -= dx2;
                    if (changed2) t2xp = signx2; else goto next2;
                }
                if (changed2) break;
                t2x += signx2;
            }
            next2:
            if (minx > t1x) minx = t1x;
            if (minx > t2x) minx = t2x;
            if (maxx < t1x) maxx = t1x;
            if (maxx < t2x) maxx = t2x;
            Scan(minx, maxx, y);
            if (!changed1) t1x += signx1;
            t1x += t1xp;
            if (!changed2) t2x += signx2;
            t2x += t2xp;
            y += 1;
            if (y == y2) break;
        }
        next:
        // Second half
        dx1 = x3 - x2; if (dx1 < 0) { dx1 = -dx1; signx1 = -1; } else signx1 = 1;
        dy1 = y3 - y2;
        t1x = x2;
        if (dy1 > dx1) { (dy1, dx1) = (dx1, dy1); changed1 = true; } else changed1 = false;
        e1 = dx1 >> 1;

        for (var i = 0; i <= dx1; i++)
        {
            t1xp = 0; t2xp = 0;
            if (t1x < t2x) { minx = t1x; maxx = t2x; } else { minx = t2x; maxx = t1x; }
            while (i < dx1)
            {
                e1 += dy1;
                while (e1 >= dx1)
                {
                    e1 -= dx1;
                    if (changed1) { t1xp = signx1; break; }
                    goto next3;
                }
                if (changed1) break;
                t1x += signx1;
                if (i < dx1) i++;
            }
            next3:
            while (t2x != x3)
            {
                e2 += dy2;
                while (e2 >= dx2)
                {
                    e2 -= dx2;
                    if (changed2) t2xp = signx2; else goto next4;
                }
                if (changed2) break;
                t2x += signx2;
            }
            next4:
            if (minx > t1x) minx = t1x;
            if (minx > t2x) minx = t2x;
            if (maxx < t1x) maxx = t1x;
            if (maxx < t2x) maxx = t2x;
            Scan(minx, maxx, y);
            if (!changed1) t1x += signx1;
            t1x += t1xp;
            if (!changed2) t2x += signx2;
            t2x += t2xp;
            y += 1;
            if (y > y3) return;
        }
    }

    /// <summary>Vector2d overload of <see cref="DrawSprite(int, int, Sprite, int, SpriteMirrorMode)"/>.</summary>
    /// <param name="pos">Top-left destination position.</param>
    /// <param name="sprite">The sprite to blit; a <c>null</c> sprite is a no-op.</param>
    /// <param name="scale">Integer scale factor (<c>1</c> = no scaling).</param>
    /// <param name="flip">Optional horizontal/vertical mirroring.</param>
    public void DrawSprite(Vector2d<int> pos, Sprite sprite, int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
        => DrawSprite(pos.X, pos.Y, sprite, scale, flip);

    /// <summary>Blits a sprite at the given position with integer scale and optional mirroring (fast row-copy path for 1:1 unflipped Normal-mode blits).</summary>
    /// <param name="x">Top-left destination x coordinate.</param>
    /// <param name="y">Top-left destination y coordinate.</param>
    /// <param name="sprite">The sprite to blit; a <c>null</c> sprite is a no-op.</param>
    /// <param name="scale">Integer scale factor (<c>1</c> = no scaling).</param>
    /// <param name="flip">Optional horizontal/vertical mirroring.</param>
    /// <remarks>At scale <c>1</c>, unflipped, in <see cref="PixelDisplayMode.Normal"/> mode the blit copies whole clipped rows via a vectorised <c>Span.CopyTo</c> rather than plotting each pixel through <see cref="Draw(int, int, Pixel)"/>.</remarks>
    public void DrawSprite(int x, int y, Sprite sprite, int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        if (sprite == null) return;

        // Fast path: a 1:1, unflipped, overwrite (Normal-mode) blit copies whole rows via Span.CopyTo
        // (a vectorised memmove) instead of plotting each pixel through Draw().
        var blitTarget = GetDrawTarget();
        if (blitTarget != null && scale == 1 && flip == SpriteMirrorMode.None && _pixelMode == PixelDisplayMode.Normal)
        {
            int dw = blitTarget.Width, dh = blitTarget.Height;
            var dst = CollectionsMarshal.AsSpan(blitTarget.PixelData);
            var src = CollectionsMarshal.AsSpan(sprite.PixelData);
            var cx0 = Math.Max(0, x);                       // clip the destination x range...
            var cx1 = Math.Min(dw, x + sprite.Width);
            var len = cx1 - cx0;
            if (len > 0)
                for (var j = 0; j < sprite.Height; j++)
                {
                    var dy = y + j;
                    if (dy < 0 || dy >= dh) continue;       // ...and the row
                    src.Slice(j * sprite.Width + (cx0 - x), len).CopyTo(dst.Slice(dy * dw + cx0, len));
                }
            return;
        }

        // Fast path: a 1:1, unflipped blit in Alpha or Mask mode blends whole clipped rows via a span loop
        // (BlendRowOver / MaskRowOver) instead of plotting each pixel through Draw() — same math, bit-exact,
        // but without the per-pixel virtual call / pixel-mode switch / List-indexer overhead. (Measured
        // ~4x. SIMD was evaluated for the per-pixel-alpha blend and was slower than this scalar span — the
        // src+dst widen and per-pixel alpha-broadcast cost more than the arithmetic saved; see BlitBenchmarks.)
        if (blitTarget != null && scale == 1 && flip == SpriteMirrorMode.None &&
            (_pixelMode == PixelDisplayMode.Alpha || _pixelMode == PixelDisplayMode.Mask))
        {
            int dw = blitTarget.Width, dh = blitTarget.Height;
            var dst = CollectionsMarshal.AsSpan(blitTarget.PixelData);
            var src = CollectionsMarshal.AsSpan(sprite.PixelData);
            var cx0 = Math.Max(0, x);
            var cx1 = Math.Min(dw, x + sprite.Width);
            var len = cx1 - cx0;
            if (len > 0)
                for (var j = 0; j < sprite.Height; j++)
                {
                    var dy = y + j;
                    if (dy < 0 || dy >= dh) continue;
                    var srcRow = src.Slice(j * sprite.Width + (cx0 - x), len);
                    var dstRow = dst.Slice(dy * dw + cx0, len);
                    if (_pixelMode == PixelDisplayMode.Alpha) BlendRowOver(dstRow, srcRow, _blendFactor);
                    else MaskRowOver(dstRow, srcRow);
                }
            return;
        }

        int fxs = 0, fxm = 1, fx;
        int fys = 0, fym = 1, fy;
        if ((flip & SpriteMirrorMode.Horizontal) != SpriteMirrorMode.None) { fxs = sprite.Width - 1; fxm = -1; }
        if ((flip & SpriteMirrorMode.Vertical) != SpriteMirrorMode.None) { fys = sprite.Height - 1; fym = -1; }

        if (scale > 1)
        {
            fx = fxs;
            for (var i = 0; i < sprite.Width; i++, fx += fxm)
            {
                fy = fys;
                for (var j = 0; j < sprite.Height; j++, fy += fym)
                    for (var isx = 0; isx < scale; isx++)
                        for (var js = 0; js < scale; js++)
                            Draw(x + i * scale + isx, y + j * scale + js, sprite.GetPixel(fx, fy));
            }
        }
        else
        {
            fx = fxs;
            for (var i = 0; i < sprite.Width; i++, fx += fxm)
            {
                fy = fys;
                for (var j = 0; j < sprite.Height; j++, fy += fym)
                    Draw(x + i, y + j, sprite.GetPixel(fx, fy));
            }
        }
    }

    /// <summary>Vector2d overload of <see cref="DrawPartialSprite(int, int, Sprite, int, int, int, int, int, SpriteMirrorMode)"/>.</summary>
    /// <param name="pos">Top-left destination position.</param>
    /// <param name="sprite">The source sprite; a <c>null</c> sprite is a no-op.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle within the sprite.</param>
    /// <param name="size">Width and height of the source sub-rectangle.</param>
    /// <param name="scale">Integer scale factor (<c>1</c> = no scaling).</param>
    /// <param name="flip">Optional horizontal/vertical mirroring.</param>
    public void DrawPartialSprite(Vector2d<int> pos, Sprite sprite, Vector2d<int> sourcePos, Vector2d<int> size,
        int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
        => DrawPartialSprite(pos.X, pos.Y, sprite, sourcePos.X, sourcePos.Y, size.X, size.Y, scale, flip);

    /// <summary>Blits a sub-rectangle of a sprite at the given position with integer scale and optional mirroring.</summary>
    /// <param name="x">Top-left destination x coordinate.</param>
    /// <param name="y">Top-left destination y coordinate.</param>
    /// <param name="sprite">The source sprite; a <c>null</c> sprite is a no-op.</param>
    /// <param name="ox">Source sub-rectangle x offset within the sprite.</param>
    /// <param name="oy">Source sub-rectangle y offset within the sprite.</param>
    /// <param name="w">Source sub-rectangle width.</param>
    /// <param name="h">Source sub-rectangle height.</param>
    /// <param name="scale">Integer scale factor (<c>1</c> = no scaling).</param>
    /// <param name="flip">Optional horizontal/vertical mirroring.</param>
    public void DrawPartialSprite(int x, int y, Sprite sprite, int ox, int oy, int w, int h,
        int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        if (sprite == null) return;

        int fxs = 0, fxm = 1, fx;
        int fys = 0, fym = 1, fy;
        if ((flip & SpriteMirrorMode.Horizontal) != SpriteMirrorMode.None) { fxs = w - 1; fxm = -1; }
        if ((flip & SpriteMirrorMode.Vertical) != SpriteMirrorMode.None) { fys = h - 1; fym = -1; }

        if (scale > 1)
        {
            fx = fxs;
            for (var i = 0; i < w; i++, fx += fxm)
            {
                fy = fys;
                for (var j = 0; j < h; j++, fy += fym)
                    for (var isx = 0; isx < scale; isx++)
                        for (var js = 0; js < scale; js++)
                            Draw(x + i * scale + isx, y + j * scale + js, sprite.GetPixel(fx + ox, fy + oy));
            }
        }
        else
        {
            fx = fxs;
            for (var i = 0; i < w; i++, fx += fxm)
            {
                fy = fys;
                for (var j = 0; j < h; j++, fy += fym)
                    Draw(x + i, y + j, sprite.GetPixel(fx + ox, fy + oy));
            }
        }
    }

    // O-------------------------------------------------------------------O
    // | HW3D - lightweight 3D (depth-tested GPU tasks)                     |
    // O-------------------------------------------------------------------O

    /// <summary>Sets the 3D projection matrix used by subsequent HW3D draws.</summary>
    /// <param name="matrix">A 16-element column-major 4x4 projection matrix.</param>
    public void HW3D_Projection(float[] matrix) => _renderer.Set3DProjection(matrix);
    /// <summary>Enables or disables depth testing for HW3D draws.</summary>
    /// <param name="enable"><c>true</c> to enable depth testing for subsequently queued HW3D tasks.</param>
    public void HW3D_EnableDepthTest(bool enable = true) => _hw3dDepthTest = enable;
    /// <summary>Sets the face-culling mode for HW3D draws.</summary>
    /// <param name="mode">The face-culling mode applied to subsequently queued HW3D tasks.</param>
    public void HW3D_SetCullMode(CullMode mode) => _hw3dCullMode = mode;

    /// <summary>Queues a 3D mesh as a depth/cull-enabled GPU task; each vertex is position (w forced to 1), UV, and colour.</summary>
    /// <param name="matModelView">A 16-element column-major model-view matrix; copied into the task's owned matrix (the caller's array is not aliased).</param>
    /// <param name="decal">Texture the mesh is sampled from.</param>
    /// <param name="layout">Vertex topology for the submitted geometry.</param>
    /// <param name="pos">Per-vertex positions, each a 3-element <c>x, y, z</c> array.</param>
    /// <param name="uv">Per-vertex texture coordinates, each a 2-element <c>u, v</c> array.</param>
    /// <param name="col">Per-vertex colours.</param>
    /// <param name="tint">Optional tint applied to the whole task; defaults to white when <c>null</c>.</param>
    /// <remarks>The task is queued into the current layer and flushed to the renderer by the core loop, honouring the current depth-test and cull-mode state.</remarks>
    public void HW3D_DrawObject(float[] matModelView, Decal decal, DecalStructure layout,
        IReadOnlyList<float[]> pos, IReadOnlyList<float[]> uv, IReadOnlyList<Pixel> col, Pixel? tint = null)
    {
        var task = NextGpuTask();
        task.Decal = decal;
        task.Mode = _decalMode;
        task.Structure = layout;
        task.Depth = _hw3dDepthTest;
        task.Cull = _hw3dCullMode;
        task.Tint = tint ?? Pixel.WHITE;
        Array.Copy(matModelView, task.Mvp, 16); // copy into the task's OWNED matrix, don't alias the caller's
        for (var i = 0; i < pos.Count; i++)
            task.Vb.Add(new Vertex(pos[i][0], pos[i][1], pos[i][2], 1.0f, uv[i][0], uv[i][1], col[i].N));
    }

    /// <summary>Queues a 3D wireframe line segment as a GPU task.</summary>
    /// <param name="matModelView">A 16-element column-major model-view matrix; copied into the task's owned matrix (the caller's array is not aliased).</param>
    /// <param name="pos1">Start point, a 3-element <c>x, y, z</c> array.</param>
    /// <param name="pos2">End point, a 3-element <c>x, y, z</c> array.</param>
    /// <param name="col">Optional line colour; defaults to white when <c>null</c>.</param>
    /// <remarks>The task is queued into the current layer and flushed by the core loop, honouring the current depth-test and cull-mode state.</remarks>
    public void HW3D_DrawLine(float[] matModelView, float[] pos1, float[] pos2, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var task = NextGpuTask();
        task.Decal = null!; // no texture: wireframe line has no decal
        task.Mode = DecalMode.Wireframe;
        task.Structure = DecalStructure.Line;
        task.Depth = _hw3dDepthTest;
        task.Cull = _hw3dCullMode;
        task.Tint = Pixel.WHITE;
        Array.Copy(matModelView, task.Mvp, 16);
        task.Vb.Add(new Vertex(pos1[0], pos1[1], pos1[2], 1.0f, 0.0f, 0.0f, c.N));
        task.Vb.Add(new Vertex(pos2[0], pos2[1], pos2[2], 1.0f, 0.0f, 0.0f, c.N));
    }

    /// <summary>Vertex-index pairs for the 12 edges of a box, used by <see cref="HW3D_DrawLineBox"/>.</summary>
    private static readonly int[] BoxEdges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };

    /// <summary>Queues a 3D axis-aligned wireframe box (12 edges) as a GPU task.</summary>
    /// <param name="matModelView">A 16-element column-major model-view matrix; copied into the task's owned matrix (the caller's array is not aliased).</param>
    /// <param name="pos">Box origin corner, a 3-element <c>x, y, z</c> array.</param>
    /// <param name="size">Box extent, a 3-element <c>x, y, z</c> array added to <paramref name="pos"/>.</param>
    /// <param name="col">Optional edge colour; defaults to white when <c>null</c>.</param>
    /// <remarks>Emits the 12 box edges as line segments via the same task path as <see cref="HW3D_DrawLine"/>; queued into the current layer and flushed by the core loop.</remarks>
    /// <seealso cref="HW3D_DrawLine"/>
    public void HW3D_DrawLineBox(float[] matModelView, float[] pos, float[] size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var task = NextGpuTask();
        task.Decal = null!; // no texture: wireframe box has no decal
        task.Mode = _decalMode;
        task.Structure = DecalStructure.Line;
        task.Depth = _hw3dDepthTest;
        task.Cull = _hw3dCullMode;
        task.Tint = Pixel.WHITE;
        Array.Copy(matModelView, task.Mvp, 16);

        float ox = pos[0], oy = pos[1], oz = pos[2], sx = size[0], sy = size[1], sz = size[2];
        var p = new[]
        {
            new[] { ox, oy, oz }, new[] { ox + sx, oy, oz }, new[] { ox + sx, oy + sy, oz }, new[] { ox, oy + sy, oz },
            new[] { ox, oy, oz + sz }, new[] { ox + sx, oy, oz + sz }, new[] { ox + sx, oy + sy, oz + sz }, new[] { ox, oy + sy, oz + sz }
        };
        // 12 edges of the box, each as a line-segment vertex pair.
        foreach (var idx in BoxEdges)
            task.Vb.Add(new Vertex(p[idx][0], p[idx][1], p[idx][2], 1.0f, 0.0f, 0.0f, c.N));
    }

    // O-------------------------------------------------------------------O
    // | Textured triangles / polygons + sprite & decal patches (software) |
    // O-------------------------------------------------------------------O

    /// <summary>Software-rasterises a gouraud-shaded, optionally textured triangle, interpolating colour and UV down each scanline (port of triangles.c).</summary>
    /// <param name="points">The three triangle vertex positions, in pixels.</param>
    /// <param name="tex">The three per-vertex texture coordinates.</param>
    /// <param name="colour">The three per-vertex colours (gouraud-interpolated).</param>
    /// <param name="sprTex">Texture sampled per pixel; pass <c>null</c> for an untextured gouraud-only fill.</param>
    /// <remarks>Faithful port of olc's triangles.c: vertices are y-sorted, the triangle is split into two halves, and colour and UV are linearly interpolated along each scanline. When <paramref name="sprTex"/> is non-null the sampled texel multiplies the interpolated colour.</remarks>
    public void FillTexturedTriangle(IReadOnlyList<Vector2d<float>> points, IReadOnlyList<Vector2d<float>> tex,
        IReadOnlyList<Pixel> colour, Sprite sprTex)
    {
        var p1 = points[0].As<int>();
        var p2 = points[1].As<int>();
        var p3 = points[2].As<int>();
        var vTex = _texTri3; vTex[0] = tex[0]; vTex[1] = tex[1]; vTex[2] = tex[2];
        var vCol = _colTri3; vCol[0] = colour[0]; vCol[1] = colour[1]; vCol[2] = colour[2];

        // Sort vertices (with their tex/colour) by ascending y.
        if (p2.Y < p1.Y) { (p1, p2) = (p2, p1); (vTex[0], vTex[1]) = (vTex[1], vTex[0]); (vCol[0], vCol[1]) = (vCol[1], vCol[0]); }
        if (p3.Y < p1.Y) { (p1, p3) = (p3, p1); (vTex[0], vTex[2]) = (vTex[2], vTex[0]); (vCol[0], vCol[2]) = (vCol[2], vCol[0]); }
        if (p3.Y < p2.Y) { (p2, p3) = (p3, p2); (vTex[1], vTex[2]) = (vTex[2], vTex[1]); (vCol[1], vCol[2]) = (vCol[2], vCol[1]); }

        var dPos1 = p2 - p1;
        float dt1x = vTex[1].X - vTex[0].X, dt1y = vTex[1].Y - vTex[0].Y;
        int dcr1 = vCol[1].Red - vCol[0].Red, dcg1 = vCol[1].Green - vCol[0].Green, dcb1 = vCol[1].Blue - vCol[0].Blue, dca1 = vCol[1].Alpha - vCol[0].Alpha;

        var dPos2 = p3 - p1;
        float dt2x = vTex[2].X - vTex[0].X, dt2y = vTex[2].Y - vTex[0].Y;
        int dcr2 = vCol[2].Red - vCol[0].Red, dcg2 = vCol[2].Green - vCol[0].Green, dcb2 = vCol[2].Blue - vCol[0].Blue, dca2 = vCol[2].Alpha - vCol[0].Alpha;

        float daxStep = 0, dbxStep = 0;
        float dcr1Step = 0, dcg1Step = 0, dcb1Step = 0, dca1Step = 0;
        float dcr2Step = 0, dcg2Step = 0, dcb2Step = 0, dca2Step = 0;
        float t1sx = 0, t1sy = 0, t2sx = 0, t2sy = 0;

        if (dPos1.Y != 0)
        {
            var inv = 1.0f / Math.Abs(dPos1.Y);
            daxStep = dPos1.X * inv; t1sx = dt1x * inv; t1sy = dt1y * inv;
            dcr1Step = dcr1 * inv; dcg1Step = dcg1 * inv; dcb1Step = dcb1 * inv; dca1Step = dca1 * inv;
        }
        if (dPos2.Y != 0)
        {
            var inv = 1.0f / Math.Abs(dPos2.Y);
            dbxStep = dPos2.X * inv; t2sx = dt2x * inv; t2sy = dt2y * inv;
            dcr2Step = dcr2 * inv; dcg2Step = dcg2 * inv; dcb2Step = dcb2 * inv; dca2Step = dca2 * inv;
        }

        for (var pass = 0; pass < 2; pass++)
        {
            Vector2d<int> vStart, vEnd;
            int vStartIdx;
            if (pass == 0)
            {
                vStart = p1; vEnd = p2; vStartIdx = 0;
            }
            else
            {
                // Second sub-triangle: recompute the "1" edge from p2->p3.
                dPos1 = p3 - p2;
                dt1x = vTex[2].X - vTex[1].X; dt1y = vTex[2].Y - vTex[1].Y;
                dcr1 = vCol[2].Red - vCol[1].Red; dcg1 = vCol[2].Green - vCol[1].Green; dcb1 = vCol[2].Blue - vCol[1].Blue; dca1 = vCol[2].Alpha - vCol[1].Alpha;
                dcr1Step = 0; dcg1Step = 0; dcb1Step = 0; dca1Step = 0;

                if (dPos2.Y != 0) dbxStep = dPos2.X / (float)Math.Abs(dPos2.Y);
                if (dPos1.Y != 0)
                {
                    var inv = 1.0f / Math.Abs(dPos1.Y);
                    daxStep = dPos1.X * inv; t1sx = dt1x * inv; t1sy = dt1y * inv;
                    dcr1Step = dcr1 * inv; dcg1Step = dcg1 * inv; dcb1Step = dcb1 * inv; dca1Step = dca1 * inv;
                }
                vStart = p2; vEnd = p3; vStartIdx = 1;
            }

            if (dPos1.Y == 0) continue;

            for (var i = vStart.Y; i <= vEnd.Y; i++)
            {
                var ax = (int)(vStart.X + (i - vStart.Y) * daxStep);
                var bx = (int)(p1.X + (i - p1.Y) * dbxStep);

                float dsi = i - vStart.Y, dpi = i - p1.Y;
                float tsx = vTex[vStartIdx].X + dsi * t1sx, tsy = vTex[vStartIdx].Y + dsi * t1sy;
                float tex_x = vTex[0].X + dpi * t2sx, tey = vTex[0].Y + dpi * t2sy;

                var colS = new Pixel(
                    (byte)(vCol[vStartIdx].Red + (byte)(dsi * dcr1Step)),
                    (byte)(vCol[vStartIdx].Green + (byte)(dsi * dcg1Step)),
                    (byte)(vCol[vStartIdx].Blue + (byte)(dsi * dcb1Step)),
                    (byte)(vCol[vStartIdx].Alpha + (byte)(dsi * dca1Step)));
                var colE = new Pixel(
                    (byte)(vCol[0].Red + (byte)(dpi * dcr2Step)),
                    (byte)(vCol[0].Green + (byte)(dpi * dcg2Step)),
                    (byte)(vCol[0].Blue + (byte)(dpi * dcb2Step)),
                    (byte)(vCol[0].Alpha + (byte)(dpi * dca2Step)));

                if (ax > bx) { (ax, bx) = (bx, ax); (tsx, tex_x) = (tex_x, tsx); (tsy, tey) = (tey, tsy); (colS, colE) = (colE, colS); }

                var tstep = 1.0f / (bx - ax);
                var t = 0.0f;
                for (var j = ax; j < bx; j++)
                {
                    var pixel = Pixel.LinearInterpolation(colS, colE, t);
                    if (sprTex != null)
                        pixel *= sprTex.SamplePixel(tsx + (tex_x - tsx) * t, tsy + (tey - tsy) * t);
                    Draw(j, i, pixel);
                    t += tstep;
                }
            }
        }
    }

    /// <summary>Software-rasterises a textured polygon by decomposing it into triangles per the List/Strip/Fan topology.</summary>
    /// <param name="points">Polygon vertex positions, in pixels.</param>
    /// <param name="tex">Per-vertex texture coordinates (parallel to <paramref name="points"/>).</param>
    /// <param name="colour">Per-vertex colours (parallel to <paramref name="points"/>).</param>
    /// <param name="sprTex">Texture sampled per pixel; pass <c>null</c> for an untextured gouraud-only fill.</param>
    /// <param name="structure">Vertex topology used to form triangles:
    /// <list type="bullet">
    /// <item><description><c>List</c> — every three vertices form one triangle.</description></item>
    /// <item><description><c>Strip</c> — each vertex after the first two forms a triangle with the previous two.</description></item>
    /// <item><description><c>Fan</c> — each vertex after the first two forms a triangle with the first vertex and the previous one.</description></item>
    /// <item><description><c>Line</c> — ignored (the method returns immediately).</description></item>
    /// </list>
    /// </param>
    /// <remarks>Each resulting triangle is forwarded to <see cref="FillTexturedTriangle"/>. Returns without drawing when fewer than three points/coords/colours are supplied.</remarks>
    /// <seealso cref="FillTexturedTriangle"/>
    public void FillTexturedPolygon(IReadOnlyList<Vector2d<float>> points, IReadOnlyList<Vector2d<float>> tex,
        IReadOnlyList<Pixel> colour, Sprite sprTex, DecalStructure structure = DecalStructure.List)
    {
        if (structure == DecalStructure.Line) return; // meaningless
        if (points.Count < 3 || tex.Count < 3 || colour.Count < 3) return;

        switch (structure)
        {
            case DecalStructure.List:
                for (var tri = 0; tri < points.Count / 3; tri++)
                    FillTexturedTriangleFrom(points, tex, colour, tri * 3, tri * 3 + 1, tri * 3 + 2, sprTex);
                break;
            case DecalStructure.Strip:
                for (var tri = 2; tri < points.Count; tri++)
                    FillTexturedTriangleFrom(points, tex, colour, tri - 2, tri - 1, tri, sprTex);
                break;
            case DecalStructure.Fan:
                for (var tri = 2; tri < points.Count; tri++)
                    FillTexturedTriangleFrom(points, tex, colour, 0, tri - 1, tri, sprTex);
                break;
        }
    }

    /// <summary>Gathers three indexed vertices into reusable scratch and rasterises them (no per-triangle allocation).</summary>
    /// <param name="points">Source polygon vertex positions.</param>
    /// <param name="tex">Source per-vertex texture coordinates.</param>
    /// <param name="colour">Source per-vertex colours.</param>
    /// <param name="a">Index of the first triangle vertex.</param>
    /// <param name="b">Index of the second triangle vertex.</param>
    /// <param name="c">Index of the third triangle vertex.</param>
    /// <param name="sprTex">Texture sampled per pixel; may be <c>null</c> for an untextured fill.</param>
    /// <seealso cref="FillTexturedTriangle"/>
    private void FillTexturedTriangleFrom(IReadOnlyList<Vector2d<float>> points, IReadOnlyList<Vector2d<float>> tex,
        IReadOnlyList<Pixel> colour, int a, int b, int c, Sprite sprTex)
    {
        _polyPos3[0] = points[a]; _polyPos3[1] = points[b]; _polyPos3[2] = points[c];
        _polyTex3[0] = tex[a]; _polyTex3[1] = tex[b]; _polyTex3[2] = tex[c];
        _polyCol3[0] = colour[a]; _polyCol3[1] = colour[b]; _polyCol3[2] = colour[c];
        FillTexturedTriangle(_polyPos3, _polyTex3, _polyCol3, sprTex);
    }

    /// <summary>Fills the 4-point scratch with a quad (CounterClockWise from bottom-left) for sprite/decal patch drawing.</summary>
    /// <param name="pos">Top-left corner of the quad, in pixels.</param>
    /// <param name="s">Quad size (width and height), in pixels.</param>
    private void FillPatchVerts(Vector2d<float> pos, Vector2d<float> s)
    {
        _scratchPts4[0] = new Vector2d<float>(pos.X, pos.Y + s.Y);
        _scratchPts4[1] = pos;
        _scratchPts4[2] = new Vector2d<float>(pos.X + s.X, pos.Y);
        _scratchPts4[3] = new Vector2d<float>(pos.X + s.X, pos.Y + s.Y);
    }

    /// <summary>Draws a sprite patch (software) as a textured quad fan at the given position and scale.</summary>
    /// <param name="pos">Top-left destination position, in pixels.</param>
    /// <param name="patch">The sprite patch supplying the texture and its quad texture coordinates.</param>
    /// <param name="scale">Optional per-axis scale; defaults to <c>1</c> on both axes when <c>null</c>.</param>
    /// <remarks>Builds a quad and forwards it to <see cref="FillTexturedPolygon"/> with fan topology.</remarks>
    /// <seealso cref="FillTexturedPolygon"/>
    public void DrawSprite(Vector2d<float> pos, SpritePatch patch, Vector2d<float>? scale = null)
    {
        FillPatchVerts(pos, scale ?? new Vector2d<float>(1, 1));
        FillTexturedPolygon(_scratchPts4, patch.Coords, WhiteQuad4, patch.Sprite, DecalStructure.Fan);
    }

    /// <summary>Draws a decal patch (GPU) as a textured polygon quad at the given position and scale.</summary>
    /// <param name="pos">Top-left destination position, in pixels.</param>
    /// <param name="patch">The decal patch supplying the decal and its quad texture coordinates.</param>
    /// <param name="scale">Optional per-axis scale; defaults to <c>1</c> on both axes when <c>null</c>.</param>
    /// <remarks>Builds a quad and forwards it to <see cref="DrawPolygonDecal(Decal, IReadOnlyList{Vector2d{float}}, IReadOnlyList{Vector2d{float}}, IReadOnlyList{Pixel})"/>.</remarks>
    public void DrawDecal(Vector2d<float> pos, DecalPatch patch, Vector2d<float>? scale = null)
    {
        FillPatchVerts(pos, scale ?? new Vector2d<float>(1, 1));
        DrawPolygonDecal(patch.Decal, _scratchPts4, patch.Coordinates, WhiteQuad4);
    }

    // O-------------------------------------------------------------------O
    // | Text (embedded 8x8 font)                                          |
    // O-------------------------------------------------------------------O

    /// <summary>The embedded font's sprite sheet.</summary>
    /// <returns>The 128x48 sprite holding the embedded 8x8 font glyphs.</returns>
    public Sprite GetFontSprite() => _fontRenderable.Sprite;

    /// <summary>Returns the pixel size of a monospaced string (8 pixels per cell, tabs expanded).</summary>
    /// <param name="s">The string to measure; <c>'\n'</c> starts a new line and <c>'\t'</c> advances by the tab width.</param>
    /// <returns>The bounding size in pixels (width and height), at 8 pixels per glyph cell.</returns>
    /// <seealso cref="DrawString(int, int, string, Pixel, int)"/>
    public Vector2d<int> GetTextSize(string s)
    {
        int sizeX = 0, sizeY = 1, posX = 0, posY = 1;
        foreach (var c in s)
        {
            if (c == '\n') { posY++; posX = 0; }
            else if (c == '\t') { posX += _tabSizeInSpaces; }
            else posX++;
            sizeX = Math.Max(sizeX, posX);
            sizeY = Math.Max(sizeY, posY);
        }
        return new Vector2d<int>(sizeX * 8, sizeY * 8);
    }

    /// <summary>Returns the pixel size of a proportionally-spaced string.</summary>
    /// <param name="s">The string to measure; <c>'\n'</c> starts a new line and <c>'\t'</c> advances by the tab width.</param>
    /// <returns>The bounding size in pixels (width and height); width uses per-glyph proportional spacing, height is 8 pixels per line.</returns>
    /// <seealso cref="DrawStringProp(int, int, string, Pixel, int)"/>
    public Vector2d<int> GetTextSizeProp(string s)
    {
        int sizeX = 0, sizeY = 1, posX = 0, posY = 1;
        foreach (var c in s)
        {
            if (c == '\n') { posY++; posX = 0; }
            else if (c == '\t') { posX += _tabSizeInSpaces * 8; }
            else posX += _fontSpacing[c - 32].Y;
            sizeX = Math.Max(sizeX, posX);
            sizeY = Math.Max(sizeY, posY);
        }
        return new Vector2d<int>(sizeX, sizeY * 8);
    }

    /// <summary>Vector2d overload of <see cref="DrawString(int, int, string, Pixel, int)"/>.</summary>
    /// <param name="pos">Top-left destination position, in pixels.</param>
    /// <param name="text">The string to draw.</param>
    /// <param name="col">Text colour.</param>
    /// <param name="scale">Integer magnification factor.</param>
    /// <seealso cref="DrawString(int, int, string, Pixel, int)"/>
    public void DrawString(Vector2d<int> pos, string text, Pixel col, int scale = 1)
        => DrawString(pos.X, pos.Y, text, col, scale);

    /// <summary>Draws monospaced text from the embedded font (software), respecting transparency via temporary Mask/Alpha mode.</summary>
    /// <param name="x">Top-left destination x coordinate, in pixels.</param>
    /// <param name="y">Top-left destination y coordinate, in pixels.</param>
    /// <param name="text">The string to draw; <c>'\n'</c> starts a new line and <c>'\t'</c> advances by the tab width.</param>
    /// <param name="col">Text colour.</param>
    /// <param name="scale">Integer magnification factor.</param>
    /// <remarks>Temporarily switches the pixel mode to <c>Alpha</c> (when the colour is translucent) or <c>Mask</c> for transparency, then restores the previous mode; a <c>Custom</c> pixel mode is left untouched.</remarks>
    /// <seealso cref="GetTextSize"/>
    public void DrawString(int x, int y, string text, Pixel col, int scale = 1)
    {
        var sx = 0;
        var sy = 0;
        var m = _pixelMode;
        // Text respects transparency: use ALPHA for translucent colours, MASK otherwise.
        if (m != PixelDisplayMode.Custom)
            SetPixelMode(col.Alpha != 255 ? PixelDisplayMode.Alpha : PixelDisplayMode.Mask);

        foreach (var c in text)
        {
            if (c == '\n') { sx = 0; sy += 8 * scale; }
            else if (c == '\t') { sx += 8 * _tabSizeInSpaces * scale; }
            else
            {
                var ox = (c - 32) % 16;
                var oy = (c - 32) / 16;
                for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                    if (_fontRenderable.Sprite.GetPixel(i + ox * 8, j + oy * 8).Red > 0)
                    {
                        if (scale > 1)
                        {
                            for (var ix = 0; ix < scale; ix++)
                            for (var jx = 0; jx < scale; jx++)
                                Draw(x + sx + i * scale + ix, y + sy + j * scale + jx, col);
                        }
                        else
                        {
                            Draw(x + sx + i, y + sy + j, col);
                        }
                    }
                sx += 8 * scale;
            }
        }
        SetPixelMode(m);
    }

    /// <summary>Vector2d overload of <see cref="DrawStringProp(int, int, string, Pixel, int)"/>.</summary>
    /// <param name="pos">Top-left destination position, in pixels.</param>
    /// <param name="text">The string to draw.</param>
    /// <param name="col">Text colour.</param>
    /// <param name="scale">Integer magnification factor.</param>
    /// <seealso cref="DrawStringProp(int, int, string, Pixel, int)"/>
    public void DrawStringProp(Vector2d<int> pos, string text, Pixel col, int scale = 1)
        => DrawStringProp(pos.X, pos.Y, text, col, scale);

    /// <summary>Draws proportionally-spaced text from the embedded font (software), respecting transparency via temporary Mask/Alpha mode.</summary>
    /// <param name="x">Top-left destination x coordinate, in pixels.</param>
    /// <param name="y">Top-left destination y coordinate, in pixels.</param>
    /// <param name="text">The string to draw; <c>'\n'</c> starts a new line and <c>'\t'</c> advances by the tab width.</param>
    /// <param name="col">Text colour.</param>
    /// <param name="scale">Integer magnification factor.</param>
    /// <remarks>Uses per-glyph proportional spacing. Temporarily switches the pixel mode to <c>Alpha</c> (when the colour is translucent) or <c>Mask</c> for transparency, then restores the previous mode; a <c>Custom</c> pixel mode is left untouched.</remarks>
    /// <seealso cref="GetTextSizeProp"/>
    public void DrawStringProp(int x, int y, string text, Pixel col, int scale = 1)
    {
        var sx = 0;
        var sy = 0;
        var m = _pixelMode;
        if (m != PixelDisplayMode.Custom)
            SetPixelMode(col.Alpha != 255 ? PixelDisplayMode.Alpha : PixelDisplayMode.Mask);

        foreach (var c in text)
        {
            if (c == '\n') { sx = 0; sy += 8 * scale; }
            else if (c == '\t') { sx += 8 * _tabSizeInSpaces * scale; }
            else
            {
                var ox = (c - 32) % 16;
                var oy = (c - 32) / 16;
                var spacing = _fontSpacing[c - 32];
                for (var i = 0; i < spacing.Y; i++)
                for (var j = 0; j < 8; j++)
                    if (_fontRenderable.Sprite.GetPixel(i + ox * 8 + spacing.X, j + oy * 8).Red > 0)
                    {
                        if (scale > 1)
                        {
                            for (var ix = 0; ix < scale; ix++)
                            for (var jx = 0; jx < scale; jx++)
                                Draw(x + sx + i * scale + ix, y + sy + j * scale + jx, col);
                        }
                        else
                        {
                            Draw(x + sx + i, y + sy + j, col);
                        }
                    }
                sx += spacing.Y * scale;
            }
        }
        SetPixelMode(m);
    }

    /// <summary>Reconstructs olc's embedded 8x8 font into a 128x48 renderable from base64-ish run data and derives per-glyph proportional spacing; requires an active GL context.</summary>
    /// <remarks>Decodes the embedded base64-ish run data into glyph pixels and uploads the renderable's decal, so it must be called with an active GL context (it is invoked during engine preparation).</remarks>
    private void ConstructFontSheet()
    {
        const string data =
            "?Q`0001oOch0o01o@F40o0<AGD4090LAGD<090@A7ch0?00O7Q`0600>00000000" +
            "O000000nOT0063Qo4d8>?7a14Gno94AA4gno94AaOT0>o3`oO400o7QN00000400" +
            "Of80001oOg<7O7moBGT7O7lABET024@aBEd714AiOdl717a_=TH013Q>00000000" +
            "720D000V?V5oB3Q_HdUoE7a9@DdDE4A9@DmoE4A;Hg]oM4Aj8S4D84@`00000000" +
            "OaPT1000Oa`^13P1@AI[?g`1@A=[OdAoHgljA4Ao?WlBA7l1710007l100000000" +
            "ObM6000oOfMV?3QoBDD`O7a0BDDH@5A0BDD<@5A0BGeVO5ao@CQR?5Po00000000" +
            "Oc``000?Ogij70PO2D]??0Ph2DUM@7i`2DTg@7lh2GUj?0TO0C1870T?00000000" +
            "70<4001o?P<7?1QoHg43O;`h@GT0@:@LB@d0>:@hN@L0@?aoN@<0O7ao0000?000" +
            "OcH0001SOglLA7mg24TnK7ln24US>0PL24U140PnOgl0>7QgOcH0K71S0000A000" +
            "00H00000@Dm1S007@DUSg00?OdTnH7YhOfTL<7Yh@Cl0700?@Ah0300700000000" +
            "<008001QL00ZA41a@6HnI<1i@FHLM81M@@0LG81?O`0nC?Y7?`0ZA7Y300080000" +
            "O`082000Oh0827mo6>Hn?Wmo?6HnMb11MP08@C11H`08@FP0@@0004@000000000" +
            "00P00001Oab00003OcKP0006@6=PMgl<@440MglH@000000`@000001P00000000" +
            "Ob@8@@00Ob@8@Ga13R@8Mga172@8?PAo3R@827QoOb@820@0O`0007`0000007P0" +
            "O`000P08Od400g`<3V=P0G`673IP0`@3>1`00P@6O`P00g`<O`000GP800000000" +
            "?P9PL020O`<`N3R0@E4HC7b0@ET<ATB0@@l6C4B0O`H3N7b0?P01L3R000000020";

        _fontRenderable = new Renderable();
        _fontRenderable.Create(128, 48, false, true);

        int px = 0, py = 0;
        for (var b = 0; b < 1024; b += 4)
        {
            var sym1 = (uint)data[b + 0] - 48;
            var sym2 = (uint)data[b + 1] - 48;
            var sym3 = (uint)data[b + 2] - 48;
            var sym4 = (uint)data[b + 3] - 48;
            var r = (sym1 << 18) | (sym2 << 12) | (sym3 << 6) | sym4;

            for (var i = 0; i < 24; i++)
            {
                var k = (r & (1u << i)) != 0 ? (byte)255 : (byte)0;
                _fontRenderable.Sprite.SetPixel(px, py, new Pixel(k, k, k, k));
                if (++py == 48) { px++; py = 0; }
            }
        }

        _fontRenderable.Decal.Update();

        byte[] vSpacing =
        {
            0x03,0x25,0x16,0x08,0x07,0x08,0x08,0x04,0x15,0x15,0x08,0x07,0x15,0x07,0x24,0x08,
            0x08,0x17,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x24,0x15,0x06,0x07,0x16,0x17,
            0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x17,0x08,0x08,0x17,0x08,0x08,0x08,
            0x08,0x08,0x08,0x08,0x17,0x08,0x08,0x08,0x08,0x17,0x08,0x15,0x08,0x15,0x08,0x08,
            0x24,0x18,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x33,0x17,0x17,0x33,0x18,0x17,0x17,
            0x17,0x17,0x17,0x17,0x07,0x17,0x17,0x18,0x18,0x17,0x17,0x07,0x33,0x07,0x08,0x00,
        };
        foreach (var c in vSpacing)
            _fontSpacing.Add(new Vector2d<int>(c >> 4, c & 15));
    }

    // O-------------------------------------------------------------------O
    // | GPU decal drawing (queued per layer, flushed by CoreUpdate)       |
    // O-------------------------------------------------------------------O

    /// <summary>Sets the decal blend mode applied to subsequently submitted decals.</summary>
    /// <param name="mode">The blend mode stamped onto every decal queued after this call.</param>
    /// <seealso cref="SetDecalStructure"/>
    public void SetDecalMode(DecalMode mode) => _decalMode = mode;
    /// <summary>Sets the decal vertex topology applied to subsequently submitted decals.</summary>
    /// <param name="structure">The vertex topology stamped onto every decal queued after this call.</param>
    /// <seealso cref="SetDecalMode"/>
    public void SetDecalStructure(DecalStructure structure) => _decalStructure = structure;

    /// <summary>Transforms a pixel-space point into normalised device coordinates (-1..1) with y flipped (olc's convention).</summary>
    /// <param name="x">The pixel-space X coordinate.</param>
    /// <param name="y">The pixel-space Y coordinate.</param>
    /// <returns>The point in normalised device coordinates, X and Y in the range -1..1.</returns>
    /// <remarks>The Y axis is flipped (pixel-space grows downward, NDC grows upward) per olc's convention.</remarks>
    private Vector2d<float> ScreenTransform(float x, float y)
        => new Vector2d<float>(x * InvScreenSize.X * 2.0f - 1.0f, (y * InvScreenSize.Y * 2.0f - 1.0f) * -1.0f);

    /// <summary>Stamps the current decal mode/structure onto a rented instance already added to the target layer's pool.</summary>
    /// <param name="di">The rented decal instance to finalise; its <c>Mode</c> and <c>Structure</c> are set from the engine's current decal state.</param>
    private void SubmitDecal(DecalInstance di)
    {
        di.Mode = _decalMode;
        di.Structure = _decalStructure;
    }

    /// <summary>Rents a reusable DecalInstance from the current target layer's pool (recycling a cleared one or growing the pool).</summary>
    /// <returns>A ready-to-fill DecalInstance owned by the target layer's pool.</returns>
    /// <remarks>The layer's instance list doubles as a persistent object pool: an already-allocated instance is reset (its vertex lists cleared, capacity kept) and reused, or a new one is appended when the live count exceeds the pool. The core loop resets the live count each frame rather than clearing the list.</remarks>
    private DecalInstance NextDecalInstance()
    {
        var layer = _layers[_targetLayer];
        DecalInstance di;
        if (layer.DecalInstanceCount < layer.VecDecalInstance.Count)
        {
            di = layer.VecDecalInstance[layer.DecalInstanceCount];
            di.Reset();
        }
        else
        {
            di = new DecalInstance();
            layer.VecDecalInstance.Add(di);
        }
        layer.DecalInstanceCount++;
        return di;
    }

    /// <summary>Rents a reusable GPUTask from the current target layer's pool (recycling a reset one or growing the pool).</summary>
    /// <returns>A ready-to-fill GPUTask owned by the target layer's pool.</returns>
    /// <remarks>Mirrors <see cref="NextDecalInstance"/>: the layer's task list is a persistent pool whose entries are reset (vertex list cleared, identity MVP restored, capacity kept) and reused, or grown on demand.</remarks>
    private GPUTask NextGpuTask()
    {
        var layer = _layers[_targetLayer];
        GPUTask task;
        if (layer.GpuTaskCount < layer.VecGPUTasks.Count)
        {
            task = layer.VecGPUTasks[layer.GpuTaskCount];
            task.Reset();
        }
        else
        {
            task = new GPUTask();
            layer.VecGPUTasks.Add(task);
        }
        layer.GpuTaskCount++;
        return task;
    }

    /// <summary>Queues a full decal drawn at the given position with optional scale and tint.</summary>
    /// <param name="pos">Top-left destination position in pixel space.</param>
    /// <param name="decal">The decal (GPU texture) to draw.</param>
    /// <param name="scale">Per-axis scale; <c>null</c> means 1:1.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>The decal is appended to the current target layer's instance list and flushed to the renderer by the core loop. See <see cref="DrawPartialDecal(Vector2d{float}, Decal, Vector2d{float}, Vector2d{float}, Vector2d{float}?, Pixel?)"/> to draw only a sub-rectangle.</remarks>
    public void DrawDecal(Vector2d<float> pos, Decal decal, Vector2d<float>? scale = null, Pixel? tint = null)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var t = tint ?? Pixel.WHITE;
        var sp = ScreenTransform(pos.X, pos.Y);
        var dimX = sp.X + 2.0f * decal.Sprite.Width * InvScreenSize.X * s.X;
        var dimY = sp.Y - 2.0f * decal.Sprite.Height * InvScreenSize.Y * s.Y;

        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = 4;
        di.Pos.Add(new Vector2d<float>(sp.X, sp.Y));
        di.Pos.Add(new Vector2d<float>(sp.X, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, sp.Y));
        di.Uv.Add(new Vector2d<float>(0, 0));
        di.Uv.Add(new Vector2d<float>(0, 1));
        di.Uv.Add(new Vector2d<float>(1, 1));
        di.Uv.Add(new Vector2d<float>(1, 0));
        for (var i = 0; i < 4; i++) { di.W.Add(1); di.Tint.Add(t); }
        SubmitDecal(di);
    }

    /// <summary>Queues a sub-rectangle of a decal at the given position with optional scale and tint, quantising to texel centres.</summary>
    /// <param name="pos">Top-left destination position in pixel space.</param>
    /// <param name="decal">The decal (GPU texture) to sample.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle, in texels.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle, in texels.</param>
    /// <param name="scale">Per-axis scale; <c>null</c> means 1:1.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>UVs are nudged inward and the destination quad is quantised to viewport pixels so sampling lands on texel centres. See also the explicit-destination-size <see cref="DrawPartialDecal(Vector2d{float}, Vector2d{float}, Decal, Vector2d{float}, Vector2d{float}, Pixel?)"/>.</remarks>
    public void DrawPartialDecal(Vector2d<float> pos, Decal decal, Vector2d<float> sourcePos, Vector2d<float> sourceSize,
        Vector2d<float>? scale = null, Pixel? tint = null)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var t = tint ?? Pixel.WHITE;

        var sspX = pos.X * InvScreenSize.X * 2.0f - 1.0f;
        var sspY = -(pos.Y * InvScreenSize.Y * 2.0f - 1.0f);
        var ssdX = (pos.X + sourceSize.X * s.X) * InvScreenSize.X * 2.0f - 1.0f;
        var ssdY = -((pos.Y + sourceSize.Y * s.Y) * InvScreenSize.Y * 2.0f - 1.0f);

        // Quantise to viewport pixels so partial-decal sampling lands on texel centres.
        float vwX = ViewSize.X, vwY = ViewSize.Y;
        var qpX = MathF.Floor(sspX * vwX + 0.5f) / vwX;
        var qpY = MathF.Floor(sspY * vwY + 0.5f) / vwY;
        var qdX = MathF.Ceiling(ssdX * vwX + 0.5f) / vwX;
        var qdY = MathF.Ceiling(ssdY * vwY - 0.5f) / vwY;

        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = 4;
        di.Pos.Add(new Vector2d<float>(qpX, qpY));
        di.Pos.Add(new Vector2d<float>(qpX, qdY));
        di.Pos.Add(new Vector2d<float>(qdX, qdY));
        di.Pos.Add(new Vector2d<float>(qdX, qpY));

        var uvtlX = (sourcePos.X + 0.0001f) * decal.UVScale.X;
        var uvtlY = (sourcePos.Y + 0.0001f) * decal.UVScale.Y;
        var uvbrX = (sourcePos.X + sourceSize.X - 0.0001f) * decal.UVScale.X;
        var uvbrY = (sourcePos.Y + sourceSize.Y - 0.0001f) * decal.UVScale.Y;
        di.Uv.Add(new Vector2d<float>(uvtlX, uvtlY));
        di.Uv.Add(new Vector2d<float>(uvtlX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvtlY));
        for (var i = 0; i < 4; i++) { di.W.Add(1); di.Tint.Add(t); }
        SubmitDecal(di);
    }

    /// <summary>Overload of DrawPartialDecal that draws a decal sub-rectangle into an explicit destination size.</summary>
    /// <param name="pos">Top-left destination position in pixel space.</param>
    /// <param name="size">Destination size in pixels (the source sub-rectangle is stretched to fit).</param>
    /// <param name="decal">The decal (GPU texture) to sample.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle, in texels.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle, in texels.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Unlike the scale-based <see cref="DrawPartialDecal(Vector2d{float}, Decal, Vector2d{float}, Vector2d{float}, Vector2d{float}?, Pixel?)"/>, this overload sizes the destination quad directly.</remarks>
    public void DrawPartialDecal(Vector2d<float> pos, Vector2d<float> size, Decal decal, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
    {
        var t = tint ?? Pixel.WHITE;
        var sp = ScreenTransform(pos.X, pos.Y);
        var dimX = sp.X + 2.0f * size.X * InvScreenSize.X;
        var dimY = sp.Y - 2.0f * size.Y * InvScreenSize.Y;

        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = 4;
        di.Pos.Add(new Vector2d<float>(sp.X, sp.Y));
        di.Pos.Add(new Vector2d<float>(sp.X, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, dimY));
        di.Pos.Add(new Vector2d<float>(dimX, sp.Y));

        var uvtlX = sourcePos.X * decal.UVScale.X;
        var uvtlY = sourcePos.Y * decal.UVScale.Y;
        var uvbrX = uvtlX + sourceSize.X * decal.UVScale.X;
        var uvbrY = uvtlY + sourceSize.Y * decal.UVScale.Y;
        di.Uv.Add(new Vector2d<float>(uvtlX, uvtlY));
        di.Uv.Add(new Vector2d<float>(uvtlX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvtlY));
        for (var i = 0; i < 4; i++) { di.W.Add(1); di.Tint.Add(t); }
        SubmitDecal(di);
    }

    /// <summary>Queues a decal with explicit per-vertex positions, UVs and colours (positions are pixel-space, transformed to NDC).</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured colour quad.</param>
    /// <param name="pos">Per-vertex positions in pixel space (each transformed to NDC via <see cref="ScreenTransform"/>).</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="col">Per-vertex tint colours.</param>
    /// <param name="elements">Number of vertices to read from the arrays (default 4).</param>
    public void DrawExplicitDecal(Decal decal, Vector2d<float>[] pos, Vector2d<float>[] uv, Pixel[] col, int elements = 4)
    {
        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = (uint)elements;
        for (var i = 0; i < elements; i++)
        {
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
            di.Uv.Add(uv[i]);
            di.Tint.Add(col[i]);
            di.W.Add(1.0f);
        }
        SubmitDecal(di);
    }

    /// <summary>Queues a textured polygon decal with a single tint applied to all vertices.</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured polygon.</param>
    /// <param name="pos">Per-vertex positions in pixel space.</param>
    /// <param name="uv">Per-vertex texture coordinates (same count as <paramref name="pos"/>).</param>
    /// <param name="tint">Single tint applied to every vertex; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>The base overload. See <see cref="DrawPolygonDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Pixel})"/> for per-vertex colours and the depth-carrying overloads for a per-vertex W component.</remarks>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, Pixel? tint = null)
    {
        var t = tint ?? Pixel.WHITE;
        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = (uint)pos.Count;
        for (var i = 0; i < pos.Count; i++)
        {
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
            di.Uv.Add(uv[i]);
            di.Tint.Add(t);
            di.W.Add(1.0f);
        }
        SubmitDecal(di);
    }

    /// <summary>Overload of DrawPolygonDecal with a per-vertex tint colour.</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured polygon.</param>
    /// <param name="pos">Per-vertex positions in pixel space.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="tints">Per-vertex tint colours (same count as <paramref name="pos"/>).</param>
    /// <seealso cref="DrawPolygonDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, Pixel?)"/>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> tints)
    {
        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = (uint)pos.Count;
        for (var i = 0; i < pos.Count; i++)
        {
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
            di.Uv.Add(uv[i]);
            di.Tint.Add(tints[i]);
            di.W.Add(1.0f);
        }
        SubmitDecal(di);
    }

    /// <summary>Overload of DrawPolygonDecal: per-vertex colours modulated by a single tint, then drawn as a textured polygon.</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured polygon.</param>
    /// <param name="pos">Per-vertex positions in pixel space.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="colours">Per-vertex colours, each multiplied by <paramref name="tint"/>.</param>
    /// <param name="tint">Single tint modulating every entry of <paramref name="colours"/>.</param>
    /// <remarks>Convenience overload that pre-multiplies the colours, then forwards to the per-vertex-tint <see cref="DrawPolygonDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Pixel})"/>.</remarks>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv,
        IReadOnlyList<Pixel> colours, Pixel tint)
    {
        var newColours = new Pixel[colours.Count];
        for (var i = 0; i < colours.Count; i++) newColours[i] = colours[i] * tint;
        DrawPolygonDecal(decal, pos, uv, newColours);
    }

    /// <summary>Overload of DrawPolygonDecal with per-vertex depth (W) and a single tint.</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured polygon.</param>
    /// <param name="pos">Per-vertex positions in pixel space.</param>
    /// <param name="depth">Per-vertex W component (perspective depth), one per vertex.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="tint">Single tint applied to every vertex; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>The per-vertex <paramref name="depth"/> is stored as the vertex W component for perspective-correct interpolation. See the full <see cref="DrawPolygonDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{float}, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, System.Collections.Generic.IReadOnlyList{Pixel}, Pixel)"/> for per-vertex colours.</remarks>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<float> depth,
        IReadOnlyList<Vector2d<float>> uv, Pixel? tint = null)
    {
        var t = tint ?? Pixel.WHITE;
        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = (uint)pos.Count;
        for (var i = 0; i < pos.Count; i++)
        {
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
            di.Uv.Add(uv[i]);
            di.Tint.Add(t);
            di.W.Add(depth[i]);
        }
        SubmitDecal(di);
    }

    /// <summary>Overload of DrawPolygonDecal with per-vertex depth (W) and per-vertex colours modulated by a tint (the full olc overload).</summary>
    /// <param name="decal">The decal to sample, or <c>null</c> for an untextured polygon.</param>
    /// <param name="pos">Per-vertex positions in pixel space.</param>
    /// <param name="depth">Per-vertex W component (perspective depth), one per vertex.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="colours">Per-vertex colours, each multiplied by <paramref name="tint"/>.</param>
    /// <param name="tint">Single tint modulating every entry of <paramref name="colours"/>.</param>
    /// <remarks>The most general decal-polygon overload; <paramref name="depth"/> becomes the vertex W component.</remarks>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<float> depth,
        IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> colours, Pixel tint)
    {
        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = (uint)pos.Count;
        for (var i = 0; i < pos.Count; i++)
        {
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
            di.Uv.Add(uv[i]);
            di.Tint.Add(colours[i] * tint);
            di.W.Add(depth[i]);
        }
        SubmitDecal(di);
    }

    /// <summary>Draws a single-pixel line as a wireframe decal between two points.</summary>
    /// <param name="pos1">Start point in pixel space.</param>
    /// <param name="pos2">End point in pixel space.</param>
    /// <param name="p">Line colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Temporarily switches the decal mode to <c>Wireframe</c>, submits the line via the untextured polygon path, then restores the previous mode.</remarks>
    public void DrawLineDecal(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null)
    {
        var col = p ?? Pixel.WHITE;
        var m = _decalMode;
        _decalMode = DecalMode.Wireframe;
        _scratchPts2[0] = pos1;
        _scratchPts2[1] = pos2;
        DrawPolygonDecal(null!, _scratchPts2, ZeroUv4, col); // untextured colour line: no decal
        _decalMode = m;
    }

    /// <summary>Fills the 4-point scratch with an axis-aligned quad (ClockWise from top-left) for colour-quad decals.</summary>
    /// <param name="pos">Top-left corner of the quad in pixel space.</param>
    /// <param name="size">Width and height of the quad in pixels.</param>
    private void FillScratchQuad(Vector2d<float> pos, Vector2d<float> size)
    {
        _scratchPts4[0] = pos;
        _scratchPts4[1] = new Vector2d<float>(pos.X, pos.Y + size.Y);
        _scratchPts4[2] = new Vector2d<float>(pos.X + size.X, pos.Y + size.Y);
        _scratchPts4[3] = new Vector2d<float>(pos.X + size.X, pos.Y);
    }

    /// <summary>Draws a rectangle outline as a wireframe colour-quad decal.</summary>
    /// <param name="pos">Top-left corner in pixel space.</param>
    /// <param name="size">Width and height in pixels.</param>
    /// <param name="col">Outline colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Submitted in <c>Wireframe</c> decal mode (restored afterwards). See <see cref="FillRectDecal"/> for the solid fill and <see cref="GradientFillRectDecal"/> for a per-corner gradient.</remarks>
    public void DrawRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var m = _decalMode;
        SetDecalMode(DecalMode.Wireframe);
        FillScratchQuad(pos, size);
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = _scratchCol4[3] = c;
        DrawExplicitDecal(null!, _scratchPts4, ZeroUv4, _scratchCol4); // untextured colour quad: no decal
        SetDecalMode(m);
    }

    /// <summary>Draws a filled rectangle as a solid colour-quad decal.</summary>
    /// <param name="pos">Top-left corner in pixel space.</param>
    /// <param name="size">Width and height in pixels.</param>
    /// <param name="col">Fill colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <seealso cref="DrawRectDecal"/>
    /// <seealso cref="GradientFillRectDecal"/>
    public void FillRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        FillScratchQuad(pos, size);
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = _scratchCol4[3] = c;
        DrawExplicitDecal(null!, _scratchPts4, ZeroUv4, _scratchCol4); // untextured colour quad: no decal
    }

    /// <summary>Draws a rectangle as a colour-quad decal with a per-corner gradient.</summary>
    /// <param name="pos">Top-left corner in pixel space.</param>
    /// <param name="size">Width and height in pixels.</param>
    /// <param name="colTL">Top-left corner colour.</param>
    /// <param name="colBL">Bottom-left corner colour.</param>
    /// <param name="colBR">Bottom-right corner colour.</param>
    /// <param name="colTR">Top-right corner colour.</param>
    /// <seealso cref="FillRectDecal"/>
    public void GradientFillRectDecal(Vector2d<float> pos, Vector2d<float> size,
        Pixel colTL, Pixel colBL, Pixel colBR, Pixel colTR)
    {
        FillScratchQuad(pos, size);
        _scratchCol4[0] = colTL; _scratchCol4[1] = colBL; _scratchCol4[2] = colBR; _scratchCol4[3] = colTR;
        DrawExplicitDecal(null!, _scratchPts4, ZeroUv4, _scratchCol4); // untextured colour quad: no decal
    }

    /// <summary>Draws a filled triangle as a solid colour decal.</summary>
    /// <param name="p0">First vertex in pixel space.</param>
    /// <param name="p1">Second vertex in pixel space.</param>
    /// <param name="p2">Third vertex in pixel space.</param>
    /// <param name="col">Fill colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <seealso cref="GradientTriangleDecal"/>
    public void FillTriangleDecal(Vector2d<float> p0, Vector2d<float> p1, Vector2d<float> p2, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        _scratchPts4[0] = p0; _scratchPts4[1] = p1; _scratchPts4[2] = p2;
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = c;
        DrawExplicitDecal(null!, _scratchPts4, ZeroUv4, _scratchCol4, 3); // untextured colour triangle: no decal
    }

    /// <summary>Draws a triangle as a colour decal with a per-vertex gradient.</summary>
    /// <param name="p0">First vertex in pixel space.</param>
    /// <param name="p1">Second vertex in pixel space.</param>
    /// <param name="p2">Third vertex in pixel space.</param>
    /// <param name="c0">Colour at <paramref name="p0"/>.</param>
    /// <param name="c1">Colour at <paramref name="p1"/>.</param>
    /// <param name="c2">Colour at <paramref name="p2"/>.</param>
    /// <seealso cref="FillTriangleDecal"/>
    public void GradientTriangleDecal(Vector2d<float> p0, Vector2d<float> p1, Vector2d<float> p2,
        Pixel c0, Pixel c1, Pixel c2)
    {
        _scratchPts4[0] = p0; _scratchPts4[1] = p1; _scratchPts4[2] = p2;
        _scratchCol4[0] = c0; _scratchCol4[1] = c1; _scratchCol4[2] = c2;
        DrawExplicitDecal(null!, _scratchPts4, ZeroUv4, _scratchCol4, 3); // untextured colour triangle: no decal
    }

    /// <summary>Draws a decal rotated about a center point (submitted as a transformed-quad GPU task).</summary>
    /// <param name="pos">Anchor position in pixel space about which the rotation is applied.</param>
    /// <param name="decal">The decal to draw.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Pivot offset within the decal, in texels; <c>null</c> means the top-left corner.</param>
    /// <param name="scale">Per-axis scale; <c>null</c> means 1:1.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Unlike the other decal calls, olc submits rotation as a GPU task (a pre-transformed quad) rather than a plain decal instance, via <see cref="NextGpuTask"/>.</remarks>
    public void DrawRotatedDecal(Vector2d<float> pos, Decal decal, float angle,
        Vector2d<float>? center = null, Vector2d<float>? scale = null, Pixel? tint = null)
    {
        var ctr = center ?? new Vector2d<float>(0, 0);
        var s = scale ?? new Vector2d<float>(1, 1);
        var t = tint ?? Pixel.WHITE;

        var local = new[]
        {
            new Vector2d<float>((0 - ctr.X) * s.X, (0 - ctr.Y) * s.Y),
            new Vector2d<float>((0 - ctr.X) * s.X, (decal.Sprite.Height - ctr.Y) * s.Y),
            new Vector2d<float>((decal.Sprite.Width - ctr.X) * s.X, (decal.Sprite.Height - ctr.Y) * s.Y),
            new Vector2d<float>((decal.Sprite.Width - ctr.X) * s.X, (0 - ctr.Y) * s.Y)
        };
        float c = MathF.Cos(angle), sn = MathF.Sin(angle);
        var sp = new Vector2d<float>[4];
        for (var i = 0; i < 4; i++)
        {
            var rx = pos.X + (local[i].X * c - local[i].Y * sn);
            var ry = pos.Y + (local[i].X * sn + local[i].Y * c);
            sp[i] = ScreenTransform(rx, ry);
        }

        // olc submits rotation as a GPU task (a transformed quad), not a plain decal instance.
        var task = NextGpuTask();
        task.Decal = decal;
        task.Mode = _decalMode;
        task.Structure = _decalStructure;
        task.Depth = false;
        task.Vb.Add(new Vertex(sp[0].X, sp[0].Y, 0.0f, 1.0f, 0.0f, 0.0f, t.N));
        task.Vb.Add(new Vertex(sp[1].X, sp[1].Y, 0.0f, 1.0f, 0.0f, 1.0f, t.N));
        task.Vb.Add(new Vertex(sp[2].X, sp[2].Y, 0.0f, 1.0f, 1.0f, 1.0f, t.N));
        task.Vb.Add(new Vertex(sp[3].X, sp[3].Y, 0.0f, 1.0f, 1.0f, 0.0f, t.N));
    }

    /// <summary>Draws a sub-rectangle of a decal rotated about a center point.</summary>
    /// <param name="pos">Anchor position in pixel space about which the rotation is applied.</param>
    /// <param name="decal">The decal to sample.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Pivot offset within the source sub-rectangle, in texels.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle, in texels.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle, in texels.</param>
    /// <param name="scale">Per-axis scale; <c>null</c> means 1:1.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    public void DrawPartialRotatedDecal(Vector2d<float> pos, Decal decal, float angle, Vector2d<float> center,
        Vector2d<float> sourcePos, Vector2d<float> sourceSize, Vector2d<float>? scale = null, Pixel? tint = null)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var t = tint ?? Pixel.WHITE;

        var local = new[]
        {
            new Vector2d<float>((0 - center.X) * s.X, (0 - center.Y) * s.Y),
            new Vector2d<float>((0 - center.X) * s.X, (sourceSize.Y - center.Y) * s.Y),
            new Vector2d<float>((sourceSize.X - center.X) * s.X, (sourceSize.Y - center.Y) * s.Y),
            new Vector2d<float>((sourceSize.X - center.X) * s.X, (0 - center.Y) * s.Y)
        };
        float c = MathF.Cos(angle), sn = MathF.Sin(angle);

        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = 4;
        for (var i = 0; i < 4; i++)
        {
            var rx = pos.X + (local[i].X * c - local[i].Y * sn);
            var ry = pos.Y + (local[i].X * sn + local[i].Y * c);
            di.Pos.Add(ScreenTransform(rx, ry));
            di.W.Add(1);
            di.Tint.Add(t);
        }

        var uvtlX = sourcePos.X * decal.UVScale.X;
        var uvtlY = sourcePos.Y * decal.UVScale.Y;
        var uvbrX = uvtlX + sourceSize.X * decal.UVScale.X;
        var uvbrY = uvtlY + sourceSize.Y * decal.UVScale.Y;
        di.Uv.Add(new Vector2d<float>(uvtlX, uvtlY));
        di.Uv.Add(new Vector2d<float>(uvtlX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvbrY));
        di.Uv.Add(new Vector2d<float>(uvbrX, uvtlY));
        SubmitDecal(di);
    }

    /// <summary>Draws a decal warped into an arbitrary quad (projective), using the full texture.</summary>
    /// <param name="decal">The decal to sample.</param>
    /// <param name="pos">The four destination corner positions (pixel space) defining the warped quad.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Forwards to the private projective-warp implementation with full 0..1 UVs. See <see cref="DrawPartialWarpedDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, Vector2d{float}, Vector2d{float}, Pixel?)"/> to warp only a source sub-rectangle.</remarks>
    public void DrawWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Pixel? tint = null)
        => DrawPartialWarpedDecal(decal, pos, new Vector2d<float>(0, 0), new Vector2d<float>(1, 1), tint, useFullUv: true);

    /// <summary>Draws a sub-rectangle of a decal warped into an arbitrary quad (projective).</summary>
    /// <param name="decal">The decal to sample.</param>
    /// <param name="pos">The four destination corner positions (pixel space) defining the warped quad.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle, in texels.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle, in texels.</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <remarks>Forwards to the shared private projective-warp implementation with the given source UVs. Compare <see cref="DrawWarpedDecal"/>, which uses the full texture.</remarks>
    public void DrawPartialWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
        => DrawPartialWarpedDecal(decal, pos, sourcePos, sourceSize, tint, useFullUv: false);

    /// <summary>Shared projective-warp implementation (Nathan Reed's quad interpolation); useFullUv selects 0..1 unit UVs vs a source sub-rect.</summary>
    /// <param name="decal">The decal to sample.</param>
    /// <param name="pos">The four destination corner positions (pixel space) defining the warped quad.</param>
    /// <param name="sourcePos">Top-left of the source sub-rectangle, in texels (ignored when <paramref name="useFullUv"/> is <c>true</c>).</param>
    /// <param name="sourceSize">Size of the source sub-rectangle, in texels (ignored when <paramref name="useFullUv"/> is <c>true</c>).</param>
    /// <param name="tint">Modulating colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <param name="useFullUv">When <c>true</c>, uses unit 0..1 UVs (full texture); when <c>false</c>, uses the source sub-rectangle UVs.</param>
    /// <remarks>Backs both <see cref="DrawWarpedDecal"/> and the public <see cref="DrawPartialWarpedDecal(Decal, System.Collections.Generic.IReadOnlyList{Vector2d{float}}, Vector2d{float}, Vector2d{float}, Pixel?)"/>. Computes per-corner projective weights (Nathan Reed quad interpolation), storing each weight as the vertex W component. Returns early without queuing if the quad is degenerate (zero area).</remarks>
    private void DrawPartialWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint, bool useFullUv)
    {
        var t = tint ?? Pixel.WHITE;
        var rd = (pos[2].X - pos[0].X) * (pos[3].Y - pos[1].Y) - (pos[3].X - pos[1].X) * (pos[2].Y - pos[0].Y);
        if (rd == 0) return;

        Vector2d<float>[] uv;
        if (useFullUv)
        {
            uv = new[] { new Vector2d<float>(0, 0), new Vector2d<float>(0, 1), new Vector2d<float>(1, 1), new Vector2d<float>(1, 0) };
        }
        else
        {
            var uvtlX = sourcePos.X * decal.UVScale.X;
            var uvtlY = sourcePos.Y * decal.UVScale.Y;
            var uvbrX = uvtlX + sourceSize.X * decal.UVScale.X;
            var uvbrY = uvtlY + sourceSize.Y * decal.UVScale.Y;
            uv = new[] { new Vector2d<float>(uvtlX, uvtlY), new Vector2d<float>(uvtlX, uvbrY), new Vector2d<float>(uvbrX, uvbrY), new Vector2d<float>(uvbrX, uvtlY) };
        }

        rd = 1.0f / rd;
        var rn = ((pos[3].X - pos[1].X) * (pos[0].Y - pos[1].Y) - (pos[3].Y - pos[1].Y) * (pos[0].X - pos[1].X)) * rd;
        var sn = ((pos[2].X - pos[0].X) * (pos[0].Y - pos[1].Y) - (pos[2].Y - pos[0].Y) * (pos[0].X - pos[1].X)) * rd;
        float centerX = 0, centerY = 0;
        if (!(rn < 0f || rn > 1f || sn < 0f || sn > 1f))
        {
            centerX = pos[0].X + rn * (pos[2].X - pos[0].X);
            centerY = pos[0].Y + rn * (pos[2].Y - pos[0].Y);
        }

        var d = new float[4];
        for (var i = 0; i < 4; i++)
        {
            var dx = pos[i].X - centerX;
            var dy = pos[i].Y - centerY;
            d[i] = MathF.Sqrt(dx * dx + dy * dy);
        }

        var di = NextDecalInstance();
        di.Decal = decal;
        di.Points = 4;
        for (var i = 0; i < 4; i++)
        {
            var q = d[i] == 0.0f ? 1.0f : (d[i] + d[(i + 2) & 3]) / d[(i + 2) & 3];
            di.Uv.Add(new Vector2d<float>(uv[i].X * q, uv[i].Y * q));
            di.W.Add(q);
            di.Tint.Add(t);
            di.Pos.Add(ScreenTransform(pos[i].X, pos[i].Y));
        }
        SubmitDecal(di);
    }

    /// <summary>Draws monospaced text as decals (each glyph a partial font decal).</summary>
    /// <param name="pos">Top-left position of the text in pixel space.</param>
    /// <param name="text">The string to draw; <c>\n</c> starts a new line and <c>\t</c> advances by the tab width.</param>
    /// <param name="col">Text colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <param name="scale">Per-axis glyph scale; <c>null</c> means 1:1.</param>
    /// <remarks>Each glyph is drawn as a partial decal of the embedded font sheet. See <see cref="DrawStringPropDecal"/> for proportional spacing.</remarks>
    public void DrawStringDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
    {
        var c = col ?? Pixel.WHITE;
        var s = scale ?? new Vector2d<float>(1, 1);
        float sx = 0, sy = 0;
        foreach (var ch in text)
        {
            if (ch == '\n') { sx = 0; sy += 8.0f * s.Y; }
            else if (ch == '\t') { sx += 8.0f * _tabSizeInSpaces * s.X; }
            else
            {
                int ox = (ch - 32) % 16, oy = (ch - 32) / 16;
                DrawPartialDecal(new Vector2d<float>(pos.X + sx, pos.Y + sy), _fontRenderable.Decal,
                    new Vector2d<float>(ox * 8.0f, oy * 8.0f), new Vector2d<float>(8.0f, 8.0f), s, c);
                sx += 8.0f * s.X;
            }
        }
    }

    /// <summary>Draws proportionally-spaced text as decals (each glyph a partial font decal).</summary>
    /// <param name="pos">Top-left position of the text in pixel space.</param>
    /// <param name="text">The string to draw; <c>\n</c> starts a new line and <c>\t</c> advances by the tab width.</param>
    /// <param name="col">Text colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <param name="scale">Per-axis glyph scale; <c>null</c> means 1:1.</param>
    /// <remarks>Like <see cref="DrawStringDecal"/> but advances by each glyph's proportional width from the font spacing table.</remarks>
    public void DrawStringPropDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
    {
        var c = col ?? Pixel.WHITE;
        var s = scale ?? new Vector2d<float>(1, 1);
        float sx = 0, sy = 0;
        foreach (var ch in text)
        {
            if (ch == '\n') { sx = 0; sy += 8.0f * s.Y; }
            else if (ch == '\t') { sx += 8.0f * _tabSizeInSpaces * s.X; }
            else
            {
                int ox = (ch - 32) % 16, oy = (ch - 32) / 16;
                var spacing = _fontSpacing[ch - 32];
                DrawPartialDecal(new Vector2d<float>(pos.X + sx, pos.Y + sy), _fontRenderable.Decal,
                    new Vector2d<float>(ox * 8.0f + spacing.X, oy * 8.0f), new Vector2d<float>(spacing.Y, 8.0f), s, c);
                sx += spacing.Y * s.X;
            }
        }
    }

    /// <summary>Draws monospaced text as decals rotated about a center point.</summary>
    /// <param name="pos">Anchor position in pixel space about which the text is rotated.</param>
    /// <param name="text">The string to draw; <c>\n</c> starts a new line and <c>\t</c> advances by the tab width.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Pivot offset for the text block; <c>null</c> means the origin.</param>
    /// <param name="col">Text colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <param name="scale">Per-axis glyph scale; <c>null</c> means 1:1.</param>
    /// <remarks>Each glyph is emitted via <see cref="DrawPartialRotatedDecal"/>. See <see cref="DrawRotatedStringPropDecal"/> for proportional spacing.</remarks>
    public void DrawRotatedStringDecal(Vector2d<float> pos, string text, float angle,
        Vector2d<float>? center = null, Pixel? col = null, Vector2d<float>? scale = null)
    {
        var ctr = center ?? new Vector2d<float>(0, 0);
        var c = col ?? Pixel.WHITE;
        var s = scale ?? new Vector2d<float>(1, 1);
        float sposX = ctr.X, sposY = ctr.Y;
        foreach (var ch in text)
        {
            if (ch == '\n') { sposX = ctr.X; sposY -= 8.0f; }
            else if (ch == '\t') { sposX += 8.0f * _tabSizeInSpaces * s.X; }
            else
            {
                int ox = (ch - 32) % 16, oy = (ch - 32) / 16;
                DrawPartialRotatedDecal(pos, _fontRenderable.Decal, angle, new Vector2d<float>(sposX, sposY),
                    new Vector2d<float>(ox * 8.0f, oy * 8.0f), new Vector2d<float>(8.0f, 8.0f), s, c);
                sposX -= 8.0f;
            }
        }
    }

    /// <summary>Draws proportionally-spaced text as decals rotated about a center point.</summary>
    /// <param name="pos">Anchor position in pixel space about which the text is rotated.</param>
    /// <param name="text">The string to draw; <c>\n</c> starts a new line and <c>\t</c> advances by the tab width.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Pivot offset for the text block; <c>null</c> means the origin.</param>
    /// <param name="col">Text colour; <c>null</c> means <c>Pixel.WHITE</c>.</param>
    /// <param name="scale">Per-axis glyph scale; <c>null</c> means 1:1.</param>
    /// <remarks>Like <see cref="DrawRotatedStringDecal"/> but advances by each glyph's proportional width.</remarks>
    public void DrawRotatedStringPropDecal(Vector2d<float> pos, string text, float angle,
        Vector2d<float>? center = null, Pixel? col = null, Vector2d<float>? scale = null)
    {
        var ctr = center ?? new Vector2d<float>(0, 0);
        var c = col ?? Pixel.WHITE;
        var s = scale ?? new Vector2d<float>(1, 1);
        float sposX = ctr.X, sposY = ctr.Y;
        foreach (var ch in text)
        {
            if (ch == '\n') { sposX = ctr.X; sposY -= 8.0f; }
            else if (ch == '\t') { sposX += 8.0f * _tabSizeInSpaces * s.X; }
            else
            {
                int ox = (ch - 32) % 16, oy = (ch - 32) / 16;
                var spacing = _fontSpacing[ch - 32];
                DrawPartialRotatedDecal(pos, _fontRenderable.Decal, angle, new Vector2d<float>(sposX, sposY),
                    new Vector2d<float>(ox * 8.0f + spacing.X, oy * 8.0f), new Vector2d<float>(spacing.Y, 8.0f), s, c);
                sposX -= spacing.Y;
            }
        }
    }

    /// <summary>Four zero UVs, used by the colour-quad decals that carry no texture.</summary>
    private static readonly Vector2d<float>[] ZeroUv4 =
    {
        new(0, 0), new(0, 0), new(0, 0), new(0, 0)
    };

    // O-------------------------------------------------------------------O
    // | Screen / viewport sizing                                          |
    // O-------------------------------------------------------------------O

    /// <summary>Resizes the screen, rebuilding every layer's draw target at the new size and refreshing the viewport.</summary>
    /// <param name="w">New screen width in pixels.</param>
    /// <param name="h">New screen height in pixels.</param>
    /// <remarks>Recreates every layer's draw-target sprite at the new size and flags it for upload, resets the draw target, and refreshes the renderer viewport. See <see cref="UpdateWindowSize"/> and <see cref="UpdateViewport"/>.</remarks>
    public void SetScreenSize(int w, int h)
    {
        ScreenSize = new Vector2d<int>(w, h);
        InvScreenSize = new Vector2d<float>(1.0f / w, 1.0f / h);
        foreach (var layer in _layers)
        {
            layer.PDrawTarget.Create((uint)ScreenSize.X, (uint)ScreenSize.Y, false, true);
            layer.BUpdate = true;
        }
        SetDrawTarget(null);
        if (!RealWindowMode)
        {
            _renderer.ClearBuffer(Pixel.BLACK, true);
            _renderer.DisplayFrame();
            _renderer.ClearBuffer(Pixel.BLACK, true);
        }
        _renderer.UpdateViewport(ViewPos, ViewSize);
    }

    /// <summary>Records a new window size, flags a screen-buffer resize in real-window mode, and recomputes the viewport.</summary>
    /// <param name="x">New window width in pixels.</param>
    /// <param name="y">New window height in pixels.</param>
    /// <remarks>In real-window mode it flags a deferred screen-buffer resize (handled after the frame is displayed via <see cref="SetScreenSize"/>); always recomputes the letterboxed viewport via <see cref="UpdateViewport"/>.</remarks>
    private void UpdateWindowSize(int x, int y)
    {
        WindowSize = new Vector2d<int>(x, y);
        if (RealWindowMode)
            _resizeRequested = true; // handled after DisplayFrame in CoreUpdate
        UpdateViewport();
    }

    /// <summary>Recomputes the letterboxed viewport (position and size) preserving aspect ratio, honouring pixel cohesion and real-window mode.</summary>
    /// <remarks>In real-window mode the viewport fills the whole window; otherwise it letterboxes the screen within the window, preserving aspect ratio and (when pixel cohesion is enabled) snapping the pixel size to integers. See <see cref="UpdateWindowSize"/> and <see cref="SetScreenSize"/>.</remarks>
    private void UpdateViewport()
    {
        if (RealWindowMode)
        {
            PixelSize = new Vector2d<int>(1, 1);
            ViewSize = ScreenSize;
            ViewPos = new Vector2d<int>(0, 0);
            return;
        }

        var ww = ScreenSize.X * PixelSize.X;
        var wh = ScreenSize.Y * PixelSize.Y;
        var wasp = (float)ww / wh;

        // Scalar component math (the X/Y components are computed differently, so vector ops
        // wouldn't help here anyway).
        if (PixelCohesion)
        {
            var spsX = WindowSize.X / ScreenSize.X;
            var spsY = WindowSize.Y / ScreenSize.Y;
            ScreenPixelSize = new Vector2d<int>(spsX, spsY);
            ViewSize = new Vector2d<int>(spsX * ScreenSize.X, spsY * ScreenSize.Y);
        }
        else
        {
            var vx = WindowSize.X;
            var vy = (int)(vx / wasp);
            if (vy > WindowSize.Y)
            {
                vy = WindowSize.Y;
                vx = (int)(vy * wasp);
            }
            ViewSize = new Vector2d<int>(vx, vy);
        }

        ViewPos = new Vector2d<int>((WindowSize.X - ViewSize.X) / 2, (WindowSize.Y - ViewSize.Y) / 2);
    }
}
