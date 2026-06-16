namespace PixelSharper.Core.Extensions;

/// <summary>Base class for PixelGameEngine extensions (olc::PGEX). Construct with hook:true to auto-register the extension so the engine invokes its OnBefore/After hooks around the user's create/update.</summary>
/// <remarks>
/// <para>Extensions subclass <see cref="PGEX"/> and override the lifecycle hooks. The engine sets <see cref="Pge"/> when it is constructed, keeps a list of registered extensions, and calls the hooks in this order each lifecycle phase:</para>
/// <para><see cref="OnBeforeUserCreate"/> then the user's create then <see cref="OnAfterUserCreate"/>; and per frame <see cref="OnBeforeUserUpdate"/> then (unless blocked) the user's update then <see cref="OnAfterUserUpdate"/>.</para>
/// <para>A hooked extension is registered automatically; an unhooked one must be driven manually by the user.</para>
/// </remarks>
public abstract class PGEX
{
    /// <summary>The single active engine instance (olc::PGEX::pge), set when a PixelGameEngine is constructed.</summary>
    /// <value>The owning engine, shared by all extensions.</value>
    protected internal static PixelGameEngine Pge;

    /// <summary>Creates the extension; when hook is true, registers it with the engine for lifecycle callbacks.</summary>
    /// <param name="hook">When <c>true</c>, registers this extension with <see cref="Pge"/> so its lifecycle hooks are invoked automatically.</param>
    protected PGEX(bool hook = false)
    {
        if (hook)
            Pge?.RegisterExtension(this);
    }

    /// <summary>Called by the engine before the user's OnCreate.</summary>
    /// <seealso cref="OnAfterUserCreate"/>
    protected internal virtual void OnBeforeUserCreate() { }
    /// <summary>Called by the engine after the user's OnCreate.</summary>
    /// <seealso cref="OnBeforeUserCreate"/>
    protected internal virtual void OnAfterUserCreate() { }
    /// <summary>Called before the user's OnUpdate; return true to BLOCK the user's OnUpdate this frame (e.g. while a splash screen plays).</summary>
    /// <param name="elapsedTime">The frame's elapsed time in seconds; passed by reference so the hook can adjust the time the user frame sees.</param>
    /// <returns><c>true</c> to block the user's OnUpdate this frame; <c>false</c> to let it run.</returns>
    /// <seealso cref="OnAfterUserUpdate"/>
    protected internal virtual bool OnBeforeUserUpdate(ref float elapsedTime) => false;
    /// <summary>Called by the engine after the user's OnUpdate.</summary>
    /// <param name="elapsedTime">The frame's elapsed time in seconds.</param>
    /// <seealso cref="OnBeforeUserUpdate"/>
    protected internal virtual void OnAfterUserUpdate(float elapsedTime) { }
}
