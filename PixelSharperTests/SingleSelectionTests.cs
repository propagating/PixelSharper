using NUnit.Framework;
using PixelSharper.Core.Utilities;

namespace PixelSharperTests
{
    [TestFixture]
    public class SingleSelectionTests
    {
        private static SingleSelection<int> Make()
        {
            var s = new SingleSelection<int>();
            s.Add(10);
            s.Add(20);
            s.Add(30);
            return s;
        }

        [Test]
        public void Select_TracksAndClamps()
        {
            var s = Make();
            s.Select(1);
            Assert.AreEqual(1, s.Selection);
            Assert.AreEqual(20, s.Selected);

            s.Select(99); // clamps to last
            Assert.AreEqual(2, s.Selection);
            s.Select(-5); // clamps to first
            Assert.AreEqual(0, s.Selection);
        }

        [Test]
        public void MoveUp_MovesSelectedTowardEnd()
        {
            var s = Make();
            s.Select(1); // value 20
            s.MoveUp();
            Assert.AreEqual(2, s.Selection);
            Assert.AreEqual(20, s.Selected);
            CollectionAssert.AreEqual(new[] { 10, 30, 20 }, s);
        }

        [Test]
        public void MoveDown_MovesSelectedTowardStart()
        {
            var s = Make();
            s.Select(2); // value 30
            s.MoveDown();
            Assert.AreEqual(1, s.Selection);
            Assert.AreEqual(30, s.Selected);
            CollectionAssert.AreEqual(new[] { 10, 30, 20 }, s);
        }

        [Test]
        public void MoveUp_AtEnd_DoesNothing()
        {
            var s = Make();
            s.Select(2); // last
            s.MoveUp();
            Assert.AreEqual(2, s.Selection);
            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, s);
        }
    }
}
