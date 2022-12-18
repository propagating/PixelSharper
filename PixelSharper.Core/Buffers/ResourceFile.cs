namespace PixelSharper.Core.Buffers;

public struct ResourceFile
{
     public uint ResourceSize;
     public uint ResourceOffset;

     public ResourceFile(uint size) : this()
     {
          ResourceSize = size;
          ResourceOffset = 0;
     }
     public ResourceFile(uint size, uint resourceOffset) : this()
     {
          ResourceSize = size;
          ResourceOffset = 0;
     }
}
