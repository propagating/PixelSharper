namespace PixelSharper.Core.Buffers;

public struct ResourceBuffer
{
    public byte[] StreamBuffer { get; set; }
    public ResourceBuffer(FileStream fileStream, int dataOffset, int dataSize)
    {
        StreamBuffer = new byte[dataSize];
        
        //TODO: Ensure bytes read is equal to the data size specified.
        var bytesRead = fileStream.Read(StreamBuffer, dataOffset, dataSize);

    }
    
}
