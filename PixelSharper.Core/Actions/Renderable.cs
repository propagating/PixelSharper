using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;

namespace PixelSharper.Core.Actions;


public class Renderable
{
    public Decal Decal { get; set; }
    public Sprite Sprite { get; set; }

    public Renderable()
    {
        Sprite = new Sprite();
        Decal = new Decal();;
    }
    
    
    public void Create(uint width, uint height, bool filter, bool clamp)
    {
        Sprite = new Sprite((int)width, (int)height);
        Decal = new Decal(Sprite, filter, clamp);
    }

    public FileReadCode Load(string file, ResourcePack pack, bool filter, bool clamp)
    {
        Sprite = new Sprite();
        if (Sprite.LoadFromFile(file, pack) == FileReadCode.OK)
        {
            Decal = new Decal(Sprite, filter, clamp);
            return FileReadCode.OK;
        }
        else
        {
            Sprite = null;
            return FileReadCode.NO_FILE;
        }
    }
}