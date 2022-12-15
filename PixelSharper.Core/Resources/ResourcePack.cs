using System.Security.Cryptography;

namespace PixelSharper.Core.Resources;

public class ResourcePack
{
    public Dictionary<string, ResourceFile> FileMap { get; set; }
    private byte[] Buffer { get; set; }
    private Stream? ResourceStream { get; set; }

    private static readonly byte[] Salt =
        new byte[] { 2, 3, 16, 125, 21, 232, 4, 189 };

    public ResourcePack()
    {
        ResourceStream = null;
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


    /// <summary>
    /// Loads an encrypted ResourceFile from disk and populates the file stream
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="encryption"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool LoadResourcePack(string filePath, bool encryption, string key = null)
    {
        if (!encryption)
        {
            return LoadResourcePack(filePath);
        }

        byte[] binaryBuffer;
        using (var fs = new FileStream(filePath, FileMode.Open))
        {
            using (var aes = Aes.Create())
            {
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
            
            
                byte[] transformedKey;
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }
                else
                {
                    transformedKey = TransformKey(key);
                }

                using (var cs = new CryptoStream(fs, aes.CreateDecryptor(transformedKey, iv), CryptoStreamMode.Read))
                {

                    using (var br = new BinaryReader(cs))
                    {
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
                }


            }
        }
        
        return false;
    }

    /// <summary>
    /// Loads an unencrypted ResourceFile and FileMap from disk
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private bool LoadResourcePack(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        using (var fs = new FileStream(filePath, FileMode.Open))
        {
            using (var br = new BinaryReader(fs))
            {
                var fileSize = br.ReadInt32();
                var mapSize = br.ReadInt32();
                for (var i = 0; i < mapSize; i++)
                {
                    var resourceName = br.ReadString();
                    var resourceSize = br.ReadInt32();
                    var resourceOffset = br.ReadInt32();

                    FileMap[resourceName] = new ResourceFile(resourceSize, resourceOffset);
                }
                
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                ResourceStream = new MemoryStream(br.ReadBytes((int)fs.Length), false);
            }
        }



        return Loaded();
    }

    
    /// <summary>
    /// Saves the resource pack, either encrypted or not. If encrypted is set to true and no key is provided, a new key will be generated and saved
    /// to the same folder as the resource file for later use. 
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="encrypted"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool SaveResourcePack(string filePath, bool encrypted, string key = "")
    {
        var success = false;
        if (!encrypted)
        {
            return SaveResourcePack(filePath);
        }

        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.GenerateIV();
            byte[] transformedKey;
            
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            else
            {
                transformedKey = TransformKey(key);
            }

            byte[] binaryBuffer;
            using (var ms = new MemoryStream())
            {
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (var bw = new BinaryWriter(ms))
                {
                    WriteBinaryData(bw, (int)ms.Position);
                    ms.Seek(0, SeekOrigin.Begin);
                    binaryBuffer = ms.ToArray();
                }
            }
            var encryptor = aes.CreateEncryptor(transformedKey, aes.IV);
            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
            {
             
                using (var cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write))
                {
                    using(var bw = new BinaryWriter(cs))
                    {
                        bw.Write(binaryBuffer);
                        bw.Flush();
                        cs.Flush();
                    }
                    success = true;
                }
            }
        }

        return success;
    }

    private void WriteBinaryData(BinaryWriter bw, int startPosition = 0)
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
            resourceKeys.Add(resource.Key, (int)bw.BaseStream.Position + startPosition);
            bw.Write(resource.Value.ResourceOffset);
            bw.Flush();
        }

        //Write the File Data to the resource file
        var position = bw.BaseStream.Position + startPosition;
        var keys = FileMap.Keys.ToList();
        foreach (var resource in keys)
        {
            var resourceFile = FileMap[resource];
            resourceFile.ResourceOffset = (int)position;
            FileMap[resource] = resourceFile;

            //TODO: replace with streaming file read in case the file is too large to fit in memory
            var fileBytes = File.ReadAllBytes(resource);
            bw.Write(fileBytes);
            position = bw.BaseStream.Position + startPosition;
            bw.Flush();
        }


        //Exclude IV from the total size of the resource file?
        resourceFileSize = (int)bw.BaseStream.Length;
        
        //Update the resource file size from 0 to the current length
        bw.Seek(startPosition, SeekOrigin.Begin);
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

    private bool SaveResourcePack(string filePath)
    {
        using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            using (var bw = new BinaryWriter(fs))
            {
                var startPosition = (int)fs.Position;
                WriteBinaryData(bw, startPosition);
            }

        }
        return true;
    }


    public ResourceBuffer GetFileBuffer(string filePath)
    {
        if (Loaded())
        {
            return new ResourceBuffer(ResourceStream!, FileMap[filePath].ResourceOffset, FileMap[filePath].ResourceSize);
        }

        return new ResourceBuffer();
    }

    /// <summary>
    /// Ensures the ResourcePack is loaded and ready to use
    /// </summary>
    /// <returns></returns>
    public bool Loaded()
    {
        return ResourceStream != null && ResourceStream.CanRead;
    }

    private static byte[] TransformKey(string password, int keyBytes = 32)
    {
        const int iterations = 3000;
        var keyGenerator = new Rfc2898DeriveBytes(password, Salt,
                                                  iterations, HashAlgorithmName.SHA512);
        return keyGenerator.GetBytes(keyBytes);
    }
}
