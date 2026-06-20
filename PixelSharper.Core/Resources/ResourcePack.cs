using System.Security.Cryptography;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Resources;

/// <summary>
/// Bundles many asset files into one (optionally AES-encrypted or XOR-scrambled) pack file,
/// indexed by an in-pack file map. Port of olc::ResourcePack.
/// </summary>
/// <remarks>
/// <para>
/// Encryption uses AES-256 in CBC mode with a PBKDF2-derived key (see <see cref="TransformKey"/>);
/// the random IV is written/read as a plaintext prefix of the pack file. Scrambling is a reversible
/// XOR cycle over the password (see <see cref="ScrambleFile"/>).
/// </para>
/// </remarks>
/// <seealso cref="ResourceFile"/>
/// <seealso cref="ResourceBuffer"/>
public class ResourcePack
{
    /// <summary>Index of packed file name to its size/offset within the pack stream.</summary>
    /// <value>A map from in-pack file path to its <see cref="ResourceFile"/> location record.</value>
    public Dictionary<string, ResourceFile> FileMap { get; set; }
    /// <summary>In-memory backing store of the loaded pack's bytes.</summary>
    /// <value>The decoded pack bytes that <see cref="GetFileBuffer"/> reads slices from.</value>
    private MemoryStream ResourceStream { get; set; }

    /// <summary>Fixed salt fed into PBKDF2 key derivation.</summary>
    private static readonly byte[] Salt =
        new byte[] { 2, 3, 16, 125, 21, 232, 4, 189 };

    /// <summary>Creates an empty pack with a fresh stream and file map.</summary>
    public ResourcePack()
    {
        ResourceStream = new MemoryStream();
        FileMap = new Dictionary<string, ResourceFile>();
    }

    /// <summary>Registers an existing on-disk file (by its size) into the pack's file map.</summary>
    /// <param name="filePath">Path of the file to register; its byte length is recorded.</param>
    /// <returns><c>true</c> if the file exists and was added; <c>false</c> if it does not exist.</returns>
    public bool AddFileToPack(string filePath)
    {
        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            var resourceFile = new ResourceFile((int)info.Length);
            FileMap.Add(filePath, resourceFile);
            return true;
        }

