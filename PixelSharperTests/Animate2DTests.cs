using NUnit.Framework;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Animate2D;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharperTests
{
    [TestFixture]
    public class Animate2DTests
    {
        // Source-less frames tagged by their rect X so we can identify which frame was selected.
        private static Frame Tagged(int id) => new(null, new Rect<int>(new Vector2d<int>(id, 0), new Vector2d<int>(8, 8)));

        private static FrameSequence Seq(Style style, int count)
        {
            var s = new FrameSequence(0.1f, style);
            for (var i = 0; i < count; i++) s.AddFrame(Tagged(i));
            return s;
        }

        [Test]
        public void Repeat_WrapsAround()
        {
            var s = Seq(Style.Repeat, 3);
            Assert.AreEqual(0, s.GetFrame(0.0f).SourceRect.Pos.X);
            Assert.AreEqual(1, s.GetFrame(0.15f).SourceRect.Pos.X);
            Assert.AreEqual(0, s.GetFrame(0.35f).SourceRect.Pos.X); // 3 % 3 -> 0
        }

        [Test]
        public void OneShot_HoldsOnLastFrame()
        {
            var s = Seq(Style.OneShot, 3);
            Assert.AreEqual(2, s.GetFrame(1.0f).SourceRect.Pos.X); // clamp(10, 0, 2)
        }

        [Test]
        public void Reverse_CountsDown()
        {
            var s = Seq(Style.Reverse, 3);
            Assert.AreEqual(2, s.GetFrame(0.0f).SourceRect.Pos.X);
            Assert.AreEqual(1, s.GetFrame(0.15f).SourceRect.Pos.X);
        }

        [Test]
        public void Complete_AtSequenceDuration()
        {
            var s = Seq(Style.OneShot, 3);
            Assert.IsTrue(s.Complete(0.3f));   // 3 frames * 0.1s
            Assert.IsFalse(s.Complete(0.1f));
        }

        private enum St { Idle, Walk }

        [Test]
        public void Animation_StateMachine_SwitchesAndAdvances()
        {
            var anim = new Animation<St>();
            var idle = new FrameSequence(0.1f); idle.AddFrame(Tagged(0));
            var walk = new FrameSequence(0.1f); walk.AddFrame(Tagged(5)); walk.AddFrame(Tagged(6));
            anim.AddState(St.Idle, idle);
            anim.AddState(St.Walk, walk);

            var state = new AnimationState();
            Assert.AreEqual(0, anim.GetFrame(state).SourceRect.Pos.X); // default state 0 = Idle

            Assert.IsTrue(anim.ChangeState(state, St.Walk));
            Assert.IsFalse(anim.ChangeState(state, St.Walk)); // already there
            Assert.AreEqual(5, anim.GetFrame(state).SourceRect.Pos.X);

            anim.UpdateState(state, 0.15f);
            Assert.AreEqual(6, anim.GetFrame(state).SourceRect.Pos.X);
        }
    }
}
