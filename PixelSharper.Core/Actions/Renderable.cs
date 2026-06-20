using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;

namespace PixelSharper.Core.Actions;


/// <summary>Pairs a CPU Sprite with its GPU Decal so an asset can be drawn either way. Port of olc::Renderable.</summary>
/// <seealso cref="Sprite"/>
/// <seealso cref="Decal"/>
public class Renderable
{
    /// <summary>GPU-side decal (texture) for this asset.</summary>
    /// <value>The <see cref="Decal"/> uploaded from <see cref="Sprite"/>.</value>
    public Decal Decal { get; set; }
    /// <summary>CPU-side pixel sprite for this asset.</summary>
    /// <value>The source <see cref="Sprite"/> pixel data.</value>
    public Sprite Sprite { get; set; }

    /// <summary>Creates an empty renderable with a blank sprite and decal.</summary>
    public Renderable()
    {
        Sprite = new Sprite();
        Decal = new Decal();
    }


    /// <summary>Allocates a blank sprite of the given size and a matching decal (filter/clamp options).</summary>
    /// <param name="width">Width of the new sprite in pixels.</param>
    /// <param name="height">Height of the new sprite in pixels.</param>
    /// <param name="filter">When true, the decal samples with linear filtering rather than nearest.</param>
    /// <param name="clamp">When true, the decal clamps texture coordinates to its edges rather than wrapping.</param>
    public void Create(uint width, uint height, bool filter, bool clamp)
    {
        Sprite = new Sprite((int)width, (int)height);
        Decal = new Decal(Sprite, filter, clamp);
    }

    /// <summary>Loads the sprite from a file/pack and, on success, builds its decal; clears the sprite on failure.</summary>
    /// <param name="file">Path of the image to load (within <paramref name="pack"/> when supplied).</param>
    /// <param name="pack">Optional resource pack to read from, or null to read from disk.</param>
    /// <param name="filter">When true, the decal samples with linear filtering rather than nearest.</param>
    /// <param name="clamp">When true, the decal clamps texture coordinates to its edges rather than wrapping.</param>
    /// <returns><see cref="FileReadCode.Ok"/> when the sprite loaded and its decal was built; otherwise <see cref="FileReadCode.NoFile"/> (the sprite is cleared).</returns>
    public FileReadCode Load(string file, ResourcePack pack, bool filter, bool clamp)
    {
        Sprite = new Sprite();
        if (Sprite.LoadFromFile(file, pack) == FileReadCode.Ok)
        {
            Decal = new Decal(Sprite, filter, clamp);
            return FileReadCode.Ok;
        }
        else
        {
            Sprite = null!;
            return FileReadCode.NoFile;
        }
    }
}