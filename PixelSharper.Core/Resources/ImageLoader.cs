using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Resources;

/// <summary>
/// Abstract image codec seam (port of olc::ImageLoader): subclasses load/save Sprite pixel
/// data from files or resource packs. No concrete backend yet, so the base returns NoFile.
/// </summary>
/// <remarks>
/// <para>
/// This is the dependency-injection seam used by <see cref="Sprite"/>: a concrete subclass is
/// assigned to the static loader field and replaced with a mock in tests.
/// </para>
/// </remarks>
/// <seealso cref="Sprite"/>
/// <seealso cref="ResourcePack"/>
public class ImageLoader
{
    /// <summary>Creates the loader.</summary>
    public ImageLoader()
    {

    }

    /// <summary>Loads pixels into the sprite from a file or pack; base does nothing.</summary>
    /// <param name="sprite">The sprite to populate with the decoded pixel data.</param>
    /// <param name="fileName">Path of the image to load (within <paramref name="resourcePack"/> when supplied).</param>
    /// <param name="resourcePack">Optional pack to read the image from, or null to read from disk.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating the outcome; the base always returns <see cref="FileReadCode.NoFile"/>.</returns>
    public virtual FileReadCode LoadImageResource(Sprite sprite, string fileName, ResourcePack resourcePack)
    {
        return FileReadCode.NoFile;
    }

    /// <summary>Saves the sprite's pixels to a file; base does nothing.</summary>
    /// <param name="sprite">The sprite whose pixels are to be encoded.</param>
    /// <param name="fileName">Destination path for the encoded image.</param>
    /// <returns>A <see cref="FileReadCode"/> indicating the outcome; the base always returns <see cref="FileReadCode.NoFile"/>.</returns>
    public virtual FileReadCode SaveImageResource(Sprite sprite, string fileName)
    {
        return FileReadCode.NoFile;
    }

    /// <summary>Finalizer; nothing to release in the base.</summary>
    ~ImageLoader()
    {

    }
}