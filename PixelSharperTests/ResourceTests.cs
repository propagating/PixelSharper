using System.IO;
using PixelSharper.Core.Resources;

namespace PixelSharperTests;

using NUnit.Framework;

public class ResourceTests
{
    
    [Test]
    public void AddFilesToResourcePack()
    {
        // Build a temp directory with known files so the test is portable (no hardcoded machine path,
        // which previously broke on any machine but the author's — including CI).
        var directory = Directory.CreateTempSubdirectory("pixelsharper_respack_");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "a.txt"), "alpha");
            File.WriteAllBytes(Path.Combine(directory.FullName, "b.bin"), [1, 2, 3, 4]);
            File.WriteAllText(Path.Combine(directory.FullName, "c.dat"), "gamma payload");

            var resourcePack = new ResourcePack();
            var fileList = directory.GetFiles();
            foreach (var file in fileList)
            {
                resourcePack.AddFileToPack(file.FullName);
            }
            Assert.AreEqual(fileList.Length, resourcePack.FileMap.Count);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
    
    
    
}
