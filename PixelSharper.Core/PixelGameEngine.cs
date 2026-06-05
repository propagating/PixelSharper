using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Platforms;
using PixelSharper.Core.Renderers;
using PixelSharper.Core.Types;

namespace PixelSharper.Core;

// Port of olc::PixelGameEngine. olc splits work across a main thread (window + OS event loop)
// and an engine thread (GL context + frame loop). Our OpenTK platform is single-threaded, so
// Start() does everything on the calling thread: create window, create graphics, then run the
// core loop until the window is closed.
public abstract class PixelGameEngine
{
    public string ApplicationName { get; set; }
    public abstract bool OnCreate();
    public abstract bool OnUpdate(float elapsedTime);
    public static PixelConfiguration Configuration { get; set; }

    // --- Construction-time configuration (set by Construct) ---
    public Vector2d<float> VectorPixel { get; set; }
    public bool EnableVSYNC { get; set; }
    public bool FullScreen { get; set; }
    public Vector2d<int> WindowSize { get; set; }
    public Vector2d<int> PixelSize { get; set; }
    public Vector2d<float> InvScreenSize { get; set; }
    public Vector2d<int> ScreenSize { get; set; }
    public bool RealWindowMode { get; set; }
    public bool PixelCohesion { get; set; }

    // --- Viewport (the letterboxed region of the window the screen is drawn into) ---
    public Vector2d<int> ViewPos { get; set; }
    public Vector2d<int> ViewSize { get; set; }
    public Vector2d<int> ScreenPixelSize { get; set; }

    // --- Per-frame state ---
    public uint LastFps { get; private set; }
    public float LastElapsed { get; private set; }

    private PlatformOpenTK _platform;
    private RendererOgl10 _renderer;

    private readonly List<LayerDesc> _layers = new();
    private Sprite _drawTarget;
    private byte _targetLayer;

    private PixelDisplayMode _pixelMode = PixelDisplayMode.Normal;
    private float _blendFactor = 1.0f;
    private DecalMode _decalMode = DecalMode.Normal;
    private DecalStructure _decalStructure = DecalStructure.Fan;
    private bool _hw3dDepthTest = true;
    private CullMode _hw3dCullMode = CullMode.NONE;
    // Reusable scratch for the colour-quad/line decal helpers — DrawExplicitDecal/DrawPolygonDecal
    // copy the data into the pooled instance synchronously, so a single shared buffer is safe.
    private readonly Vector2d<float>[] _scratchPts4 = new Vector2d<float>[4];
    private readonly Pixel[] _scratchCol4 = new Pixel[4];
    private readonly Vector2d<float>[] _scratchPts2 = new Vector2d<float>[2];
    // Scratch for the textured-triangle/polygon path (filled & consumed synchronously per call).
    private readonly Vector2d<float>[] _texTri3 = new Vector2d<float>[3];
    private readonly Pixel[] _colTri3 = new Pixel[3];
    private readonly Vector2d<float>[] _polyPos3 = new Vector2d<float>[3];
    private readonly Vector2d<float>[] _polyTex3 = new Vector2d<float>[3];
    private readonly Pixel[] _polyCol3 = new Pixel[3];
    private static readonly Pixel[] WhiteQuad4 = { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE, Pixel.WHITE };
    private Func<int, int, Pixel, Pixel, Pixel> _funcPixelMode;
    private bool _suspendTextureTransfer;

    // Embedded 8x8 font: a 128x48 sprite plus per-glyph proportional spacing (x offset, width).
    private Renderable _fontRenderable;
    private readonly List<Vector2d<int>> _fontSpacing = new();
    private int _tabSizeInSpaces = 4;

    private readonly Stopwatch _clock = new();
    private double _lastTime;
    private float _frameTimer;
    private int _frameCount;
    private bool _active;
    private bool _resizeRequested;
    private readonly List<PGEX> _extensions = new();

    protected PixelGameEngine()
    {
        // olc sets PGEX::pge in the engine constructor, so extensions constructed by the user
        // (in their subclass ctor / OnCreate) can find the engine and auto-register.
        PGEX.Pge = this;
    }

