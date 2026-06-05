using NUnit.Framework;
using PixelSharper.Core.Extensions.PopUp;

namespace PixelSharperTests
{
    [TestFixture]
    public class PopUpMenuTests
    {
        private static Menu BuildMenu()
        {
            var root = new Menu("root");
            root.SetTable(1, 5);
            _ = root["Play"];
            root["Options"].SetTable(1, 3);
            _ = root["Options"]["Sound"];
            _ = root["Options"]["Video"];
            root["Quit"].SetID(99);
            root["Disabled"].Enable(false);
            root.Build();
            return root;
        }

        [Test]
        public void Indexer_CreatesAndReuses()
        {
            var root = BuildMenu();
            Assert.IsTrue(root["Options"].HasChildren());
            Assert.IsFalse(root["Play"].HasChildren());
            Assert.AreEqual(99, root["Quit"].GetID());
        }

        [Test]
        public void Navigate_And_ConfirmLeaf_ReturnsItem()
        {
            var man = new Manager();
            man.Open(BuildMenu());

            man.OnDown(); // Options
            man.OnDown(); // Quit
            var chosen = man.OnConfirm();

            Assert.IsNotNull(chosen);
            Assert.AreEqual("Quit", chosen.GetName());
            Assert.AreEqual(99, chosen.GetID());
        }

        [Test]
        public void Confirm_OnSubmenu_DescendsThenSelects()
        {
            var man = new Manager();
            man.Open(BuildMenu());

            man.OnDown();                       // Options (a submenu)
            Assert.IsNull(man.OnConfirm());     // descends -> no selection yet

            var chosen = man.OnConfirm();       // Sound (first item of submenu)
            Assert.AreEqual("Sound", chosen.GetName());

            man.OnBack();                       // back to root; nothing crashes
        }

        [Test]
        public void Confirm_OnDisabledItem_ReturnsNull()
        {
            var man = new Manager();
            man.Open(BuildMenu());

            man.OnDown(); // Options
            man.OnDown(); // Quit
            man.OnDown(); // Disabled
            Assert.IsNull(man.OnConfirm());
        }

        [Test]
        public void Navigation_ClampsAtEnds()
        {
            var man = new Manager();
            man.Open(BuildMenu());

            // Up at the top stays at the first item.
            man.OnUp();
            man.OnUp();
            Assert.AreEqual("Play", man.OnConfirm().GetName());

            // Down past the end clamps to the last item.
            for (var i = 0; i < 10; i++) man.OnDown();
            Assert.IsNull(man.OnConfirm()); // last item is Disabled -> null
        }
    }
}
