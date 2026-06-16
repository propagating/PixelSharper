using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

/// <summary>A GPU texture handle wrapping a Sprite — olc::Decal; uploaded to and drawn by the renderer.</summary>
/// <seealso cref="Sprite"/>
/// <seealso cref="Renderer"/>
public class Decal
{
    /// <summary>Renderer texture id, or -1 when none is allocated.</summary>
    /// <value>The GL texture id, or <c>-1</c> when no texture is held.</value>
    public int Id { get; set; }
    /// <summary>The source sprite whose pixels back this texture.</summary>
    /// <value>The backing <see cref="Sprite"/>, or <c>null</c> for an empty decal.</value>
    public Sprite Sprite { get; set; }
    /// <summary>Reciprocal of sprite size; scales pixel coords to 0..1 UV space.</summary>
    /// <value>The per-axis reciprocal of the sprite size, mapping pixel coordinates to 0..1 UVs.</value>
    public Vector2d<float> UVScale { get; set; }

    /// <summary>Creates an empty decal with no texture (Id = -1).</summary>
    public Decal()
    {
        Id = -1;
        Sprite = null;
        UVScale = new Vector2d<float>(1.0f, 1.0f);
    }

    /// <summary>Creates a texture from a sprite and uploads it (filter = linear sampling, clamp = edge clamp).</summary>
    /// <param name="sprite">The source sprite to upload; a <c>null</c> sprite leaves the decal empty.</param>
    /// <param name="filter">When <c>true</c> the texture uses linear sampling; otherwise nearest-neighbour.</param>
    /// <param name="clamp">When <c>true</c> the texture clamps to its edge; otherwise it repeats.</param>
    public Decal(Sprite sprite, bool filter = false, bool clamp = true)
    {
        if (sprite == null) return;
        Sprite = sprite;
        Id = Renderer.Active.CreateTexture(sprite.Size(), filter, clamp);
        Update();
    }

    /// <summary>Wraps an already-created renderer texture id without uploading.</summary>
    /// <param name="existingTextureResource">An existing renderer texture id to adopt.</param>
    /// <param name="sprite">The associated sprite; a <c>null</c> sprite leaves the decal empty.</param>
    public Decal(int existingTextureResource, Sprite sprite)
    {
        if (sprite == null) return;
        Id = existingTextureResource;
    }

    /// <summary>Recomputes UVScale and re-uploads the sprite's pixels to the GPU texture.</summary>
    /// <seealso cref="UpdateSprite"/>
    public void Update()
    {
        if(Sprite == null) return;
        UVScale = new Vector2d<float>(1.0f, 1.0f) / Sprite.Size().As<float>();
        Renderer.Active.ApplyTexture((uint)Id);
        Renderer.Active.UpdateTexture((uint)Id, Sprite);
    }

    /// <summary>Reads the GPU texture back into the backing sprite (reverse of Update).</summary>
    /// <seealso cref="Update"/>
    public void UpdateSprite()
    {
        Renderer.Active.ApplyTexture((uint)Id);
        Renderer.Active.ReadTexture((uint)Id, Sprite);
    }
    /// <summary>Returns a patch covering the whole decal (olc's implicit Decal-to-patch conversion).</summary>
    /// <returns>A <see cref="DecalPatch"/> whose quad spans the entire decal.</returns>
    /// <seealso cref="Patch(Vector2d{int}, Vector2d{int})"/>
    public DecalPatch ToDecalPatch()
    {
        return Patch(new Vector2d<int>(0, 0), this.Sprite.Size());
    }

    /// <summary>Builds a quad patch over a pixel sub-rectangle, with UVs normalised to sprite size.</summary>
    /// <param name="pos">Top-left pixel of the sub-rectangle.</param>
    /// <param name="size">Width and height of the sub-rectangle in pixels.</param>
    /// <returns>A <see cref="DecalPatch"/> with the four corner UVs of the sub-rectangle normalised to sprite size.</returns>
    /// <seealso cref="Patch(Vector2d{float}, Vector2d{float}, Vector2d{float}, Vector2d{float})"/>
    public DecalPatch Patch(Vector2d<int> pos, Vector2d<int> size)
    {
        var patch = new DecalPatch();
        patch.Decal = this;
        var sizeF = this.Sprite.Size().As<float>();
        patch.Coordinates[0] = new Vector2d<float>(pos.X, pos.Y + size.Y) / sizeF;
        patch.Coordinates[1] = new Vector2d<float>(pos.X, pos.Y) / sizeF;
        patch.Coordinates[2] = new Vector2d<float>(pos.X + size.X, pos.Y) / sizeF;
        patch.Coordinates[3] = new Vector2d<float>(pos.X + size.X, pos.Y + size.Y) / sizeF;
        return patch;
    }

    /// <summary>Builds a patch from four explicit corner UVs (bottom-left, top-left, top-right, bottom-right).</summary>
    /// <param name="pBL">Bottom-left corner UV.</param>
    /// <param name="pTL">Top-left corner UV.</param>
    /// <param name="pTR">Top-right corner UV.</param>
    /// <param name="pBR">Bottom-right corner UV.</param>
    /// <returns>A <see cref="DecalPatch"/> with the four explicit corner UVs.</returns>
    /// <seealso cref="Patch(Vector2d{int}, Vector2d{int})"/>
    public DecalPatch Patch(Vector2d<float> pBL, Vector2d<float> pTL, Vector2d<float> pTR, Vector2d<float> pBR)
    {
        var patch = new DecalPatch();
        patch.Decal = this;
        patch.Coordinates[0] = pBL;
        patch.Coordinates[1] = pTL;
        patch.Coordinates[2] = pTR;
        patch.Coordinates[3] = pBR;
        return patch;
    }

    /// <summary>Finalizer; deletes the GPU texture if one is held.</summary>
    ~Decal()
    {
        if (Id != -1)
        {
            Renderer.Active.DeleteTexture((uint)Id);
            Id = -1;
        }
    }
}

