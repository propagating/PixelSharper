using NUnit.Framework;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;

namespace PixelSharperTests
{
    [TestFixture]
    public class SpriteTests
    {
        [SetUp]
        public void Setup()
        {
            Sprite.ImageLoader = new MockImageLoader();
        }

        [Test]
        public void Constructor_Default_ShouldSetWidthAndHeightToZero()
        {
            // Arrange & Act
            var sprite = new Sprite();

            // Assert
            Assert.AreEqual(0, sprite.Width);
            Assert.AreEqual(0, sprite.Height);
        }

        [Test]
        public void Constructor_WithDimensions_ShouldSetWidthAndHeight()
        {
            // Arrange
            const int width = 100;
            const int height = 50;

            // Act
            var sprite = new Sprite(width, height);

            // Assert
            Assert.AreEqual(width, sprite.Width);
            Assert.AreEqual(height, sprite.Height);
        }

        [Test]
        public void Duplicate_ShouldCreateExactCopyOfSprite()
        {
            // Arrange
            const int width = 10;
            const int height = 10;
            var originalSprite = new Sprite(width, height)
            {
                SpriteDisplayMode = SpriteDisplayMode.Periodic
            };

            // Simulate some data
            for (var i = 0; i < width * height; i++)
            {
                originalSprite.PixelData.Add(new Pixel
                {
                    Red = (byte)(i % 255),
                    Green = (byte)((i + 100) % 255),
                    Blue = (byte)((i + 200) % 255),
                    Alpha = (byte)((i + 50) % 255)
                });
            }

            // Act
            var duplicateSprite = originalSprite.Duplicate();

            // Assert
            Assert.AreEqual(originalSprite.Width, duplicateSprite.Width);
            Assert.AreEqual(originalSprite.Height, duplicateSprite.Height);
            Assert.AreEqual(originalSprite.SpriteDisplayMode, duplicateSprite.SpriteDisplayMode);

            for (var i = 0; i < originalSprite.PixelData.Count; i++)
            {
                Assert.AreEqual(originalSprite.PixelData[i].Red, duplicateSprite.PixelData[i].Red);
                Assert.AreEqual(originalSprite.PixelData[i].Green, duplicateSprite.PixelData[i].Green);
                Assert.AreEqual(originalSprite.PixelData[i].Blue, duplicateSprite.PixelData[i].Blue);
                Assert.AreEqual(originalSprite.PixelData[i].Alpha, duplicateSprite.PixelData[i].Alpha);
            }
        }

        [Test]
        public void SetPixel_ValidCoordinates_ShouldSetPixel()
        {
            // Arrange
            const int width = 10;
            const int height = 10;
            var sprite = new Sprite(width, height);
            var pixel = new Pixel { Red = 255, Green = 0, Blue = 0, Alpha = 255 };

            // Act
            var result = sprite.SetPixel(5, 5, pixel);
            var resultPixel = sprite.GetPixel(5, 5);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(pixel, resultPixel);
        }

        [Test]
        public void SetPixel_InvalidCoordinates_ShouldReturnFalse()
        {
            // Arrange
            const int width = 10;
            const int height = 10;
            var sprite = new Sprite(width, height);
            var pixel = new Pixel { Red = 255, Green = 0, Blue = 0, Alpha = 255 };

            // Act & Assert
            Assert.IsFalse(sprite.SetPixel(-1, 5, pixel));
            Assert.IsFalse(sprite.SetPixel(5, -1, pixel));
            Assert.IsFalse(sprite.SetPixel(10, 5, pixel));
            Assert.IsFalse(sprite.SetPixel(5, 10, pixel));
        }

        [Test]
        public void LoadFromFile_ShouldCallImageLoader()
        {
            // Arrange
            const string filePath = "test.png";
            var resourcePack = new ResourcePack();
            var sprite = new Sprite();

            // Act
            var result = sprite.LoadFromFile(filePath, resourcePack);

            // Assert
            Assert.AreEqual(FileReadCode.OK, result);
            Assert.IsNotEmpty(sprite.PixelData);
        }

        [Test]
        public void SampleBl_ShouldReturnInterpolatedPixel()
        {
            // Arrange
            var sprite = new Sprite(2, 2);
            sprite.SetPixel(0, 0, new Pixel { Red = 0, Green = 0, Blue = 0, Alpha = 255 });
            sprite.SetPixel(1, 0, new Pixel { Red = 255, Green = 0, Blue = 0, Alpha = 255 });
            sprite.SetPixel(0, 1, new Pixel { Red = 0, Green = 255, Blue = 0, Alpha = 255 });
            sprite.SetPixel(1, 1, new Pixel { Red = 255, Green = 255, Blue = 0, Alpha = 255 });

            // Act
            var result = sprite.SampleBl(0.5f, 0.5f);

            // Assert
            Assert.AreEqual(127, result.Red); // Expected interpolated value
            Assert.AreEqual(127, result.Green);
            Assert.AreEqual(0, result.Blue);
            Assert.AreEqual(255, result.Alpha);
        }

        private class MockImageLoader : ImageLoader
        {
            public override FileReadCode LoadImageResource(Sprite sprite, string imageFilePath, ResourcePack resourcePack)
            {
                // Mock image loading logic
                sprite.SetSize(10, 10);
                for (var i = 0; i < 100; i++)
                {
                    sprite.PixelData.Add(new Pixel {
                        Red = (byte)(i % 255),
                        Green = (byte)((i + 100) % 255), 
                        Blue = (byte)((i + 200) % 255), 
                        Alpha = (byte)((i + 50) % 255) });
                }
                return FileReadCode.OK;
            }
        }
    }
}