using System.IO;
using System.Runtime.InteropServices;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using StbImageSharp;

namespace PixelSharper.Core.Resources;

/// <summary>
/// Concrete <see cref="ImageLoader"/> backed by the managed stb_image port (StbImageSharp /
/// StbImageWriteSharp) — the C# equivalent of olc's <c>ImageLoader_STB</c>. Decodes PNG/JPG/BMP/etc.
/// into a <see cref="Sprite"/> and encodes a sprite back out as PNG.
/// </summary>
/// <remarks>
/// <para>
/// Images are decoded forcing four channels (RGBA), exactly like olc's
/// <c>stbi_load(..., 4)</c>. The decoded byte order (R, G, B, A) is identical to the blittable
/// 4-byte <see cref="Pixel"/> layout, so the pixel store is filled by a single reinterpreting copy
/// — no per-pixel conversion.
/// </para>
/// <para>
/// olc's stb backend left <c>SaveImageResource</c> as an empty stub; this port implements it as a
/// real PNG write (the GDI+/libpng olc backends do save), giving a working load/save round-trip.
/// </para>
/// </remarks>
/// <seealso cref="ImageLoader"/>
/// <seealso cref="Sprite"/>
public class ImageLoaderStb : ImageLoader
{
    /// <summary>Decodes an image into the sprite from a resource pack or from disk, forcing RGBA.</summary>
    /// <param name="sprite">The sprite to populate; its pixel store is replaced with the decoded image.</param>
    /// <param name="fileName">Path of the image (a pack entry name when <paramref name="resourcePack"/> is supplied, else a file path).</param>
    /// <param name="resourcePack">Optional pack to read the encoded bytes from, or <c>null</c> to read from disk.</param>
    /// <returns><see cref="FileReadCode.Ok"/> on success; <see cref="FileReadCode.NoFile"/> when the file/entry is absent; <see cref="FileReadCode.Fail"/> when decoding fails.</returns>
    public override FileReadCode LoadImageResource(Sprite sprite, string fileName, ResourcePack resourcePack)
    {
        // Clear any existing pixels (olc clears pColData up front).
        sprite.PixelData.Clear();

        ImageResult? image;
        if (resourcePack != null)
        {
            if (!resourcePack.FileMap.ContainsKey(fileName)) return FileReadCode.NoFile;
            var bytes = resourcePack.GetFileBuffer(fileName).Buffer;
            image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        }
        else
        {
            if (!File.Exists(fileName)) return FileReadCode.NoFile;
            using var stream = File.OpenRead(fileName);
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }

        if (image == null || image.Width <= 0 || image.Height <= 0) return FileReadCode.Fail;

        // SetSize grows the pixel store to width*height; the RGBA bytes then copy straight in,
        // reinterpreted as Pixel (same byte order, so no per-pixel conversion).
        sprite.SetSize(image.Width, image.Height);
        var destination = CollectionsMarshal.AsSpan(sprite.PixelData);
        MemoryMarshal.Cast<byte, Pixel>(image.Data).CopyTo(destination);
        return FileReadCode.Ok;
    }

    /// <summary>Encodes the sprite's pixels to <paramref name="fileName"/> as a PNG.</summary>
    /// <param name="sprite">The sprite to encode; must have positive dimensions.</param>
    /// <param name="fileName">Destination file path for the PNG.</param>
    /// <returns><see cref="FileReadCode.Ok"/> on success; <see cref="FileReadCode.Fail"/> if the sprite has no pixels.</returns>
    public override FileReadCode SaveImageResource(Sprite sprite, string fileName)
    {
        if (sprite.Width <= 0 || sprite.Height <= 0) return FileReadCode.Fail;

        // Pixel is a blittable 4-byte RGBA struct, so the backing store reinterprets straight to bytes.
        var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(sprite.PixelData)).ToArray();
        using var stream = File.OpenWrite(fileName);
        var writer = new StbImageWriteSharp.ImageWriter();
        writer.WritePng(bytes, sprite.Width, sprite.Height,
            StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
        return FileReadCode.Ok;
    }
}
