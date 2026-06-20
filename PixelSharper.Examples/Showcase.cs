using PixelSharper.Core;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Examples;

/// <summary>One page of the showcase: a self-contained tutorial that lazily builds its resources and draws itself each frame within the canvas between the header and footer.</summary>
/// <remarks>Implemented once per demonstrated feature; instances are listed in <c>Program.cs</c> and paged through by <see cref="Showcase"/>.</remarks>
/// <seealso cref="Showcase"/>
public interface IExampleScene
{
    /// <summary>The scene's display name, shown in the showcase header.</summary>
    /// <value>A short human-readable title, e.g. <c>"Welcome"</c>.</value>
    string Title { get; }
    /// <summary>Build any resources the scene needs; called once, lazily, on first view.</summary>
    /// <param name="e">The host showcase engine, used to query screen/canvas metrics and create engine resources.</param>
    void Initialise(Showcase e);
    /// <summary>Run and draw the scene for one frame.</summary>
    /// <param name="e">The host showcase engine, used to draw and poll input.</param>
    /// <param name="elapsedTime">Seconds elapsed since the previous frame, for time-based animation.</param>
    void Update(Showcase e, float elapsedTime);
}

/// <summary>The navigable showcase engine: LEFT/RIGHT (or SPACE) cycles scenes and ESC quits, with a title/index header and a controls footer framing each scene's canvas.</summary>
/// <remarks>Subclasses <see cref="PixelGameEngine"/>; each frame it dispatches to the current <see cref="IExampleScene"/> and draws the header/footer chrome.</remarks>
/// <seealso cref="IExampleScene"/>
public class Showcase : PixelGameEngine
{
    /// <summary>Height in pixels of the title/index header strip at the top of the screen.</summary>
    public const int HeaderHeight = 11;
    /// <summary>Height in pixels of the controls footer strip at the bottom of the screen.</summary>
    public const int FooterHeight = 10;

    /// <summary>The ordered list of scenes to page through.</summary>
    private readonly List<IExampleScene> _scenes;
    /// <summary>Index of the currently displayed scene.</summary>
    private int _current;
    /// <summary>Whether the current scene's Initialise() has run yet.</summary>
    private bool _initialised;

    /// <summary>When set (--autotest), auto-advance through every scene then exit, smoke-testing that each scene runs without throwing.</summary>
    private readonly bool _autoAdvance;
    /// <summary>Seconds each scene is shown before auto-advancing in auto-test mode.</summary>
    private const float AutoInterval = 0.3f;
    /// <summary>Time accumulated towards the next auto-advance.</summary>
    private float _autoTimer;
    /// <summary>Count of auto-advances performed; used to stop after every scene is visited.</summary>
    private int _advances;

    /// <summary>Builds the showcase over the given scenes, optionally in auto-test mode.</summary>
    /// <param name="scenes">The ordered scenes to page through; materialised into the internal list.</param>
    /// <param name="autoAdvance">When <c>true</c> (the <c>--autotest</c> mode), auto-advances through every scene then exits, smoke-testing each scene; defaults to <c>false</c>.</param>
    public Showcase(IEnumerable<IExampleScene> scenes, bool autoAdvance = false)
    {
        ApplicationName = "PixelSharper Showcase";
        _scenes = scenes.ToList();
        _autoAdvance = autoAdvance;
    }

    /// <summary>First y coordinate a scene should draw within (just below the header).</summary>
    /// <value>The y coordinate one pixel below the header strip.</value>
    public int CanvasTop => HeaderHeight + 1;
    /// <summary>Last y coordinate a scene should draw within (just above the footer).</summary>
    /// <value>The y coordinate one pixel above the footer strip.</value>
    public int CanvasBottom => ScreenHeight() - FooterHeight - 1;
    /// <summary>Height in pixels of the scene canvas between header and footer.</summary>
    /// <value>The pixel distance from <see cref="CanvasTop"/> to <see cref="CanvasBottom"/>.</value>
    public int CanvasHeight => CanvasBottom - CanvasTop;

    /// <summary>Engine create hook; no global setup needed.</summary>
    /// <returns>Always <c>true</c>, signalling successful creation so the engine starts.</returns>
    public override bool OnCreate() => true;

    /// <summary>Handles navigation/auto-advance, then initialises and draws the current scene plus the header/footer chrome.</summary>
    /// <param name="elapsedTime">Seconds elapsed since the previous frame; forwarded to the active scene and used to time auto-advance.</param>
    /// <returns><c>false</c> when ESC is pressed or auto-test has visited every scene (requesting shutdown); otherwise <c>true</c>.</returns>
    public override bool OnUpdate(float elapsedTime)
    {
        if (GetKey(KeyPress.Escape).Pressed) return false;

        if (GetKey(KeyPress.Right).Pressed || GetKey(KeyPress.Space).Pressed) Switch(+1);
        if (GetKey(KeyPress.Left).Pressed) Switch(-1);

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

    /// <summary>Advances the current scene by the given direction (wrapping), resets it for re-init, and disables any active text entry.</summary>
    /// <param name="dir">Step to apply to the current index, e.g. <c>+1</c> for the next scene or <c>-1</c> for the previous; wraps around the scene count.</param>
    private void Switch(int dir)
    {
        _current = (_current + dir + _scenes.Count) % _scenes.Count;
        _initialised = false;
        // Make sure an interactive scene (e.g. text entry) doesn't keep capturing keys after we leave.
        TextEntryEnable(false);
    }
}
