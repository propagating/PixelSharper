using System.IO;
using NUnit.Framework;
using PixelSharper.Core.Extensions.Gfx3d;

namespace PixelSharperTests
{
    [TestFixture]
    public class Graphics3DTests
    {
        private static Vec3d V(float x, float y, float z) => new() { X = x, Y = y, Z = z, W = 1 };

        [Test]
        public void Vector_DotCrossLength()
        {
            Assert.AreEqual(32, Gfx3dMath.VecDotProduct(V(1, 2, 3), V(4, 5, 6)), 1e-5);
            var cross = Gfx3dMath.VecCrossProduct(V(1, 0, 0), V(0, 1, 0));
            Assert.AreEqual(0, cross.X, 1e-5);
            Assert.AreEqual(1, cross.Z, 1e-5);
            Assert.AreEqual(5, Gfx3dMath.VecLength(V(3, 4, 0)), 1e-5);
        }

        [Test]
        public void Matrix_TranslationAndMultiply()
        {
            var t = Gfx3dMath.MatMakeTranslation(1, 2, 3);
            var p = Gfx3dMath.MatMultiplyVector(t, V(0, 0, 0));
            Assert.AreEqual(1, p.X, 1e-5);
            Assert.AreEqual(2, p.Y, 1e-5);
            Assert.AreEqual(3, p.Z, 1e-5);

            // A * Identity == A
            var prod = Gfx3dMath.MatMultiplyMatrix(t, Gfx3dMath.MatMakeIdentity());
            Assert.AreEqual(1, prod.M[3, 0], 1e-5);
            Assert.AreEqual(2, prod.M[3, 1], 1e-5);
        }

        [Test]
        public void Clip_InsideOutsideStraddle()
        {
            var planeP = new Vec3d { Z = 0.1f };
            var planeN = new Vec3d { Z = 1f };

            var inside = new Tri(); inside.P[0] = V(0, 0, 1); inside.P[1] = V(1, 0, 1); inside.P[2] = V(0, 1, 1);
            Assert.AreEqual(1, Gfx3dMath.TriangleClipAgainstPlane(planeP, planeN, inside, out _, out _));

            var outside = new Tri(); outside.P[0] = V(0, 0, -1); outside.P[1] = V(1, 0, -1); outside.P[2] = V(0, 1, -1);
            Assert.AreEqual(0, Gfx3dMath.TriangleClipAgainstPlane(planeP, planeN, outside, out _, out _));

            var straddle = new Tri(); straddle.P[0] = V(0, 0, 1); straddle.P[1] = V(1, 0, 1); straddle.P[2] = V(0, 1, -1);
            Assert.AreEqual(2, Gfx3dMath.TriangleClipAgainstPlane(planeP, planeN, straddle, out _, out _)); // 2 inside -> quad
        }

        [Test]
        public void Mesh_LoadsObj()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
            try
            {
                var m = new Mesh();
                Assert.IsTrue(m.LoadObjFile(path));
                Assert.AreEqual(1, m.Tris.Count);
                Assert.AreEqual(1, m.Tris[0].P[1].X, 1e-5);
                Assert.AreEqual(1, m.Tris[0].P[2].Y, 1e-5);
            }
            finally { File.Delete(path); }
        }
    }
}
