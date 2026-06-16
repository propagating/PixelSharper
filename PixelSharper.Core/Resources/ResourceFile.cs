namespace PixelSharper.Core.Resources;

/// <summary>A packed file's location within a ResourcePack: its byte size and stream offset.</summary>
/// <seealso cref="ResourcePack"/>
public struct ResourceFile
{
     /// <summary>Length of the file's data in bytes.</summary>
     public int ResourceSize;
     /// <summary>Byte offset of the file's data within the pack stream.</summary>
     public int ResourceOffset;

     /// <summary>Creates an entry of the given size with a zero (not-yet-assigned) offset.</summary>
     /// <param name="size">Length of the file's data in bytes.</param>
     public ResourceFile(int size) : this()
     {
          ResourceSize = size;
          ResourceOffset = 0;
     }
     /// <summary>Creates an entry with an explicit size and stream offset.</summary>
     /// <param name="size">Length of the file's data in bytes.</param>
     /// <param name="resourceOffset">Byte offset of the file's data within the pack stream.</param>
     public ResourceFile(int size, int resourceOffset) : this()
     {
          ResourceSize = size;
          ResourceOffset = resourceOffset;
     }
}
