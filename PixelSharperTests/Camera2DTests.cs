using NUnit.Framework;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;

namespace PixelSharperTests
{
    [TestFixture]
    public class Camera2DTests
    {
        [Test]
        public void Simple_FollowsTarget_AndCentresView()
        {
            var cam = new Camera2D(new Vector2d<float>(10, 10));
            cam.SetMode(Camera2D.Mode.Simple);
            cam.SetTarget(new Vector2d<float>(50, 50));

            var visible = cam.Update(0f);

            Assert.AreEqual(50, cam.GetPosition().X, 1e-4);
            Assert.AreEqual(45, cam.GetViewPosition().X, 1e-4); // centred: 50 - 10/2
            Assert.AreEqual(45, cam.GetViewPosition().Y, 1e-4);
            Assert.IsTrue(visible);
        }

        [Test]
        public void LiveProvider_IsTracked()
        {
            var cam = new Camera2D(new Vector2d<float>(10, 10));
            cam.SetTarget(() => new Vector2d<float>(7, 7));
            cam.Update(0f);
            Assert.AreEqual(7, cam.GetPosition().X, 1e-4);
            Assert.AreEqual(7, cam.GetPosition().Y, 1e-4);
        }

        [Test]
        public void WorldBoundary_ClampsViewPosition()
        {
            var cam = new Camera2D(new Vector2d<float>(10, 10));
            cam.SetWorldBoundary(new Vector2d<float>(0, 0), new Vector2d<float>(100, 100));
            cam.EnableWorldBoundary(true);
            cam.SetTarget(new Vector2d<float>(2, 2)); // would centre view at (-3,-3)

            cam.Update(0f);

            // Clamped to the boundary top-left.
            Assert.AreEqual(0, cam.GetViewPosition().X, 1e-4);
            Assert.AreEqual(0, cam.GetViewPosition().Y, 1e-4);
        }

        [Test]
        public void LazyFollow_EasesTowardTarget()
        {
            var cam = new Camera2D(new Vector2d<float>(10, 10));
            cam.SetMode(Camera2D.Mode.LazyFollow);
            cam.SetLazyFollowRate(4.0f);
            cam.SetTarget(new Vector2d<float>(100, 0));

            cam.Update(0.1f); // moves a fraction (rate*dt = 0.4) toward target
            var x = cam.GetPosition().X;
            Assert.That(x, Is.GreaterThan(0).And.LessThan(100));
        }
    }
}
