using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

/// <summary>
/// Abstract OS windowing / event-loop device, the sibling of <see cref="Renderer"/>.
/// Port of olc::Platform.
/// </summary>
/// <remarks>
/// <para>
/// Concrete implementations (e.g. <c>PlatformOpenTK</c>) own the native window and feed the
/// GL context to the active <see cref="Renderer"/> via <see cref="CreateGraphics"/>. This is
/// the seam that decouples the engine from any specific OS / windowing toolkit.
/// </para>
/// <para>
/// olc returns <c>olc::rcode</c> from these calls; the C# port returns <see cref="FileReadCode"/>.
/// olc splits the OS event loop (main thread) from the GL context (engine thread); because GLFW is
/// single-thread-friendly the port collapses to one thread — the engine loop pumps events via
/// <see cref="HandleSystemEvent"/> each frame and <see cref="StartSystemEventLoop"/> is a no-op.
/// </para>
/// </remarks>
public abstract class Platform
{
    /// <summary>
    /// The engine this platform serves. Mirrors olc::Platform::ptrPGE; set during engine construction.
    /// </summary>
    public static PixelGameEngine PtrPGE;

    /// <summary>Performs one-time application start-up for the platform layer.</summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode ApplicationStartUp();

    /// <summary>Performs one-time application clean-up for the platform layer.</summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode ApplicationCleanUp();

    /// <summary>Performs per-thread start-up (engine-thread context setup).</summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode ThreadStartUp();

    /// <summary>Performs per-thread clean-up (engine-thread teardown).</summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode ThreadCleanUp();

    /// <summary>
    /// Creates the graphics context and hands it to the active <see cref="Renderer"/> for device creation.
    /// </summary>
    /// <param name="fullScreen">Whether to create the context for a full-screen surface.</param>
    /// <param name="enableVsync">Whether vertical sync should be enabled.</param>
    /// <param name="viewPos">The letterbox viewport top-left position, in window pixels.</param>
    /// <param name="viewSize">The letterbox viewport size, in window pixels.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode CreateGraphics(bool fullScreen, bool enableVsync, Vector2d<int> viewPos, Vector2d<int> viewSize);

    /// <summary>Creates the native window pane the engine renders into.</summary>
    /// <param name="windowPos">The window top-left position, in screen pixels.</param>
    /// <param name="windowSize">The window size, in screen pixels.</param>
    /// <param name="fullScreen">Whether the window should be created full-screen.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode CreateWindowPane(Vector2d<int> windowPos, Vector2d<int> windowSize, bool fullScreen);

    /// <summary>Sets the native window title bar text.</summary>
    /// <param name="title">The title text to display.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode SetWindowTitle(string title);

    /// <summary>Shows or hides the native window frame / decorations.</summary>
    /// <param name="showFrame">When <c>true</c> the window frame is shown; when <c>false</c> it is hidden.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode ShowWindowFrame(bool showFrame = true);

    /// <summary>Repositions and resizes the native window.</summary>
    /// <param name="windowPos">The new window top-left position, in screen pixels.</param>
    /// <param name="windowSize">The new window size, in screen pixels.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode SetWindowSize(Vector2d<int> windowPos, Vector2d<int> windowSize);

    /// <summary>
    /// Starts the OS event loop. In the single-thread collapse this is typically a no-op; events are
    /// pumped per frame via <see cref="HandleSystemEvent"/> instead.
    /// </summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode StartSystemEventLoop();

    /// <summary>Pumps pending OS / window events once. Called each frame by the engine loop.</summary>
    /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
    public abstract FileReadCode HandleSystemEvent();
}