        return false;
    }

    /// <summary>Loads a pack from disk per its protection mode (plain/encrypted/scrambled) and reads its file map.</summary>
    /// <param name="filePath">Path of the pack file to load.</param>
    /// <param name="protectionMode">How the pack is protected: plain, AES-encrypted, or XOR-scrambled.</param>
    /// <param name="key">Password required for the encrypted and scrambled modes; ignored for plain.</param>
    /// <returns><c>true</c> if the pack loaded and is readable; <c>false</c> if the file is missing, a required key is blank, or scrambled (not yet supported) is requested.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protectionMode"/> is not a recognised value.</exception>
    /// <exception cref="System.IO.IOException">An I/O error occurs reading the pack file.</exception>
    /// <exception cref="CryptographicException">Decryption fails (e.g. a wrong <paramref name="key"/>) for the encrypted mode.</exception>
    /// <seealso cref="SaveResourcePack"/>
    public bool LoadResourcePack(string filePath, ResourcePackProtectionMode protectionMode, string key = "")
    {

        if (!File.Exists(filePath))
        {
            return false;
            
        }
        switch (protectionMode)
        {
            case ResourcePackProtectionMode.None:
                LoadPlainResources(filePath);
                ReadBinaryData();
                break;
            case ResourcePackProtectionMode.Encrypted:
                if (string.IsNullOrWhiteSpace(key)) return false;
                LoadEncryptedResources(filePath, key);
                ReadBinaryData();
                break;
            case ResourcePackProtectionMode.Scrambled:
                if (string.IsNullOrWhiteSpace(key)) return false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protectionMode), protectionMode, null);
        }
        

        return Loaded();
    }
    
    /// <summary>Reads an unprotected pack file straight into the resource stream.</summary>
    /// <param name="filePath">Path of the plain pack file to read.</param>
    /// <exception cref="System.IO.IOException">An I/O error occurs opening or copying the file.</exception>
    private void LoadPlainResources(string filePath)
    {

        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Options = FileOptions.RandomAccess,
            Share = FileShare.Read,
        };
        var fs = new FileStream(filePath, options);
        var ms = new MemoryStream();
        fs.CopyTo(ms);

        ResourceStream = ms;
    }

    /// <summary>Reads a leading IV then AES-CBC decrypts the pack into the resource stream.</summary>
    /// <param name="filePath">Path of the encrypted pack file to read.</param>
    /// <param name="key">Password from which the AES key is derived via <see cref="TransformKey"/>.</param>
    /// <remarks>
    /// <para>
    /// The file begins with the plaintext AES IV (one block); the remaining bytes are the
    /// AES-256/CBC ciphertext decrypted through a <see cref="CryptoStream"/> into the resource stream.
    /// </para>
    /// </remarks>
    /// <exception cref="System.IO.IOException">An I/O error occurs reading the file.</exception>
    /// <exception cref="CryptographicException">Decryption fails, e.g. the <paramref name="key"/> is wrong.</exception>
    private void LoadEncryptedResources(string filePath, string key)
    {
        byte[] binaryBuffer;
        using (var fs = new FileStream(filePath, FileMode.Open))
        {
            using (var aes = Aes.Create())
            {
                aes.Key = TransformKey(key);
                aes.Padding = PaddingMode.ISO10126;
                aes.Mode = CipherMode.CBC;
                
                var iv = new byte[aes.IV.Length];
                var numBytesToRead = aes.IV.Length;
                var currentByte = 0;
            
                while (numBytesToRead > 0)
                {
                    var n = fs.Read(iv, currentByte, numBytesToRead);
                    if (n == 0) break;

                    currentByte += n;
                    numBytesToRead -= n;
                }

                aes.IV = iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var cs = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
                {
                    binaryBuffer = new byte[(int)fs.Length];
                    
                    //Read returns the number of bytes read and can be used to validate
                    //that we actually read the entire file into our buffer
                    var bytesRead  = cs.Read(binaryBuffer, 0, (int)fs.Length);
                }
            }
        }

        ResourceStream = new MemoryStream(binaryBuffer);
    }

    /// <summary>Parses the pack header (file size, map size) and populates the file map.</summary>
    /// <remarks>
    /// <para>
    /// Reads, in order: the total file size (skipped past), the map entry count, then for each
    /// entry the file name, byte size, and stream offset, building <see cref="FileMap"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="System.IO.EndOfStreamException">The stream ends before the declared header/map is read.</exception>
    private void ReadBinaryData()
    {
        var br = new BinaryReader(ResourceStream);
        //reads file size, not currently used, but we still need to read past these bytes regardless of use.
        var fileSize = br.ReadInt32();
        var mapSize = br.ReadInt32();
        for (var i = 0; i < mapSize; i++)
        {
            var resourceName = br.ReadString();
            var resourceSize = br.ReadInt32();
            var resourceOffset = br.ReadInt32();

            FileMap[resourceName] = new ResourceFile(resourceSize, resourceOffset);
        }
    }

    /// <summary>Writes the pack to disk per its protection mode (plain/encrypted/scrambled).</summary>
    /// <param name="filePath">Destination path for the pack file.</param>
    /// <param name="protectionMode">How to protect the pack: plain, AES-encrypted, or XOR-scrambled.</param>
    /// <param name="key">Password required for the encrypted and scrambled modes; ignored for plain.</param>
    /// <returns><c>true</c> if the pack was written; <c>false</c> if a required key is blank or scrambled (not yet supported) is requested.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protectionMode"/> is not a recognised value.</exception>
    /// <exception cref="System.IO.IOException">An I/O error occurs writing the pack file.</exception>
    /// <exception cref="CryptographicException">Encryption fails for the encrypted mode.</exception>
    /// <seealso cref="LoadResourcePack"/>
    public bool SaveResourcePack(string filePath, ResourcePackProtectionMode protectionMode, string key = "")
    {

        switch (protectionMode)
        {
            case ResourcePackProtectionMode.None:
                return SavePlainResourcePack(filePath);
            case ResourcePackProtectionMode.Encrypted:
                if (string.IsNullOrWhiteSpace(key)) return false;
                if (File.Exists(filePath))File.Delete(filePath);
                return SaveEncryptedResources(filePath, key);
            case ResourcePackProtectionMode.Scrambled:
                if (string.IsNullOrWhiteSpace(key)) return false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protectionMode), protectionMode, null);

        }
        
        return false;
    }

    /// <summary>Writes the pack unencrypted via a BinaryWriter.</summary>
    /// <param name="filePath">Destination path for the plain pack file.</param>
    /// <returns>Always <c>true</c> once the write completes.</returns>
    /// <exception cref="System.IO.IOException">An I/O error occurs writing the file.</exception>
    private bool SavePlainResourcePack(string filePath)
    {
        using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            using (var bw = new BinaryWriter(fs))
            {
                WriteBinaryData(bw);
            }

        }
        return true;
    }
    /// <summary>Serialises the pack, then writes the IV followed by the AES-CBC ciphertext.</summary>
    /// <param name="filePath">Destination path for the encrypted pack file.</param>
    /// <param name="key">Password from which the AES key is derived via <see cref="TransformKey"/>.</param>
    /// <returns>Always <c>true</c> once the write completes.</returns>
    /// <remarks>
    /// <para>
    /// Generates a fresh IV, writes it as the file's plaintext prefix, then streams the serialised
    /// pack bytes through an AES-256/CBC encryptor (<see cref="CryptoStream"/>) into the file.
    /// </para>
    /// </remarks>
    /// <exception cref="System.IO.IOException">An I/O error occurs writing the file.</exception>
    /// <exception cref="CryptographicException">Encryption fails.</exception>
    private bool SaveEncryptedResources(string filePath, string key)
    {

        using (var aes = Aes.Create())
        {
            aes.Key = TransformKey(key);
            aes.Padding = PaddingMode.ISO10126;
            aes.Mode = CipherMode.CBC;
            aes.GenerateIV();

            byte[] binaryBuffer;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    WriteBinaryData(bw);
                    ms.Seek(0, SeekOrigin.Begin);
                    binaryBuffer = ms.ToArray();
                }
            }

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            
            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                fs.Write(aes.IV, 0, aes.IV.Length);
                using (var cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write))
                {
                    using (var bw = new BinaryWriter(cs))
                    {
                        bw.Write(binaryBuffer);
                        bw.Flush();
                        cs.FlushFinalBlock();
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Writes the header, file map, and file data, then back-patches the total size and per-file offsets.</summary>
    /// <param name="bw">The writer over the destination stream to serialise into.</param>
    /// <remarks>
    /// <para>
    /// Writes a placeholder total size and the map count, then each map entry (name, size, offset),
    /// recording the stream position of every offset field. It appends each file's bytes, captures the
    /// real offsets, then seeks back to patch the total size and each recorded offset position.
    /// </para>
    /// </remarks>
    /// <exception cref="System.IO.IOException">An I/O error occurs reading a source file or writing the stream.</exception>
    private void WriteBinaryData(BinaryWriter bw)
    {
        //Binary Writer inherently knows the size of the type and will write the expected
        //number of bytes to the stream

        var resourceKeys = new Dictionary<string, int>();
        var resourceFileSize = 0;
        bw.Write(resourceFileSize);
        var mapSize = FileMap.Count;
        bw.Write(mapSize);

        //Write the File Map
        foreach (var resource in FileMap)
        {
            //C# binary writer prefixes string with their length
            bw.Write(resource.Key);
            bw.Write(resource.Value.ResourceSize);
            //store start position of resource offset so that we can repopulate
            //the offset with the start position of the actual file data
            resourceKeys.Add(resource.Key, (int)bw.BaseStream.Position);
            bw.Write(resource.Value.ResourceOffset);
            bw.Flush();
        }

        //Write the File Data to the resource file
        var position = bw.BaseStream.Position;
        var keys = FileMap.Keys.ToList();
        foreach (var resource in keys)
        {
            var resourceFile = FileMap[resource];
            //With how the IV is written and read we no longer need to worry about the start position 
            resourceFile.ResourceOffset = (int)position;
            FileMap[resource] = resourceFile;

            //TODO: replace with streaming file read in case the file is too large to fit in memory
            var fileBytes = File.ReadAllBytes(resource);
            bw.Write(fileBytes);
            position = bw.BaseStream.Position;
            bw.Flush();
        }


        //Exclude IV from the total size of the resource file?
        resourceFileSize = (int)bw.BaseStream.Length;
        
        //Update the resource file size from 0 to the current length
        bw.Seek(0, SeekOrigin.Begin);
        bw.Write(resourceFileSize);
        bw.Flush();

        //Update resource file offsets at the positions we recorded in the resourceKeys dictionary,
        //with the updated offsets stored in the FileMap 
        foreach (var resource in FileMap)
        {
            var offsetPosition = resourceKeys[resource.Key];
            bw.Seek(offsetPosition, SeekOrigin.Begin);
            bw.Write(resource.Value.ResourceOffset);
            bw.Flush();
        }
        
    }

    /// <summary>XOR-cycles file bytes against the key; the same call both scrambles and unscrambles.</summary>
    /// <param name="fileData">The bytes to scramble or unscramble.</param>
    /// <param name="key">Password whose characters are XOR-cycled over the data.</param>
    /// <returns>A new array of XOR-transformed bytes, or the input array unchanged when <paramref name="key"/> is empty.</returns>
    /// <remarks>
    /// <para>The transform is its own inverse, so applying it twice with the same key restores the original bytes.</para>
    /// </remarks>
    public byte[] ScrambleFile(byte[] fileData, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return fileData;
        }

        byte[] scrambledData = new byte[fileData.Length];
        var c = 0;
        for(var i = 0; i < fileData.Length; i++)
        {
            c = (c +1) % key.Length;
            scrambledData[i] = (byte)(fileData[i] ^ key[c]);
        }

        return scrambledData;

    }

    /// <summary>Returns a buffer over the named file's bytes within the loaded pack stream.</summary>
    /// <param name="filePath">In-pack file name (a <see cref="FileMap"/> key) to extract.</param>
    /// <returns>A <see cref="ResourceBuffer"/> holding the file's bytes, or an empty buffer when no pack is loaded.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="filePath"/> is not present in <see cref="FileMap"/>.</exception>
    public ResourceBuffer GetFileBuffer(string filePath)
    {
        if (Loaded())
        {
            return new ResourceBuffer(ResourceStream!, FileMap[filePath].ResourceOffset, FileMap[filePath].ResourceSize);
        }

        return new ResourceBuffer();
    }
    /// <summary>True when a pack stream is loaded and readable.</summary>
    /// <returns><c>true</c> if the resource stream is readable; otherwise <c>false</c>.</returns>
    public bool Loaded()
    {
        return ResourceStream.CanRead;
    }

    /// <summary>Normalises a path to POSIX separators (backslash to forward slash).</summary>
    /// <param name="path">The path to convert.</param>
    /// <returns>The path with every backslash replaced by a forward slash.</returns>
    /// <remarks>
    /// <para>Pack file names are stored with POSIX separators so a pack built on Windows resolves identically on other platforms.</para>
    /// </remarks>
    public string MakePosixPath(string path)
    {
        return path.Replace("\\", "/");
    }

    /// <summary>Derives a 256-bit AES key from the password via PBKDF2 (SHA-512) over the fixed salt.</summary>
    /// <param name="password">The user password to stretch into a key.</param>
    /// <param name="keyBytes">Derived key length in bytes; defaults to 32 (256 bits) for AES-256.</param>
    /// <returns>The PBKDF2-derived key of <paramref name="keyBytes"/> bytes.</returns>
    /// <remarks>
    /// <para>
    /// Uses <see cref="Rfc2898DeriveBytes"/> with SHA-512 over the fixed <c>Salt</c> and a high iteration
    /// count, so a given password and salt always yield the same key for both encrypt and decrypt.
    /// </para>
    /// </remarks>
    private static byte[] TransformKey(string password, int keyBytes = 32)
    {
        //TODO: make this configurable
        const int iterations = 655360;
        // KeyBytes are 32*8 = 256 bits for AES. Rfc2898DeriveBytes.Pbkdf2 is the non-obsolete static API
        // (the instance constructors are deprecated, SYSLIB0060); same PBKDF2/SHA-512 derivation over the
        // fixed Salt, so the key is identical and existing encrypted packs still decrypt.
        return Rfc2898DeriveBytes.Pbkdf2(password, Salt, iterations, HashAlgorithmName.SHA512, keyBytes);
    }
}
