using System.Collections.Generic;
using System.IO;
using System.Text;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;

namespace PixelSharperTests;

using NUnit.Framework;

public class ResourceTests
{

    [TearDown]
    public void TearDown()
    {
        // Round-trip tests may tweak the global iteration count; restore the default so order
        // independence holds.
        ResourcePack.KeyDerivationIterations = ResourcePack.DefaultKeyDerivationIterations;
    }

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

    // Builds a pack from three known files, saves it under the given protection, loads it back, and
    // asserts every file's bytes survive the round-trip. Exercises the streamed (non-ReadAllBytes)
    // write path in WriteBinaryData.
    private static void AssertRoundTrips(ResourcePackProtectionMode mode, string key)
    {
        var directory = Directory.CreateTempSubdirectory("pixelsharper_respack_rt_");
        var packPath = Path.Combine(directory.FullName, "assets.pack");
        try
        {
            var files = new Dictionary<string, byte[]>
            {
                ["a.txt"] = Encoding.UTF8.GetBytes("alpha"),
                ["b.bin"] = [1, 2, 3, 4, 250, 128, 0],
                ["c.dat"] = Encoding.UTF8.GetBytes("gamma payload that is a little longer"),
            };
            foreach (var (name, bytes) in files)
                File.WriteAllBytes(Path.Combine(directory.FullName, name), bytes);

            var source = new ResourcePack();
            foreach (var name in files.Keys)
                Assert.That(source.AddFileToPack(Path.Combine(directory.FullName, name)), Is.True);

            Assert.That(source.SaveResourcePack(packPath, mode, key), Is.True);

            var loaded = new ResourcePack();
            Assert.That(loaded.LoadResourcePack(packPath, mode, key), Is.True);

            foreach (var (name, expected) in files)
            {
                // The in-pack file names are the on-disk paths used at AddFileToPack time.
                var key2 = Path.Combine(directory.FullName, name);
                var buffer = loaded.GetFileBuffer(key2);
                Assert.That(buffer.Buffer, Is.EqualTo(expected), $"contents of {name} did not survive the round-trip");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void SaveAndLoad_Plain_RoundTripsEveryFile()
    {
        // Act / Assert
        AssertRoundTrips(ResourcePackProtectionMode.None, key: "");
    }

    [Test]
    public void SaveAndLoad_Encrypted_RoundTripsEveryFile()
    {
        // Act / Assert
        AssertRoundTrips(ResourcePackProtectionMode.Encrypted, key: "correct horse battery staple");
    }

    [Test]
    public void AddFileToPack_SameFileTwice_RefreshesInsteadOfThrowing()
    {
        // olc's AddFile overwrites the map entry on a duplicate add; Dictionary.Add used to throw.
        var directory = Directory.CreateTempSubdirectory("pixelsharper_respack_dup_");
        try
        {
            var filePath = Path.Combine(directory.FullName, "a.txt");
            File.WriteAllText(filePath, "alpha");

            var pack = new ResourcePack();
            Assert.That(pack.AddFileToPack(filePath), Is.True);
            Assert.That(pack.AddFileToPack(filePath), Is.True, "re-adding the same file must not throw");
            Assert.That(pack.FileMap.Count, Is.EqualTo(1));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void Loaded_IsFalseBeforeAnyLoad()
    {
        // A fresh pack's empty MemoryStream is readable, but that must not count as "loaded"
        // (olc: baseFile.is_open()). GetFileBuffer relies on this for its empty-buffer fallback.
        var pack = new ResourcePack();
        Assert.That(pack.Loaded(), Is.False);
        Assert.That(pack.GetFileBuffer("nope").Buffer, Is.Null);
    }

    [Test]
    public void LoadResourcePack_Scrambled_ReturnsFalse()
    {
        // Scrambled mode is not implemented yet; loading must report failure, not a false success
        // over an empty stream.
        var directory = Directory.CreateTempSubdirectory("pixelsharper_respack_scr_");
        try
        {
            var packPath = Path.Combine(directory.FullName, "assets.pack");
            File.WriteAllBytes(packPath, [1, 2, 3, 4]);

            var pack = new ResourcePack();
            Assert.That(pack.LoadResourcePack(packPath, ResourcePackProtectionMode.Scrambled, "key"), Is.False);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void SavePlain_OverAnExistingLargerFile_Truncates()
    {
        // FileMode.OpenOrCreate left the tail of a longer pre-existing file in place, so the pack
        // carried stale garbage and its back-patched total-size header recorded the stale length.
        var directory = Directory.CreateTempSubdirectory("pixelsharper_respack_trunc_");
        try
        {
            var assetPath = Path.Combine(directory.FullName, "a.txt");
            File.WriteAllText(assetPath, "alpha");

            var pack = new ResourcePack();
            Assert.That(pack.AddFileToPack(assetPath), Is.True);

            var freshPath = Path.Combine(directory.FullName, "fresh.pack");
            Assert.That(pack.SaveResourcePack(freshPath, ResourcePackProtectionMode.None), Is.True);
            var expectedLength = new FileInfo(freshPath).Length;

            // Pre-fill the destination with junk longer than the real pack, then save over it.
            var overwrittenPath = Path.Combine(directory.FullName, "overwritten.pack");
            File.WriteAllBytes(overwrittenPath, new byte[expectedLength + 4096]);
            Assert.That(pack.SaveResourcePack(overwrittenPath, ResourcePackProtectionMode.None), Is.True);

            Assert.That(new FileInfo(overwrittenPath).Length, Is.EqualTo(expectedLength),
                "saving over a larger existing file must truncate it");

            // And the truncated pack still loads and round-trips its file.
            var loaded = new ResourcePack();
            Assert.That(loaded.LoadResourcePack(overwrittenPath, ResourcePackProtectionMode.None), Is.True);
            Assert.That(Encoding.UTF8.GetString(loaded.GetFileBuffer(assetPath).Buffer), Is.EqualTo("alpha"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void Encrypted_RoundTripsUnderACustomIterationCount()
    {
        // Arrange — pick a non-default (cheap) iteration count so the test proves the knob is actually
        // consumed by key derivation (a faithful save+load must agree on it).
        ResourcePack.KeyDerivationIterations = 4096;
        Assert.That(ResourcePack.KeyDerivationIterations, Is.Not.EqualTo(ResourcePack.DefaultKeyDerivationIterations));

        // Act / Assert — save and load both run under the custom count and the data survives.
        AssertRoundTrips(ResourcePackProtectionMode.Encrypted, key: "pw");
    }
}
