using NUnit.Framework;
using PixelSharper.Core;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharperTests
{
    // Verifies the Span fast-paths (FillRect via Span.Fill, DrawSprite via Span.CopyTo) produce the
    // exact pixels the per-pixel path would, including clipping. Runs headless: a Sprite is set as the
    // draw target (no GL context required).
    [TestFixture]
    public class SpanDrawTests
    {
        private sealed class TestEngine : PixelGameEngine
        {
            public override bool OnCreate() => true;
            public override bool OnUpdate(float elapsedTime) => true;
        }

        private static readonly Pixel Bg = new(10, 20, 30);

        private static (TestEngine e, Sprite t) NewTarget(int w, int h)
        {
            var e = new TestEngine();
            var t = new Sprite(w, h);
            for (var i = 0; i < w * h; i++) t.PixelData[i] = Bg;
            e.SetDrawTarget(t);
            e.SetPixelMode(PixelDisplayMode.Normal);
            return (e, t);
        }

        private static Sprite MakeSprite(int w, int h, Pixel baseColour)
        {
            var s = new Sprite(w, h);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    s.SetPixel(x, y, new Pixel((byte)(baseColour.Red + x), (byte)(baseColour.Green + y), baseColour.Blue));
            return s;
        }

        [Test]
        public void FillRect_Normal_FillsRegionLeavesOutside()
        {
            var (e, t) = NewTarget(8, 8);
            e.FillRect(2, 3, 3, 2, Pixel.RED); // x in [2,5), y in [3,5)
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var inside = x is >= 2 and < 5 && y is >= 3 and < 5;
                    Assert.AreEqual(inside ? Pixel.RED.N : Bg.N, t.PixelData[y * 8 + x].N, $"({x},{y})");
                }
        }

        [Test]
        public void FillRect_ClipsToBounds()
        {
            var (e, t) = NewTarget(8, 8);
            e.FillRect(-2, -2, 4, 4, Pixel.GREEN);  // clips to [0,2)x[0,2)
            e.FillRect(6, 6, 5, 5, Pixel.BLUE);     // clips to [6,8)x[6,8)
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var expect = (x < 2 && y < 2) ? Pixel.GREEN.N : (x >= 6 && y >= 6) ? Pixel.BLUE.N : Bg.N;
                    Assert.AreEqual(expect, t.PixelData[y * 8 + x].N, $"({x},{y})");
                }
        }

        [Test]
        public void FillRect_Mask_OpaqueOverwrites_TranslucentDoesNothing()
        {
            var (e, t) = NewTarget(4, 4);
            e.SetPixelMode(PixelDisplayMode.Mask);
            e.FillRect(0, 0, 4, 4, new Pixel(200, 100, 50, 255)); // opaque -> overwrites
            Assert.AreEqual(new Pixel(200, 100, 50, 255).N, t.PixelData[0].N);

            e.FillRect(0, 0, 4, 4, new Pixel(1, 2, 3, 128));      // translucent -> Mask draws nothing
            Assert.AreEqual(new Pixel(200, 100, 50, 255).N, t.PixelData[0].N);
        }

        [Test]
        public void DrawSprite_Normal_BlitsExactPixels()
        {
            var (e, t) = NewTarget(8, 8);
            var s = MakeSprite(3, 3, new Pixel(100, 100, 100));
            e.DrawSprite(2, 2, s);
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var inSprite = x is >= 2 and < 5 && y is >= 2 and < 5;
                    var expect = inSprite ? s.GetPixel(x - 2, y - 2).N : Bg.N;
                    Assert.AreEqual(expect, t.PixelData[y * 8 + x].N, $"({x},{y})");
                }
        }

        [Test]
        public void FillRect_Alpha_MatchesPerPixelDraw()
        {
            var src = new Pixel(200, 50, 10, 128);

            // Reference: blend src over the background at one pixel via the per-pixel Draw() Alpha path.
            var (e1, t1) = NewTarget(4, 4);
            e1.SetPixelMode(PixelDisplayMode.Alpha);
            e1.Draw(1, 1, src);

            // The FillRect span blend over the whole target must produce the same pixel.
            var (e2, t2) = NewTarget(4, 4);
            e2.SetPixelMode(PixelDisplayMode.Alpha);
            e2.FillRect(0, 0, 4, 4, src);
            Assert.AreEqual(t1.PixelData[5].N, t2.PixelData[5].N, "first blend matches per-pixel Draw");

            // And blending a second time (over a non-background value) must still match.
            e1.Draw(1, 1, src);
            e2.FillRect(0, 0, 4, 4, src);
            Assert.AreEqual(t1.PixelData[5].N, t2.PixelData[5].N, "second blend matches");
        }

        [Test]
        public void DrawSprite_ClipsLeftTopAndRightBottom()
        {
            var (e, t) = NewTarget(8, 8);
            var s = MakeSprite(4, 4, new Pixel(50, 60, 70));
            e.DrawSprite(-1, -1, s);  // top-left corner clipped: dst (0,0) <- src (1,1)
            e.DrawSprite(6, 6, s);    // bottom-right clipped: dst (6,6) <- src (0,0)
            Assert.AreEqual(s.GetPixel(1, 1).N, t.PixelData[0].N, "top-left clip");
            Assert.AreEqual(s.GetPixel(0, 0).N, t.PixelData[6 * 8 + 6].N, "bottom-right clip");
            // dst(1,0) with the sprite at (-1,-1) maps to src(2,1).
            Assert.AreEqual(s.GetPixel(2, 1).N, t.PixelData[0 * 8 + 1].N, "top edge");
        }
    }
}
