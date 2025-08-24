using System;
using System.IO;
using System.Drawing;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Resources;

public class ImageLoader
{
    public ImageLoader()
    {
        
    }

    public virtual FileReadCode LoadImageResource(Sprite sprite, string fileName, ResourcePack resourcePack)
    {
        return FileReadCode.NO_FILE;
    }

    public virtual FileReadCode SaveImageResource(Sprite sprite, string fileName)
    {
        return FileReadCode.NO_FILE;
    }
    
    ~ImageLoader()
    {
        
    }
}