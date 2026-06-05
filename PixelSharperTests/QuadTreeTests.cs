using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharperTests
{
    [TestFixture]
    public class QuadTreeTests
    {
        private static Vector2d<float> V(float x, float y) => new(x, y);
        private static Rect<float> R(float x, float y, float w, float h) => new(V(x, y), V(w, h));

        [Test]
        public void DynamicQuadTree_SearchReturnsOverlappingItems()
        {
            var tree = new DynamicQuadTree<int>(R(0, 0, 100, 100));
            tree.Insert(1, R(10, 10, 5, 5));
            tree.Insert(2, R(90, 90, 5, 5));
            Assert.AreEqual(2, tree.Size());

            var res = new List<int>();
            tree.Search(R(0, 0, 50, 50), res);
            Assert.Contains(1, res);
            Assert.IsFalse(res.Contains(2));
        }

        [Test]
        public void Container_Insert_Search_Enumerate()
        {
            var qt = new QuadTreeContainer<string>(R(0, 0, 100, 100));
            qt.Insert("a", R(10, 10, 5, 5));
            qt.Insert("b", R(80, 80, 5, 5));
            qt.Insert("c", R(12, 12, 3, 3));

            Assert.AreEqual(3, qt.Size);
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, qt.ToList());

            var near = qt.Search(R(5, 5, 15, 15));
            Assert.Contains("a", near);
            Assert.Contains("c", near);
            Assert.IsFalse(near.Contains("b"));

            Assert.Contains("b", qt.Search(R(75, 75, 20, 20)));
        }

        [Test]
        public void Container_Remove()
        {
            var qt = new QuadTreeContainer<string>(R(0, 0, 100, 100));
            qt.Insert("a", R(10, 10, 5, 5));
            var hb = qt.Insert("b", R(80, 80, 5, 5));

            qt.Remove(hb);
            Assert.AreEqual(1, qt.Size);
            Assert.IsFalse(qt.Search(R(75, 75, 20, 20)).Contains("b"));
            CollectionAssert.AreEquivalent(new[] { "a" }, qt.ToList());
        }

        [Test]
        public void Container_Relocate_MovesItemSpatially()
        {
            var qt = new QuadTreeContainer<string>(R(0, 0, 100, 100));
            var ha = qt.Insert("a", R(10, 10, 5, 5));

            qt.Relocate(ha, R(80, 80, 5, 5));

            Assert.Contains("a", qt.Search(R(75, 75, 20, 20)));
            Assert.IsFalse(qt.Search(R(0, 0, 12, 12)).Contains("a"));
            Assert.AreEqual(1, qt.Size); // relocate doesn't duplicate
        }
    }
}
