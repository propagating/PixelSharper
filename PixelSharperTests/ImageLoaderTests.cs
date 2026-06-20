using System.IO;
using NUnit.Framework;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;

namespace PixelSharperTests
{
    // Exercises the concrete stb ImageLoader: a lossless PNG save/load round-trip (pixel-exact, incl.
    // alpha) and the missing-file path. The decode shared by the resource-pack path is covered here;
    // the pack-specific byte read (GetFileBuffer) is covered by ResourceTests.
    [TestFixture]
    public class ImageLoaderTests
    {
        private ImageLoaderStb _loader = null!;
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _loader = new ImageLoaderStb();
            _tempDir = Directory.CreateTempSubdirectory("pixelsharper_img_").FullName;
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_tempDir, recursive: true);

        // int args cast to byte inside, to avoid binding the float (PixelF, 0..1) Pixel constructor.
        private static Pixel P(int r, int g, int b, int a) => new((byte)r, (byte)g, (byte)b, (byte)a);

        [Test]
        public void SaveThenLoad_RoundTripsPixelsExactly()
        {
            // Arrange — a 3x2 sprite with distinct, alpha-varying colours.
            var colours = new[]
            {
                P(255, 0, 0, 255), P(0, 255, 0, 255), P(0, 0, 255, 255),
                P(10, 20, 30, 40), P(200, 150, 100, 128), P(0, 0, 0, 0)
            };
            var source = new Sprite(3, 2);
            for (var i = 0; i < colours.Length; i++) source.SetPixel(i % 3, i / 3, colours[i]);
            var path = Path.Combine(_tempDir, "roundtrip.png");

            // Act
            var saved = _loader.SaveImageResource(source, path);
            var loaded = new Sprite();
            var read = _loader.LoadImageResource(loaded, path, null!);

            // Assert — PNG is lossless, so every pixel (including alpha) survives the round trip.
            Assert.AreEqual(FileReadCode.Ok, saved);
            Assert.AreEqual(FileReadCode.Ok, read);
            Assert.AreEqual(3, loaded.Width);
            Assert.AreEqual(2, loaded.Height);
            Assert.AreEqual(colours.Length, loaded.PixelData.Count);
            for (var i = 0; i < colours.Length; i++)
                Assert.AreEqual(colours[i].N, loaded.PixelData[i].N, $"pixel {i} mismatch");
        }

        [Test]
        public void Load_MissingFile_ReturnsNoFile()
        {
            // Arrange / Act
            var sprite = new Sprite();
            var code = _loader.LoadImageResource(sprite, Path.Combine(_tempDir, "does-not-exist.png"), null!);

            // Assert
            Assert.AreEqual(FileReadCode.NoFile, code);
        }

        [Test]
        public void Save_EmptySprite_ReturnsFail()
        {
            // Arrange — a zero-sized sprite has nothing to encode.
            var sprite = new Sprite();

            // Act
            var code = _loader.SaveImageResource(sprite, Path.Combine(_tempDir, "empty.png"));

            // Assert
            Assert.AreEqual(FileReadCode.Fail, code);
        }
    }
}
