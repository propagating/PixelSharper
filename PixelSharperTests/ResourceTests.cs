using System.IO;
using PixelSharper.Core.Resources;

namespace PixelSharperTests;

using NUnit.Framework;
using PixelSharper.Core.Components;

public class ResourceTests
{
    
    [Test]
    public void AddFilesToResourcePack()
    {
        var resourcePack = new ResourcePack();
        var directory = new DirectoryInfo("E:\\Projects\\C#\\PixelSharper\\PixelSharper.Core");
        var fileList = directory.GetFiles();
        foreach (var file in fileList)
        {
            resourcePack.AddFileToPack(file.FullName);
        }
        Assert.AreEqual(resourcePack.FileMap.Count, fileList.Length);
    }
    
    
    
}
