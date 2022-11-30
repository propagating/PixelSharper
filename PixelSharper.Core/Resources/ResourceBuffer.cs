namespace PixelSharper.Core.Resources;

public struct ResourceBuffer
{
    public byte[] Buffer { get; set; }
    public ResourceBuffer(Stream fileStream, int dataOffset, int dataSize)
    {
        Buffer = new byte[dataSize];
        fileStream.ReadExactly(Buffer, dataOffset, dataSize);
    }
    
}
