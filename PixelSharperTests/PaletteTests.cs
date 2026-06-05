using NUnit.Framework;
using PixelSharper.Core.Components;
using PixelSharper.Core.Utilities;

namespace PixelSharperTests
{
    [TestFixture]
    public class PaletteTests
    {
        [Test]
        public void Empty_SamplesToBlack()
        {
            var pal = new Palette(Palette.Stock.Empty);
            Assert.AreEqual(Pixel.BLACK, pal.Sample(0.5));
            Assert.AreEqual(Pixel.BLACK, pal.Index(128));
        }

        [Test]
        public void Greyscale_Endpoints_AndMidpoint()
        {
            var pal = new Palette(Palette.Stock.Greyscale);
            Assert.AreEqual(Pixel.BLACK, pal.Sample(0.0));
            Assert.AreEqual(Pixel.WHITE, pal.Sample(1.0));

            // Midpoint is halfway between black and white.
            var mid = pal.Sample(0.5);
            Assert.That(mid.Red, Is.EqualTo(127).Within(1));
            Assert.That(mid.Green, Is.EqualTo(127).Within(1));
            Assert.That(mid.Blue, Is.EqualTo(127).Within(1));
        }

        [Test]
        public void Spectrum_StartsRed_AndIndexMatchesSample()
        {
            var pal = new Palette(Palette.Stock.Spectrum);
            Assert.AreEqual(Pixel.RED, pal.Sample(0.0));
            Assert.AreEqual(Pixel.GREEN, pal.Sample(2.0 / 6.0));
            // The LUT entry should match a continuous sample at the same position.
            Assert.AreEqual(pal.Sample(0.0), pal.Index(0));
            Assert.AreEqual(pal.Sample(1.0), pal.Index(255));
        }

        [Test]
        public void SetColour_AddsAndInterpolates()
        {
            var pal = new Palette(Palette.Stock.Empty);
            pal.SetColour(0.0, Pixel.BLACK);
            pal.SetColour(1.0, new Pixel(255, 0, 0));
            var mid = pal.Sample(0.5);
            Assert.That(mid.Red, Is.EqualTo(127).Within(1));
            Assert.AreEqual(0, mid.Green);
        }

        [Test]
        public void SetColour_ReplacesExistingLocation()
        {
            var pal = new Palette(Palette.Stock.Empty);
            pal.SetColour(0.5, Pixel.RED);
            pal.SetColour(0.5, Pixel.BLUE); // replace, not add a second stop
            Assert.AreEqual(Pixel.BLUE, pal.Sample(0.5));
        }
    }
}
