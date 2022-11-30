using System.Security.Cryptography;
using System.Text;

namespace PixelSharper.Core.Resources;

public class ResourcePack
{
    private Dictionary<string, ResourceFile> FileMap { get; set; }
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
            FileMap[filePath] = resourceFile;
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
                        var fileSize = br.ReadUInt32();
                        var mapSize = br.ReadUInt32();
                        for (var i = 0; i < mapSize; i++)
                        {
                            var resourceName = br.ReadString();
                            var resourceSize = br.ReadInt32();
                            var resourceOffset = br.ReadInt32();

                            FileMap[resourceName] = new ResourceFile(resourceSize, resourceOffset);
                        }

                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        ResourceStream = new MemoryStream(br.ReadBytes((int)fs.Length));
                    }
                }
            
            }
        }
       
        



        return false;
    }

    /// <summary>
    /// Loads an unencrypted ResourceFile and FileMap from disk and populates the file stream
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
                var fileSize = br.ReadUInt32();
                var mapSize = br.ReadUInt32();
                for (var i = 0; i < mapSize; i++)
                {
                    var resourceName = br.ReadString();
                    var resourceSize = br.ReadInt32();
                    var resourceOffset = br.ReadInt32();

                    FileMap[resourceName] = new ResourceFile(resourceSize, resourceOffset);
                }
                
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                ResourceStream = new MemoryStream(br.ReadBytes((int)fs.Length));
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

                aes.GenerateKey();
               
                var tempKey = Encoding.UTF8.GetString(aes.Key);


                var fileInfo = new FileInfo(filePath);
                var directory = fileInfo.Directory.FullName;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    
                }
                var newPath = directory + Path.DirectorySeparatorChar + "key.text";

                using (var fs = new FileStream(newPath, FileMode.Create))
                {
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(@$"Generated Key : {tempKey}");
                        sw.Flush();
                    }
                }
                
                transformedKey = TransformKey(tempKey);
            }
            else
            {
                transformedKey = TransformKey(key);
            }

            var encryptor = aes.CreateEncryptor(transformedKey, aes.IV);
            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                //Write the IV to the file unencrypted
                fs.Write(aes.IV, 0, aes.IV.Length);
                var startPosition = (int)fs.Position;
                using (var cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write))
                {
                    WriteBinaryFile(cs, startPosition);
                }
            }
        }

        return false;
    }

    private void WriteBinaryFile(Stream cs, int startPosition)
    {
        //Binary Writer inherently knows the size of the type and will write the expected
        //number of bytes to the stream
        using (var bw = new BinaryWriter(cs))
        {
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
                resourceFile.ResourceOffset = (int)position;

                //TODO: replace with streaming file read in case the file is too large to fit in memory
                var fileBytes = File.ReadAllBytes(resource);
                bw.Write(fileBytes);
                position = bw.BaseStream.Position;
                bw.Flush();
            }


            //Exclude IV from the total size of the resource file?
            resourceFileSize = (int)bw.BaseStream.Length - startPosition;
            bw.Seek(startPosition, SeekOrigin.Begin);
            bw.Write(resourceFileSize);
            bw.Flush();

            //Update resource file offsets
            foreach (var resource in FileMap)
            {
                var offsetPosition = resourceKeys[resource.Key];
                bw.Seek(offsetPosition, SeekOrigin.Begin);
                bw.Write(resource.Value.ResourceOffset);
                bw.Flush();
            }
        }
    }

    private bool SaveResourcePack(string filePath)
    {
        using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            var startPosition = (int)fs.Position;
            WriteBinaryFile(fs, startPosition);
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
