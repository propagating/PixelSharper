namespace PixelSharper.Core.Buffers;

public class ResourcePack
{
    private Dictionary<string, ResourceFile> FileMap { get; set; }
    private FileStream? BaseFile { get; set; }
    
    public ResourcePack()
    {
        BaseFile = null;
        FileMap = new Dictionary<string, ResourceFile>();
    }

    public bool AddFileToPack(string filePath)
    {
        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            var resourceFile = new ResourceFile((uint)info.Length);
            FileMap[filePath] = resourceFile;
            return true;
        }
        return false;
    }
    
    public bool LoadResourcePack(string filePath)
    {

        if (!File.Exists(filePath))
        {
            return false;
        }

        BaseFile = new FileStream(filePath, FileMode.Open);

        using (var br = new BinaryReader(BaseFile))
        {
            var fileSize = br.ReadUInt32();
            var mapSize = br.ReadUInt32();
            for (var i = 0; i < mapSize; i++)
            {
                var resourceName = br.ReadString();
                var resourceSize = br.ReadUInt32();
                var resourceOffest = br.ReadUInt32();
                
                FileMap[resourceName] = new ResourceFile(resourceSize, resourceOffest);
            }
        }
        
        return false;
    }

    public bool SaveResourcePack(string filePath)
    {
        var resourceKeys = new Dictionary<string, int>();
        using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            
            //Binary Writer inherently knows the size of the type and will write the expected
            //number of bytes to the stream
            using (var bw = new BinaryWriter(fs))
            {
                uint resourceFileSize = 0;
                bw.Write(resourceFileSize);
                var mapSize = (uint)FileMap.Count;
                bw.Write(mapSize);

                //Write the File Index
                foreach (var resource in FileMap)
                {
                    //C# binary writer prefixes string with their length
                    bw.Write(resource.Key);
                    bw.Write(resource.Value.ResourceSize);
                    //store start position of resource offset for later use 
                    resourceKeys.Add(resource.Key, (int)bw.BaseStream.Position);
                    bw.Write(resource.Value.ResourceOffset);
                    bw.Flush();
                }
                
                //Write the File Data
                var position = bw.BaseStream.Position;
                foreach (var resource in FileMap)
                {
                    var file = resource.Value;
                    file.ResourceOffset = (uint)position; 
                    
                    //TODO: replace with streaming file read
                    var fileBytes = File.ReadAllBytes(resource.Key);
                    bw.Write(fileBytes);
                    position = bw.BaseStream.Position;
                    bw.Flush();
                }
                
                
                resourceFileSize = (uint)bw.BaseStream.Length;;
                bw.Seek(0, SeekOrigin.Begin);
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

        return false;
    }
    

    public ResourceBuffer GetFileBuffer(string filePath)
    {
        return new ResourceBuffer();
    }

    public bool Loaded()
    {
        return BaseFile != null && BaseFile.CanRead;
    }

    private byte[] Scramble(byte[] fileData, string key)
    {
        return new byte[0];
    }    
    
}
