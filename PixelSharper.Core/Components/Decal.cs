using PixelSharper.Core.Types;

namespace PixelSharper.Core.Components;

public class Decal
{
    public int Id { get; set; }
    public Sprite Sprite { get; set; }
    public Vector2d<float> UVScale { get; set; }
    
    public Decal()
    {
        Id = -1;
        Sprite = null;
        UVScale = new Vector2d<float>(1.0f, 1.0f);
    }
    
    public Decal(Sprite sprite, bool filter = false, bool clamp = true)
    {
        if (sprite == null) return;
        Sprite = sprite;
        Id = Renderer.Active.CreateTexture(sprite.Size(), filter, clamp);
        Update();
    }

    public Decal(int existingTextureResource, Sprite sprite)
    {
        if (sprite == null) return;
        Id = existingTextureResource;
    }

    public void Update()
    {
        if(Sprite == null) return;
        UVScale = new Vector2d<float>(1.0f, 1.0f) / Sprite.Size().As<float>();
        Renderer.Active.ApplyTexture((uint)Id);
        Renderer.Active.UpdateTexture((uint)Id, Sprite);
    }

    public void UpdateSprite()
    {
        Renderer.Active.ApplyTexture((uint)Id);
        Renderer.Active.ReadTexture((uint)Id, Sprite);
    }
    public DecalPatch ToDecalPatch()
    {
        return Patch(new Vector2d<int>(0, 0), this.Sprite.Size());
    }

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

    ~Decal()
    {
        if (Id != -1)
        {
            Renderer.Active.DeleteTexture((uint)Id);
            Id = -1;
        }
    }
}

