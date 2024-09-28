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
        
    }

    public Decal(uint existingTextureResource, Sprite sprite)
    {
        
    }

    public void Update()
    {
        
    }

    public void UpdateSprite()
    {
        
    }

    ~Decal()
    {
        
    }
}