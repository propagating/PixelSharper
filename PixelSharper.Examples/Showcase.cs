using PixelSharper.Core;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Examples;

// One page of the showcase. Each scene is a self-contained tutorial: Initialise() builds whatever
// resources it needs (once, lazily on first view), and Update() runs + draws it each frame. The
// drawable area is the whole screen except a thin header (top) and footer (bottom).
public interface IExampleScene
{
    string Title { get; }
    void Initialise(Showcase e);
    void Update(Showcase e, float elapsedTime);
}

// A navigable showcase: LEFT/RIGHT (or SPACE) cycles scenes, ESC quits. It draws a header with the
// scene title + index and a footer with the controls; everything between is the scene's canvas.
public class Showcase : PixelGameEngine
{
    public const int HeaderHeight = 11;
    public const int FooterHeight = 10;

    private readonly List<IExampleScene> _scenes;
    private int _current;
    private bool _initialised;

    // Auto-test mode (--autotest): cycle through every scene quickly, then exit. Used to verify each
    // scene's Initialise()/Update() runs without throwing (the alternative to manual navigation).
    private readonly bool _autoAdvance;
    private const float AutoInterval = 0.3f;
    private float _autoTimer;
    private int _advances;

    public Showcase(IEnumerable<IExampleScene> scenes, bool autoAdvance = false)
    {
        ApplicationName = "PixelSharper Showcase";
        _scenes = scenes.ToList();
        _autoAdvance = autoAdvance;
    }

    // Convenience: the y range a scene should draw within.
    public int CanvasTop => HeaderHeight + 1;
    public int CanvasBottom => ScreenHeight() - FooterHeight - 1;
    public int CanvasHeight => CanvasBottom - CanvasTop;

    public override bool OnCreate() => true;

    public override bool OnUpdate(float elapsedTime)
    {
        if (GetKey(KeyPress.ESCAPE).Pressed) return false;

        if (GetKey(KeyPress.RIGHT).Pressed || GetKey(KeyPress.SPACE).Pressed) Switch(+1);
        if (GetKey(KeyPress.LEFT).Pressed) Switch(-1);

        if (_autoAdvance)
        {
            _autoTimer += elapsedTime;
            if (_autoTimer >= AutoInterval)
            {
                _autoTimer = 0;
                Switch(+1);
                if (++_advances >= _scenes.Count) return false; // visited every scene — done
            }
        }

        var scene = _scenes[_current];
        if (!_initialised) { scene.Initialise(this); _initialised = true; }

        // Reset shared engine state each frame so scenes don't leak modes into one another.
        SetPixelMode(PixelDisplayMode.Normal);
        SetDecalMode(DecalMode.Normal);

        Clear(new Pixel(18, 18, 28));
        scene.Update(this, elapsedTime);

        // Header + footer chrome, drawn over the scene (reset modes a scene may have changed).
        SetPixelMode(PixelDisplayMode.Normal);
        FillRect(0, 0, ScreenWidth(), HeaderHeight, new Pixel(10, 10, 40));
        DrawString(2, 2, $"[{_current + 1}/{_scenes.Count}] {scene.Title}", Pixel.YELLOW);
        FillRect(0, ScreenHeight() - FooterHeight, ScreenWidth(), FooterHeight, new Pixel(10, 10, 40));
        DrawString(2, ScreenHeight() - 8, "<- ->  switch scene      ESC  quit", Pixel.GREY);
        return true;
    }

    private void Switch(int dir)
    {
        _current = (_current + dir + _scenes.Count) % _scenes.Count;
        _initialised = false;
        // Make sure an interactive scene (e.g. text entry) doesn't keep capturing keys after we leave.
        TextEntryEnable(false);
    }
}
