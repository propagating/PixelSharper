using System.Security.Cryptography;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Resources;

public class ResourcePack
{
    public Dictionary<string, ResourceFile> FileMap { get; set; }
    private MemoryStream ResourceStream { get; set; }

    private static readonly byte[] Salt =
        new byte[] { 2, 3, 16, 125, 21, 232, 4, 189 };

    public ResourcePack()
    {
        ResourceStream = new MemoryStream();
        FileMap = new Dictionary<string, ResourceFile>();
    }

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

    // can be used to scramble or unscramble file data with the same key because XOR is cyclical (assumes not byte corruption
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

    public ResourceBuffer GetFileBuffer(string filePath)
    {
        if (Loaded())
        {
            return new ResourceBuffer(ResourceStream!, FileMap[filePath].ResourceOffset, FileMap[filePath].ResourceSize);
        }

        return new ResourceBuffer();
    }
    public bool Loaded()
    {
        return ResourceStream.CanRead;
    }
    
    public string MakePosixPath(string path)
    {
        return path.Replace("\\", "/");
    }

    private static byte[] TransformKey(string password, int keyBytes = 32)
    {
        //TODO: make this configurable
        const int iterations = 655360;
        var keyGenerator = new Rfc2898DeriveBytes(password, Salt,
                                                  iterations, HashAlgorithmName.SHA512);
        //KeyBytes are 32*8 = 256 bits for AES
        return keyGenerator.GetBytes(keyBytes);
    }
}
