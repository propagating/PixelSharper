using NUnit.Framework;
using PixelSharper.Core.Extensions.Fx;

namespace PixelSharperTests
{
    // Only the data-side surface is headless-testable; the Shade pipeline needs a live GL context.
    [TestFixture]
    public class ShadersTests
    {
        [Test]
        public void BuiltInEffects_HaveSourcesAndAttributes()
        {
            Assert.IsNotEmpty(Fx.Normal.VertexSource);
            Assert.IsNotEmpty(Fx.Normal.PixelSource);
            Assert.IsNotEmpty(Fx.Greyscale.PixelSource);

            Assert.AreEqual(1, Fx.BoxBlur.Attributes.Count);
            Assert.AreEqual("box_width", Fx.BoxBlur.Attributes[0].Name);
            Assert.AreEqual(1, Fx.Threshold.Attributes.Count);
            Assert.AreEqual(3, Fx.Scanline.Attributes.Count);
        }

        [Test]
        public void Effect_DefaultIsOkWithNoSlots()
        {
            var e = new Effect();
            Assert.IsTrue(e.IsOK());
            Assert.AreEqual("", e.GetStatus());
            Assert.AreEqual(0, e.GetTargetSlots());
            Assert.AreEqual(0, e.GetInputSlots());
        }
    }
}
