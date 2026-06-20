using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharperTests
{
    // Guards the deferred texture-deletion path. GL calls are only valid on the GL-context thread, but
    // ~Decal() runs on the GC finalizer thread; calling GL there throws AccessViolationException. So the
    // finalizer enqueues the id and the engine drains it on the GL thread via ProcessPendingTextureDeletes.
    [TestFixture]
    public class RendererTests
    {
        private sealed class MockRenderer : Renderer
        {
            public readonly List<uint> Deleted = new();
            public override uint DeleteTexture(uint id) { Deleted.Add(id); return id; }

            // Unused stubs for this fixture.
            public override void PrepareDevice() { }
            public override FileReadCode CreateDevice(List<object> p, bool fs, bool vs) => FileReadCode.Ok;
            public override FileReadCode DestroyDevice() => FileReadCode.Ok;
            public override void DisplayFrame() { }
            public override void PrepareDrawing() { }
            public override void SetDecalMode(DecalMode mode) { }
            public override void DrawLayerQuad(Vector2d<float> o, Vector2d<float> s, Pixel t) { }
            public override void DrawDecal(DecalInstance d) { }
            public override void DoGPUTask(GPUTask t) { }
            public override void Set3DProjection(float[] m) { }
            public override int CreateTexture(Vector2d<int> size, bool filtered = false, bool clamp = true) => 1;
            public override void UpdateTexture(uint id, Sprite sprite) { }
            public override void ReadTexture(uint id, Sprite sprite) { }
            public override void ApplyTexture(uint id) { }
            public override void UpdateViewport(Vector2d<int> pos, Vector2d<int> size) { }
            public override void ClearBuffer(Pixel p, bool depth) { }
        }

        private MockRenderer _mock = null!;
        private Renderer? _previous;

        [SetUp]
        public void SetUp()
        {
            _previous = Renderer.Active;
            _mock = new MockRenderer();
            Renderer.Active = _mock;
            // Drain any ids left in the shared static queue by other tests' finalizers.
            _mock.ProcessPendingTextureDeletes();
            _mock.Deleted.Clear();
        }

        [TearDown]
        public void TearDown() => Renderer.Active = _previous!;

        [Test]
        public void ScheduleTextureDelete_DefersUntilProcessOnGlThread()
        {
            Renderer.ScheduleTextureDelete(4242);

            // Enqueuing must NOT delete immediately (that is the whole point — no GL off the GL thread).
            Assert.IsFalse(_mock.Deleted.Contains(4242), "scheduling must defer, not delete");

            _mock.ProcessPendingTextureDeletes();
            Assert.IsTrue(_mock.Deleted.Contains(4242), "draining performs the delete on the GL thread");
        }

        [Test]
        public void DecalFinalizer_SchedulesDeleteInsteadOfCallingGlDirectly()
        {
            // A Decal holding a texture id, then made unreachable. Its finalizer must enqueue the id
            // (not call DeleteTexture directly), so nothing is deleted until we drain on the GL thread.
            CreateAndAbandonDecal(98765);

            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            Assert.IsFalse(_mock.Deleted.Contains(98765), "finalizer must not delete directly");
            _mock.ProcessPendingTextureDeletes();
            Assert.IsTrue(_mock.Deleted.Contains(98765), "queued id is freed on the GL thread");
        }

        // Separate non-inlined method so the Decal has no live reference once it returns.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CreateAndAbandonDecal(int id)
        {
            var d = new Decal(id, new Sprite(2, 2));
            Assert.AreEqual(id, d.Id);
        }

        // The OGL33 backend composes projection x MVP on the CPU per GPU task (olc's matMVP loop). The
        // rest of RendererOgl33 is raw GL and needs a live context, but this column-major multiply is
        // pure arithmetic and is the one piece worth pinning down without a window.
        [Test]
        public void Ogl33_MultiplyProjection_Identity_LeavesMvpUnchanged()
        {
            // Arrange
            var identity = new float[16] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            var mvp = new float[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

            // Act
            var result = PixelSharper.Core.Renderers.RendererOgl33.MultiplyProjection(identity, mvp);

            // Assert
            Assert.AreEqual(mvp, result);
        }

        [Test]
        public void Ogl33_MultiplyProjection_ScaleTimesScale_MultipliesDiagonals()
        {
            // Arrange — column-major uniform-scale matrices diag(2) and diag(3) (w stays 1).
            var proj = new float[16] { 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1 };
            var mvp = new float[16] { 3, 0, 0, 0, 0, 3, 0, 0, 0, 0, 3, 0, 0, 0, 0, 1 };

            // Act
            var result = PixelSharper.Core.Renderers.RendererOgl33.MultiplyProjection(proj, mvp);

            // Assert — product is diag(6) with w == 1.
            var expected = new float[16] { 6, 0, 0, 0, 0, 6, 0, 0, 0, 0, 6, 0, 0, 0, 0, 1 };
            Assert.AreEqual(expected, result);
        }
    }
}
