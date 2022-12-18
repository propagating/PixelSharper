namespace PixelSharper.Core.Resources;

public struct ResourceBuffer
{
    public byte[] Buffer { get; set; }
    public ResourceBuffer(Stream memStream, int dataOffset, int dataSize)
    {
        Buffer = new byte[dataSize];
        memStream.Seek(dataOffset, SeekOrigin.Begin);
        memStream.ReadExactly(Buffer, 0, dataSize);
    }
    
}
