namespace PixelSharper.Core.Resources;

/// <summary>An in-memory copy of one packed file's bytes, extracted from a pack stream. Port of olc::ResourceBuffer.</summary>
/// <seealso cref="ResourcePack"/>
/// <seealso cref="ResourceFile"/>
public struct ResourceBuffer
{
    /// <summary>The extracted file bytes.</summary>
    /// <value>A byte array sized to the packed file's data length.</value>
    public byte[] Buffer { get; set; }
    /// <summary>Seeks to the file's offset in the stream and reads its bytes into the buffer.</summary>
    /// <param name="memStream">The loaded pack stream to read from.</param>
    /// <param name="dataOffset">Byte offset of the file's data within <paramref name="memStream"/>.</param>
    /// <param name="dataSize">Number of bytes to read into <see cref="Buffer"/>.</param>
    /// <exception cref="System.IO.EndOfStreamException">The stream ends before <paramref name="dataSize"/> bytes are read.</exception>
    public ResourceBuffer(Stream memStream, int dataOffset, int dataSize)
    {
        Buffer = new byte[dataSize];
        memStream.Seek(dataOffset, SeekOrigin.Begin);
        memStream.ReadExactly(Buffer, 0, dataSize);
    }
    
}
