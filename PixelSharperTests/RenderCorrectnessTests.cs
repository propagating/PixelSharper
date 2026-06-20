using NUnit.Framework;
using PixelSharper.Core;
using PixelSharper.Core.Components;

namespace PixelSharperTests
{
    // Headless render-correctness harness for the SOFTWARE rasterizer. The Draw* primitives write
    // straight into the current draw-target Sprite (pure CPU — no GL, no window), so the harness
    // points a bare PixelGameEngine at a Sprite, draws, and asserts exact pixels. This pins down the
    // rasterizer's output so CI catches regressions (the GL backends need a live context; this does
    // not). Assertions stay off boundary edges so they don't depend on inclusive/exclusive fill rules.
    [TestFixture]
    public class RenderCorrectnessTests
    {
        // A minimal concrete engine: no lifecycle, no window — just a draw surface.
        private sealed class Harness : PixelGameEngine
        {
            public override bool OnCreate() => true;
            public override bool OnUpdate(float elapsedTime) => true;
        }

        private const int W = 32;
        private const int H = 24;
        private Harness _engine = null!;
        private Sprite _target = null!;

        [SetUp]
        public void SetUp()
        {
            _engine = new Harness();
            _target = new Sprite(W, H);
            _engine.SetDrawTarget(_target);
            _engine.Clear(Pixel.BLACK);
        }

        /// <summary>Packed colour of the target pixel at (x, y).</summary>
        private uint At(int x, int y) => _target.GetPixel(x, y).N;

        [Test]
        public void Clear_FillsEntireTarget()
        {
            _engine.Clear(Pixel.RED);

            Assert.AreEqual(Pixel.RED.N, At(0, 0));
            Assert.AreEqual(Pixel.RED.N, At(W - 1, H - 1));
            Assert.AreEqual(Pixel.RED.N, At(W / 2, H / 2));
        }

        [Test]
        public void Draw_SetsOnlyTheTargetPixel()
        {
            _engine.Draw(5, 7, Pixel.WHITE);

            Assert.AreEqual(Pixel.WHITE.N, At(5, 7));
            Assert.AreEqual(Pixel.BLACK.N, At(5, 8), "neighbour below untouched");
            Assert.AreEqual(Pixel.BLACK.N, At(6, 7), "neighbour right untouched");
        }

        [Test]
        public void Draw_OutOfBounds_IsClippedNotThrowing()
        {
            // Off-target writes must be silently clipped (SetPixel bounds-checks), not throw.
            Assert.DoesNotThrow(() =>
            {
                _engine.Draw(-1, -1, Pixel.WHITE);
                _engine.Draw(W, H, Pixel.WHITE);
                _engine.Draw(1000, 1000, Pixel.WHITE);
            });
        }

        [Test]
        public void DrawLine_Horizontal_SetsTheRowOnly()
        {
            _engine.DrawLine(4, 10, 20, 10, Pixel.WHITE);

            Assert.AreEqual(Pixel.WHITE.N, At(4, 10), "start");
            Assert.AreEqual(Pixel.WHITE.N, At(12, 10), "middle");
            Assert.AreEqual(Pixel.WHITE.N, At(20, 10), "end");
            Assert.AreEqual(Pixel.BLACK.N, At(12, 11), "row below untouched");
            Assert.AreEqual(Pixel.BLACK.N, At(3, 10), "before start untouched");
        }

        [Test]
        public void DrawLine_Diagonal_SetsTheDiagonal()
        {
            _engine.DrawLine(0, 0, 10, 10, Pixel.WHITE);

            for (var i = 0; i <= 10; i++)
                Assert.AreEqual(Pixel.WHITE.N, At(i, i), $"diagonal pixel {i}");
            Assert.AreEqual(Pixel.BLACK.N, At(0, 5), "off-diagonal untouched");
        }

        [Test]
        public void FillRect_FillsInterior_NotOutside()
        {
            _engine.FillRect(4, 4, 6, 6, Pixel.GREEN); // covers x in [4,10), y in [4,10)

            Assert.AreEqual(Pixel.GREEN.N, At(4, 4), "fill origin");
            Assert.AreEqual(Pixel.GREEN.N, At(6, 6), "clearly inside");
            Assert.AreEqual(Pixel.GREEN.N, At(9, 9), "last inside cell");
            Assert.AreEqual(Pixel.BLACK.N, At(10, 10), "just outside");
            Assert.AreEqual(Pixel.BLACK.N, At(3, 3), "before origin");
        }

        [Test]
        public void DrawRect_DrawsBorder_NotInterior()
        {
            _engine.DrawRect(4, 4, 8, 8, Pixel.BLUE); // border around x:4..12, y:4..12

            Assert.AreEqual(Pixel.BLUE.N, At(4, 4), "corner");
            Assert.AreEqual(Pixel.BLUE.N, At(12, 12), "opposite corner");
            Assert.AreEqual(Pixel.BLUE.N, At(8, 4), "top edge");
            Assert.AreEqual(Pixel.BLUE.N, At(4, 8), "left edge");
            Assert.AreEqual(Pixel.BLACK.N, At(8, 8), "interior empty");
        }

        [Test]
        public void FillCircle_FillsCentre_NotFarCorner()
        {
            _engine.FillCircle(16, 12, 6, Pixel.WHITE);

            Assert.AreEqual(Pixel.WHITE.N, At(16, 12), "centre");
            Assert.AreEqual(Pixel.WHITE.N, At(16, 7), "near top edge (dist 5 <= 6)");
            Assert.AreEqual(Pixel.WHITE.N, At(11, 12), "near left edge (dist 5 <= 6)");
            Assert.AreEqual(Pixel.BLACK.N, At(0, 0), "far corner outside");
        }

        [Test]
        public void FillTriangle_FillsInterior_NotExterior()
        {
            // A wide triangle pointing down: top edge y=2 (x 4..28), apex at (16,20).
            _engine.FillTriangle(4, 2, 28, 2, 16, 20, Pixel.WHITE);

            Assert.AreEqual(Pixel.WHITE.N, At(16, 8), "interior near centroid");
            Assert.AreEqual(Pixel.BLACK.N, At(2, 18), "exterior lower-left");
            Assert.AreEqual(Pixel.BLACK.N, At(1, 1), "exterior corner");
        }
    }
}
