using System;
using NUnit.Framework;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Hardware3D;

namespace PixelSharperTests
{
    [TestFixture]
    public class Hardware3DTests
    {
        [Test]
        public void CreateCube_Has36Vertices()
        {
            var m = Hw3d.CreateCube(new Vector3d(1, 1, 1));
            Assert.AreEqual(36, m.Pos.Count); // 6 faces * 2 tris * 3 verts
            Assert.AreEqual(36, m.Uv.Count);
            Assert.AreEqual(36, m.Norm.Count);
            Assert.AreEqual(36, m.Col.Count);
            Assert.AreEqual(DecalStructure.List, m.Layout);
            Assert.AreEqual(36, Hw3d.CreateSanityCube().Pos.Count);
        }

        [Test]
        public void RayVsTriangle_HitsAndDistances()
        {
            var hit = Hw3d.RayVsTriangle(
                new Vector3d(0.25f, 0.25f, -1), new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            Assert.IsTrue(hit.HasValue);
            Assert.AreEqual(0, hit!.Value.Point.Z, 1e-4);
            Assert.AreEqual(1, hit.Value.T, 1e-4);

            // Ray that misses the triangle.
            var miss = Hw3d.RayVsTriangle(
                new Vector3d(5, 5, -1), new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            Assert.IsFalse(miss.HasValue);
        }

        [Test]
        public void RayVsMesh_HitsCube()
        {
            var cube = Hw3d.CreateCube(new Vector3d(2, 2, 2), new Vector3d(-1, -1, -1)); // centred on origin
            var hits = Hw3d.RayVsMesh(new Vector3d(-0.5f, 0.5f, -5), new Vector3d(0, 0, 1), cube);
            Assert.GreaterOrEqual(hits.Count, 1);
        }

        [Test]
        public void RayVsPlane_Intersects()
        {
            var p = Hw3d.RayVsPlane(new Vector3d(0, 5, 0), new Vector3d(0, -1, 0), new Vector3d(0, 0, 0), new Vector3d(0, 1, 0));
            Assert.IsTrue(p.HasValue);
            Assert.AreEqual(0, p!.Value.Y, 1e-4); // meets the y=0 plane
        }

        [Test]
        public void SimpleFps_Forwards_MovesAlongView()
        {
            var cam = new Camera3DSimpleFps(); // default looks toward +z
            cam.Forwards(1f);
            Assert.AreEqual(1, cam.GetPosition().Z, 1e-4);
        }

        [Test]
        public void Orbit_Zoom_ScalesDistance()
        {
            var cam = new Camera3DOrbit();
            var d0 = cam.GetDistance();
            cam.Zoom(0.5f);
            Assert.AreEqual(d0 * 0.5f, cam.GetDistance(), 1e-4);
        }

        [Test]
        public void ScreenRay_IsNormalised()
        {
            var cam = new Camera3D();
            cam.SetFieldOfView(MathF.PI / 2);
            cam.SetScreenSize(new Vector2d<int>(640, 480));
            var ray = cam.ScreenRay(new Vector2d<float>(320, 240));
            Assert.AreEqual(1f, ray.Mag(), 1e-3);
        }
    }
}
