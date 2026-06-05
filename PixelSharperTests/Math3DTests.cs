using System;
using NUnit.Framework;
using PixelSharper.Core.Types;

namespace PixelSharperTests
{
    [TestFixture]
    public class Math3DTests
    {
        [Test]
        public void Vector3d_DotCrossMagNormLerp()
        {
            Assert.AreEqual(32, new Vector3d(1, 2, 3).Dot(new Vector3d(4, 5, 6)), 1e-5);

            var cross = new Vector3d(1, 0, 0).Cross(new Vector3d(0, 1, 0));
            Assert.AreEqual(0, cross.X, 1e-5);
            Assert.AreEqual(0, cross.Y, 1e-5);
            Assert.AreEqual(1, cross.Z, 1e-5);

            Assert.AreEqual(5, new Vector3d(3, 4, 0).Mag(), 1e-5);

            var n = new Vector3d(3, 4, 0).Norm();
            Assert.AreEqual(0.6, n.X, 1e-5);
            Assert.AreEqual(0.8, n.Y, 1e-5);

            var l = new Vector3d(0, 0, 0).Lerp(new Vector3d(10, 0, 0), 0.5f);
            Assert.AreEqual(5, l.X, 1e-5);
        }

        [Test]
        public void Matrix_Identity_Translation_Rotation()
        {
            var id = new Matrix4x4();
            var v = id * new Vector3d(7, 8, 9, 1);
            Assert.AreEqual(7, v.X, 1e-5);
            Assert.AreEqual(9, v.Z, 1e-5);

            var t = Matrix4x4.Translation(1, 2, 3) * new Vector3d(0, 0, 0, 1);
            Assert.AreEqual(1, t.X, 1e-5);
            Assert.AreEqual(2, t.Y, 1e-5);
            Assert.AreEqual(3, t.Z, 1e-5);

            // RotateZ(90deg): (1,0,0) -> (0,1,0)
            var r = Matrix4x4.RotateZ(MathF.PI / 2) * new Vector3d(1, 0, 0, 1);
            Assert.AreEqual(0, r.X, 1e-5);
            Assert.AreEqual(1, r.Y, 1e-5);
        }

        [Test]
        public void Matrix_Multiply_And_Invert()
        {
            var a = Matrix4x4.Translation(1, 2, 3);

            // A * Identity == A
            var prod = a * new Matrix4x4();
            Assert.AreEqual(1, prod[3, 0], 1e-5);
            Assert.AreEqual(2, prod[3, 1], 1e-5);

            // A * A^-1 == Identity
            var ident = a * a.Invert();
            for (var c = 0; c < 4; c++)
                for (var rw = 0; rw < 4; rw++)
                    Assert.AreEqual(c == rw ? 1f : 0f, ident[c, rw], 1e-4);
        }

        [Test]
        public void PointAt_QuickInvert_IsView()
        {
            // The inverse of a "point at" (camera placement) is the view matrix; it should map the
            // camera's own world position to the view-space origin.
            var camera = Matrix4x4.PointAt(new Vector3d(0, 0, -5), new Vector3d(0, 0, 0), new Vector3d(0, 1, 0));
            var view = camera.QuickInvert();
            var atOrigin = view * new Vector3d(0, 0, -5, 1);
            Assert.AreEqual(0, atOrigin.X, 1e-4);
            Assert.AreEqual(0, atOrigin.Y, 1e-4);
            Assert.AreEqual(0, atOrigin.Z, 1e-4);
        }
    }
}