    internal void RegisterExtension(PGEX extension)
    {
        if (!_extensions.Contains(extension))
            _extensions.Add(extension);
    }

    // --- Hardware input state (olc's pKeyboardState / pMouseState model) ---
    private const int MouseButtonCount = 5; // olc's nMouseButtons
    private readonly HardwareButton[] _keyboardState = new HardwareButton[(int)KeyPress.ENUM_END];
    private readonly bool[] _keyOldState = new bool[(int)KeyPress.ENUM_END];
    private readonly bool[] _keyNewState = new bool[(int)KeyPress.ENUM_END];
    private readonly HardwareButton[] _mouseButtonState = new HardwareButton[MouseButtonCount];
    private readonly bool[] _mouseOldState = new bool[MouseButtonCount];
    private readonly bool[] _mouseNewState = new bool[MouseButtonCount];
    private Vector2d<int> _mousePos;
    private Vector2d<int> _mousePosCache;
    private Vector2d<int> _mouseWindowPos;
    private int _mouseWheelDelta;
    private int _mouseWheelDeltaCache;
    private bool _hasInputFocus;
    private static readonly List<string> NoFiles = new();
    private List<string> _droppedFiles = NoFiles;
    private Vector2d<int> _droppedFilesPoint;

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
            return FileReadCode.FAIL;
        return FileReadCode.OK;
    }

    public FileReadCode Start()
    {
        // Wire up the platform + renderer and the static back-references olc keeps as globals.
        _platform = new PlatformOpenTK();
        _renderer = new RendererOgl10();
        Renderer.Active = _renderer;
        Renderer.PtrPGE = this;
        Platform.PtrPGE = this;

        if (_platform.ApplicationStartUp() != FileReadCode.OK) return FileReadCode.FAIL;

        // Construct the window
        if (_platform.CreateWindowPane(new Vector2d<int>(30, 30), WindowSize, FullScreen) != FileReadCode.OK)
            return FileReadCode.FAIL;
        UpdateWindowSize(WindowSize.X, WindowSize.Y);

        if (_platform.ThreadStartUp() != FileReadCode.OK) return FileReadCode.FAIL;
        PrepareEngine();

        foreach (var ext in _extensions) ext.OnBeforeUserCreate();
        _active = OnCreate();
        foreach (var ext in _extensions) ext.OnAfterUserCreate();

        while (_active)
        {
            CoreUpdate();
            if (_platform.ShouldClose) _active = false;
        }

        _platform.ThreadCleanUp();
        if (_platform.ApplicationCleanUp() != FileReadCode.OK) return FileReadCode.FAIL;
        return FileReadCode.OK;
    }

    private void PrepareEngine()
    {
        // Start OpenGL; the context becomes current on this thread inside CreateGraphics.
        if (_platform.CreateGraphics(FullScreen, EnableVSYNC, ViewPos, ViewSize) == FileReadCode.FAIL)
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

        // Display frame
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

    public uint CreateLayer()
    {
        var layer = new LayerDesc();
        layer.PDrawTarget.Create((uint)ScreenSize.X, (uint)ScreenSize.Y, false, true);
        _layers.Add(layer);
        return (uint)_layers.Count - 1;
    }

    public List<LayerDesc> GetLayers() => _layers;

    public void EnableLayer(byte layer, bool show)
    {
        if (layer < _layers.Count) _layers[layer].BShow = show;
    }

    public void SetLayerOffset(byte layer, Vector2d<float> offset)
    {
        if (layer < _layers.Count) _layers[layer].VOffset = offset;
    }

    public void SetLayerScale(byte layer, Vector2d<float> scale)
    {
        if (layer < _layers.Count) _layers[layer].VScale = scale;
    }

    public void SetLayerTint(byte layer, Pixel tint)
    {
        if (layer < _layers.Count) _layers[layer].Tint = tint;
    }

    public void SetLayerCustomRenderFunction(byte layer, Action f)
    {
        if (layer < _layers.Count) _layers[layer].FuncHook = f;
    }

    // O-------------------------------------------------------------------O
    // | Hardware input                                                    |
    // O-------------------------------------------------------------------O

    public HardwareButton GetKey(KeyPress key) => _keyboardState[(int)key];
    public HardwareButton GetMouse(int button) =>
        button >= 0 && button < MouseButtonCount ? _mouseButtonState[button] : default;
    public int GetMouseX() => _mousePos.X;
    public int GetMouseY() => _mousePos.Y;
    public Vector2d<int> GetMousePos() => _mousePos;
    public Vector2d<int> GetWindowMouse() => _mouseWindowPos;
    public int GetMouseWheel() => _mouseWheelDelta;
    public bool IsFocused() => _hasInputFocus;
    // Files dropped onto the window this frame (empty otherwise), and the drop point in pixel space.
    public IReadOnlyList<string> GetDroppedFiles() => _droppedFiles;
    public Vector2d<int> GetDroppedFilesPoint() => _droppedFilesPoint;

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

    // Port of olc's per-frame ScanHardware lambda: derive Pressed/Released/Held edges from the
    // new vs. previous raw button states. Arrays (not List) so element-field writes mutate in place.
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

    // Same window->pixel-space transform as the mouse, used for the file-drop point.
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

    // Mouse coords arrive in window space; transform into pixel (screen) space, clamped to screen.
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

    public void SetDrawTarget(Sprite target)
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

    public void SetDrawTarget(byte layer, bool dirty = true)
    {
        if (layer >= _layers.Count) return;
        _drawTarget = _layers[layer].PDrawTarget.Sprite;
        _layers[layer].BUpdate = dirty;
        _targetLayer = layer;
    }

    public Sprite GetDrawTarget() => _drawTarget;
    public int GetDrawTargetWidth() => _drawTarget?.Width ?? 0;
    public int GetDrawTargetHeight() => _drawTarget?.Height ?? 0;
    public int ScreenWidth() => ScreenSize.X;
    public int ScreenHeight() => ScreenSize.Y;
    public uint GetFps() => LastFps;
    public float GetElapsedTime() => LastElapsed;

    public void SetPixelMode(PixelDisplayMode mode) => _pixelMode = mode;
    public PixelDisplayMode GetPixelMode() => _pixelMode;
    public void SetPixelMode(Func<int, int, Pixel, Pixel, Pixel> pixelMode)
    {
        _funcPixelMode = pixelMode;
        _pixelMode = PixelDisplayMode.Custom;
    }
    public void SetPixelBlend(float blend) => _blendFactor = Math.Clamp(blend, 0.0f, 1.0f);

    // The critical function that plots a pixel into the current draw target.
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

    public bool Draw(Vector2d<int> pos, Pixel p) => Draw(pos.X, pos.Y, p);

    public void Clear(Pixel p)
    {
        var target = GetDrawTarget();
        if (target == null) return;
        // Vectorised fill over the backing array rather than a per-element List indexer loop.
        CollectionsMarshal.AsSpan(target.PixelData).Fill(p);
    }

    public void ClearBuffer(Pixel p, bool depth = true) => _renderer.ClearBuffer(p, depth);

    // When disabled, layer draw-target sprites are not re-uploaded to their textures each frame.
    public void EnablePixelTransfer(bool enable = true) => _suspendTextureTransfer = !enable;

    // O-------------------------------------------------------------------O
    // | Drawing primitives (software, into the current draw target)       |
    // O-------------------------------------------------------------------O

    public void DrawLine(Vector2d<int> pos1, Vector2d<int> pos2, Pixel p, uint pattern = 0xFFFFFFFF)
        => DrawLine(pos1.X, pos1.Y, pos2.X, pos2.Y, p, pattern);

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

    // Cohen–Sutherland clip of a line segment to the draw target rectangle.
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

    public void DrawCircle(Vector2d<int> pos, int radius, Pixel p, byte mask = 0xFF)
        => DrawCircle(pos.X, pos.Y, radius, p, mask);

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

    public void FillCircle(Vector2d<int> pos, int radius, Pixel p) => FillCircle(pos.X, pos.Y, radius, p);

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

    public void DrawRect(Vector2d<int> pos, Vector2d<int> size, Pixel p) => DrawRect(pos.X, pos.Y, size.X, size.Y, p);

    public void DrawRect(int x, int y, int w, int h, Pixel p)
    {
        DrawLine(x, y, x + w, y, p);
        DrawLine(x + w, y, x + w, y + h, p);
        DrawLine(x + w, y + h, x, y + h, p);
        DrawLine(x, y + h, x, y, p);
    }

    public void FillRect(Vector2d<int> pos, Vector2d<int> size, Pixel p) => FillRect(pos.X, pos.Y, size.X, size.Y, p);

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

        for (var i = x; i < x2; i++)
            for (var j = y; j < y2; j++)
                Draw(i, j, p);
    }

    public void DrawTriangle(Vector2d<int> pos1, Vector2d<int> pos2, Vector2d<int> pos3, Pixel p)
        => DrawTriangle(pos1.X, pos1.Y, pos2.X, pos2.Y, pos3.X, pos3.Y, p);

    public void DrawTriangle(int x1, int y1, int x2, int y2, int x3, int y3, Pixel p)
    {
        DrawLine(x1, y1, x2, y2, p);
        DrawLine(x2, y2, x3, y3, p);
        DrawLine(x3, y3, x1, y1, p);
    }

    public void FillTriangle(Vector2d<int> pos1, Vector2d<int> pos2, Vector2d<int> pos3, Pixel p)
        => FillTriangle(pos1.X, pos1.Y, pos2.X, pos2.Y, pos3.X, pos3.Y, p);

    // Port of olc's scanline fill (triangles.c). The goto structure is preserved verbatim.
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

    public void DrawSprite(Vector2d<int> pos, Sprite sprite, int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
        => DrawSprite(pos.X, pos.Y, sprite, scale, flip);

    public void DrawSprite(int x, int y, Sprite sprite, int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        if (sprite == null) return;

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

    public void DrawPartialSprite(Vector2d<int> pos, Sprite sprite, Vector2d<int> sourcePos, Vector2d<int> size,
        int scale = 1, SpriteMirrorMode flip = SpriteMirrorMode.None)
        => DrawPartialSprite(pos.X, pos.Y, sprite, sourcePos.X, sourcePos.Y, size.X, size.Y, scale, flip);

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

    public void HW3D_Projection(float[] matrix) => _renderer.Set3DProjection(matrix);
    public void HW3D_EnableDepthTest(bool enable = true) => _hw3dDepthTest = enable;
    public void HW3D_SetCullMode(CullMode mode) => _hw3dCullMode = mode;

    // Draws a 3D mesh: each vertex is { x, y, z } (w forced to 1), uv { u, v }, plus a colour.
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

    public void HW3D_DrawLine(float[] matModelView, float[] pos1, float[] pos2, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var task = NextGpuTask();
        task.Decal = null;
        task.Mode = DecalMode.Wireframe;
        task.Structure = DecalStructure.Line;
        task.Depth = _hw3dDepthTest;
        task.Cull = _hw3dCullMode;
        task.Tint = Pixel.WHITE;
        Array.Copy(matModelView, task.Mvp, 16);
        task.Vb.Add(new Vertex(pos1[0], pos1[1], pos1[2], 1.0f, 0.0f, 0.0f, c.N));
        task.Vb.Add(new Vertex(pos2[0], pos2[1], pos2[2], 1.0f, 0.0f, 0.0f, c.N));
    }

    private static readonly int[] BoxEdges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };

    public void HW3D_DrawLineBox(float[] matModelView, float[] pos, float[] size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var task = NextGpuTask();
        task.Decal = null;
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

    // Software-rasterised, gouraud-shaded, optionally textured triangle (olc's port of
    // triangles.c). Colour and UV are linearly interpolated down each scanline.
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

    // Gathers three indexed vertices into reusable scratch and rasterises them (no per-triangle alloc).
    private void FillTexturedTriangleFrom(IReadOnlyList<Vector2d<float>> points, IReadOnlyList<Vector2d<float>> tex,
        IReadOnlyList<Pixel> colour, int a, int b, int c, Sprite sprTex)
    {
        _polyPos3[0] = points[a]; _polyPos3[1] = points[b]; _polyPos3[2] = points[c];
        _polyTex3[0] = tex[a]; _polyTex3[1] = tex[b]; _polyTex3[2] = tex[c];
        _polyCol3[0] = colour[a]; _polyCol3[1] = colour[b]; _polyCol3[2] = colour[c];
        FillTexturedTriangle(_polyPos3, _polyTex3, _polyCol3, sprTex);
    }

    private void FillPatchVerts(Vector2d<float> pos, Vector2d<float> s)
    {
        _scratchPts4[0] = new Vector2d<float>(pos.X, pos.Y + s.Y);
        _scratchPts4[1] = pos;
        _scratchPts4[2] = new Vector2d<float>(pos.X + s.X, pos.Y);
        _scratchPts4[3] = new Vector2d<float>(pos.X + s.X, pos.Y + s.Y);
    }

    public void DrawSprite(Vector2d<float> pos, SpritePatch patch, Vector2d<float>? scale = null)
    {
        FillPatchVerts(pos, scale ?? new Vector2d<float>(1, 1));
        FillTexturedPolygon(_scratchPts4, patch.Coords, WhiteQuad4, patch.Sprite, DecalStructure.Fan);
    }

    public void DrawDecal(Vector2d<float> pos, DecalPatch patch, Vector2d<float>? scale = null)
    {
        FillPatchVerts(pos, scale ?? new Vector2d<float>(1, 1));
        DrawPolygonDecal(patch.Decal, _scratchPts4, patch.Coordinates, WhiteQuad4);
    }

    // O-------------------------------------------------------------------O
    // | Text (embedded 8x8 font)                                          |
    // O-------------------------------------------------------------------O

    public Sprite GetFontSprite() => _fontRenderable.Sprite;

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

    public void DrawString(Vector2d<int> pos, string text, Pixel col, int scale = 1)
        => DrawString(pos.X, pos.Y, text, col, scale);

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

    public void DrawStringProp(Vector2d<int> pos, string text, Pixel col, int scale = 1)
        => DrawStringProp(pos.X, pos.Y, text, col, scale);

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

    // Reconstructs olc's embedded 8x8 font into a 128x48 sprite from base64-ish run data,
    // then derives per-glyph proportional spacing. Requires an active GL context (called from
    // PrepareEngine after CreateGraphics) because the Renderable allocates a texture.
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

    public void SetDecalMode(DecalMode mode) => _decalMode = mode;
    public void SetDecalStructure(DecalStructure structure) => _decalStructure = structure;

    // Pixel-space point -> normalised device coords [-1,1], with y flipped (olc's convention).
    private Vector2d<float> ScreenTransform(float x, float y)
        => new Vector2d<float>(x * InvScreenSize.X * 2.0f - 1.0f, (y * InvScreenSize.Y * 2.0f - 1.0f) * -1.0f);

    // Finalises a rented decal instance for this frame. The instance is already in the target
    // layer's pooled list (added by NextDecalInstance), so this only sets the current mode/structure.
    private void SubmitDecal(DecalInstance di)
    {
        di.Mode = _decalMode;
        di.Structure = _decalStructure;
    }

    // Rent a reusable DecalInstance from the current target layer's pool (clearing a recycled one,
    // or growing the pool by one). Returns a class instance the caller fills in place.
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

    // Per-vertex colours modulated by a single tint, then drawn as a textured polygon.
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv,
        IReadOnlyList<Pixel> colours, Pixel tint)
    {
        var newColours = new Pixel[colours.Count];
        for (var i = 0; i < colours.Count; i++) newColours[i] = colours[i] * tint;
        DrawPolygonDecal(decal, pos, uv, (IReadOnlyList<Pixel>)newColours);
    }

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

    public void DrawLineDecal(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null)
    {
        var col = p ?? Pixel.WHITE;
        var m = _decalMode;
        _decalMode = DecalMode.Wireframe;
        _scratchPts2[0] = pos1;
        _scratchPts2[1] = pos2;
        DrawPolygonDecal(null, _scratchPts2, ZeroUv4, col);
        _decalMode = m;
    }

    private void FillScratchQuad(Vector2d<float> pos, Vector2d<float> size)
    {
        _scratchPts4[0] = pos;
        _scratchPts4[1] = new Vector2d<float>(pos.X, pos.Y + size.Y);
        _scratchPts4[2] = new Vector2d<float>(pos.X + size.X, pos.Y + size.Y);
        _scratchPts4[3] = new Vector2d<float>(pos.X + size.X, pos.Y);
    }

    public void DrawRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        var m = _decalMode;
        SetDecalMode(DecalMode.Wireframe);
        FillScratchQuad(pos, size);
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = _scratchCol4[3] = c;
        DrawExplicitDecal(null, _scratchPts4, ZeroUv4, _scratchCol4);
        SetDecalMode(m);
    }

    public void FillRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        FillScratchQuad(pos, size);
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = _scratchCol4[3] = c;
        DrawExplicitDecal(null, _scratchPts4, ZeroUv4, _scratchCol4);
    }

    public void GradientFillRectDecal(Vector2d<float> pos, Vector2d<float> size,
        Pixel colTL, Pixel colBL, Pixel colBR, Pixel colTR)
    {
        FillScratchQuad(pos, size);
        _scratchCol4[0] = colTL; _scratchCol4[1] = colBL; _scratchCol4[2] = colBR; _scratchCol4[3] = colTR;
        DrawExplicitDecal(null, _scratchPts4, ZeroUv4, _scratchCol4);
    }

    public void FillTriangleDecal(Vector2d<float> p0, Vector2d<float> p1, Vector2d<float> p2, Pixel? col = null)
    {
        var c = col ?? Pixel.WHITE;
        _scratchPts4[0] = p0; _scratchPts4[1] = p1; _scratchPts4[2] = p2;
        _scratchCol4[0] = _scratchCol4[1] = _scratchCol4[2] = c;
        DrawExplicitDecal(null, _scratchPts4, ZeroUv4, _scratchCol4, 3);
    }

    public void GradientTriangleDecal(Vector2d<float> p0, Vector2d<float> p1, Vector2d<float> p2,
        Pixel c0, Pixel c1, Pixel c2)
    {
        _scratchPts4[0] = p0; _scratchPts4[1] = p1; _scratchPts4[2] = p2;
        _scratchCol4[0] = c0; _scratchCol4[1] = c1; _scratchCol4[2] = c2;
        DrawExplicitDecal(null, _scratchPts4, ZeroUv4, _scratchCol4, 3);
    }

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

    public void DrawWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Pixel? tint = null)
        => DrawPartialWarpedDecal(decal, pos, new Vector2d<float>(0, 0), new Vector2d<float>(1, 1), tint, useFullUv: true);

    public void DrawPartialWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
        => DrawPartialWarpedDecal(decal, pos, sourcePos, sourceSize, tint, useFullUv: false);

    // Shared warp implementation (Nathan Reed's projective quad interpolation). useFullUv selects
    // the 0..1 unit UVs (DrawWarpedDecal) vs a source sub-rect (DrawPartialWarpedDecal).
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

    private static readonly Vector2d<float>[] ZeroUv4 =
    {
        new(0, 0), new(0, 0), new(0, 0), new(0, 0)
    };

    // O-------------------------------------------------------------------O
    // | Screen / viewport sizing                                          |
    // O-------------------------------------------------------------------O

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

    private void UpdateWindowSize(int x, int y)
    {
        WindowSize = new Vector2d<int>(x, y);
        if (RealWindowMode)
            _resizeRequested = true; // handled after DisplayFrame in CoreUpdate
        UpdateViewport();
    }

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
