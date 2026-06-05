using NUnit.Framework;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharperTests
{
    [TestFixture]
    public class Geometry2DTests
    {
        private static Vector2d<float> V(float x, float y) => new(x, y);

        [Test]
        public void Rect_Contains_Point()
        {
            var r = new Rect<float>(V(0, 0), V(10, 10));
            Assert.IsTrue(Geom2D.Contains(r, V(5, 5)));
            Assert.IsTrue(Geom2D.Contains(r, V(0, 0)));   // edge
            Assert.IsFalse(Geom2D.Contains(r, V(11, 5)));
            Assert.IsFalse(Geom2D.Contains(r, V(5, -1)));
        }

        [Test]
        public void Circle_Contains_Point()
        {
            var c = new Circle<float>(V(0, 0), 5);
            Assert.IsTrue(Geom2D.Contains(c, V(3, 4)));   // exactly on radius (3-4-5)
            Assert.IsTrue(Geom2D.Contains(c, V(0, 0)));
            Assert.IsFalse(Geom2D.Contains(c, V(4, 4)));  // dist ~5.66 > 5
        }

        [Test]
        public void Rect_Overlaps_Rect()
        {
            var a = new Rect<float>(V(0, 0), V(10, 10));
            Assert.IsTrue(Geom2D.Overlaps(a, new Rect<float>(V(5, 5), V(10, 10))));
            Assert.IsFalse(Geom2D.Overlaps(a, new Rect<float>(V(20, 20), V(5, 5))));
        }

        [Test]
        public void Circle_Overlaps_Circle_And_Rect()
        {
            var c = new Circle<float>(V(0, 0), 5);
            Assert.IsTrue(Geom2D.Overlaps(c, new Circle<float>(V(8, 0), 5)));   // gap 8 < 10
            Assert.IsFalse(Geom2D.Overlaps(c, new Circle<float>(V(20, 0), 5)));
            Assert.IsTrue(Geom2D.Overlaps(c, new Rect<float>(V(3, -1), V(4, 2))));
            Assert.IsFalse(Geom2D.Overlaps(c, new Rect<float>(V(10, 10), V(2, 2))));
        }

        [Test]
        public void Closest_OnLine_ClampsToSegment()
        {
            var l = new Line<float>(V(0, 0), V(10, 0));
            var mid = Geom2D.Closest(l, V(5, 5));
            Assert.AreEqual(5, mid.X, 1e-4);
            Assert.AreEqual(0, mid.Y, 1e-4);
            // Beyond the end clamps to the endpoint.
            var end = Geom2D.Closest(l, V(20, 5));
            Assert.AreEqual(10, end.X, 1e-4);
        }

        [Test]
        public void Closest_OnCircle_IsOnPerimeter()
        {
            var c = new Circle<float>(V(0, 0), 5);
            var p = Geom2D.Closest(c, V(10, 0));
            Assert.AreEqual(5, p.X, 1e-4);
            Assert.AreEqual(0, p.Y, 1e-4);
        }

        [Test]
        public void Triangle_Contains_Point()
        {
            var t = new Triangle<float>(V(0, 0), V(10, 0), V(0, 10));
            Assert.IsTrue(Geom2D.Contains(t, V(2, 2)));
            Assert.IsFalse(Geom2D.Contains(t, V(8, 8)));
        }

        [Test]
        public void Rect_IntType_WorksForAnimate2D()
        {
            // Rect<int> is the data type Animate2D's Frame uses.
            var r = new Rect<int>(new Vector2d<int>(4, 8), new Vector2d<int>(16, 16));
            Assert.AreEqual(16, r.Size.X);
            Assert.AreEqual(256, r.Area());
            Assert.IsTrue(Geom2D.Contains(r, new Vector2d<int>(10, 10)));
        }
    }
}
