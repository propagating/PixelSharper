namespace PixelSharper.Core.Extensions;

// Base class for PixelGameEngine extensions (olc::PGEX). Construct with hook:true to auto-register
// the extension so the engine invokes its OnBefore/After hooks around the user's create/update.
public abstract class PGEX
{
    // The single active engine instance (olc::PGEX::pge), set when a PixelGameEngine is constructed.
    protected internal static PixelGameEngine Pge;

    protected PGEX(bool hook = false)
    {
        if (hook)
            Pge?.RegisterExtension(this);
    }

    protected internal virtual void OnBeforeUserCreate() { }
    protected internal virtual void OnAfterUserCreate() { }
    // Return true to BLOCK the user's OnUpdate this frame (e.g. while a splash screen plays).
    protected internal virtual bool OnBeforeUserUpdate(ref float elapsedTime) => false;
    protected internal virtual void OnAfterUserUpdate(float elapsedTime) { }
}
