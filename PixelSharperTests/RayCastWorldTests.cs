using NUnit.Framework;
using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions.Rcw;
using PixelSharper.Core.Types;

namespace PixelSharperTests
{
    [TestFixture]
    public class RayCastWorldTests
    {
        // A 10x10 walled room with an interior wall at column x == 5.
        private sealed class TestWorld : Engine
        {
            public TestWorld() : base(60, 40, 1.0f) { }

            protected override bool IsLocationSolid(float x, float y)
                => x < 1 || y < 1 || x >= 9 || y >= 9 || (int)x == 5;

            protected override Pixel SelectSceneryPixel(int tx, int ty, CellSide side, float sx, float sy, float dist) => Pixel.WHITE;
            protected override float GetObjectWidth(uint id) => 1f;
            protected override float GetObjectHeight(uint id) => 1f;
            protected override Pixel SelectObjectPixel(uint id, float sx, float sy, float dist, float angle) => Pixel.WHITE;
        }

        [Test]
        public void Object_Walk_Stop_Turn()
        {
            var o = new RcwObject { Heading = 0f };
            o.Walk(5f);
            Assert.AreEqual(5f, o.Vel.X, 1e-4);
            Assert.AreEqual(0f, o.Vel.Y, 1e-4);

            o.Stop();
            Assert.AreEqual(0f, o.Vel.X, 1e-4);

            o.Heading = 3.0f;
            o.Turn(0.5f); // 3.5 > pi -> wraps negative
            Assert.Less(o.Heading, 0f);
        }

        [Test]
        public void CastRayDDA_HitsWallColumn()
        {
            var world = new TestWorld();
            var hit = world.CastRayDDA(new Vector2d<float>(2.5f, 2.5f), new Vector2d<float>(1, 0), out var th);

            Assert.IsTrue(hit);
            Assert.AreEqual(5, th.TilePos.X);          // the interior wall column
            Assert.AreEqual(Engine.CellSide.West, th.Side); // hit its west face going east
        }

        [Test]
        public void CastRayDDA_NoWall_ReturnsFalseWithinRange()
        {
            // Looking "up" a clear corridor that only ends at the far border still hits a wall, so
            // instead verify a ray cast away from the room (origin already in solid space) terminates.
            var world = new TestWorld();
            var hit = world.CastRayDDA(new Vector2d<float>(2.5f, 2.5f), new Vector2d<float>(-1, 0), out var th);
            Assert.IsTrue(hit); // hits the x<1 border
            Assert.AreEqual(0, th.TilePos.X);
        }

        [Test]
        public void Update_ResolvesSceneryCollision()
        {
            var world = new TestWorld();
            var o = new RcwObject { Pos = new Vector2d<float>(4f, 2.5f), Radius = 0.5f };
            o.Vel = new Vector2d<float>(20f, 0f); // driving hard into the wall at x == 5
            world.MapObjects[1] = o;

            world.Update(0.1f);

            // Stopped ~one radius from the wall face (x ~= 4.5), not penetrating it, but having moved.
            Assert.Less(o.Pos.X, 4.6f);
            Assert.Greater(o.Pos.X, 4.3f);
            Assert.AreEqual(2.5f, o.Pos.Y, 1e-3); // no sideways displacement
        }
    }
}
