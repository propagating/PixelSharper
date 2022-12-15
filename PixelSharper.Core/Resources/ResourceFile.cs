namespace PixelSharper.Core.Resources;

public struct ResourceFile
{
     public int ResourceSize;
     public int ResourceOffset;

     public ResourceFile(int size) : this()
     {
          ResourceSize = size;
          ResourceOffset = 0;
     }
     public ResourceFile(int size, int resourceOffset) : this()
     {
          ResourceSize = size;
          ResourceOffset = resourceOffset;
     }
}
