using NUnit.Framework;
using PixelSharper.Core;
using PixelSharper.Core.Enums;

namespace PixelSharperTests
{
    // Covers the headless-testable surface of the newly-backfilled engine subsystems (the typing
    // loop itself, UpdateTextEntry, is driven by the live frame loop and exercised via the demo).
    [TestFixture]
    public class TextEntryTests
    {
        private sealed class HeadlessEngine : PixelGameEngine
        {
            public override bool OnCreate() => true;
            public override bool OnUpdate(float elapsedTime) => true;
        }

        private static HeadlessEngine Engine() => new();

        [Test]
        public void KeySymbol_LettersDigitsAndModifiers()
        {
            var e = Engine();
            Assert.AreEqual("a", e.GetKeySymbol(KeyPress.A));
            Assert.AreEqual("A", e.GetKeySymbol(KeyPress.A, shift: true));
            Assert.AreEqual("z", e.GetKeySymbol(KeyPress.Z));
            Assert.AreEqual("1", e.GetKeySymbol(KeyPress.K1));
            Assert.AreEqual("!", e.GetKeySymbol(KeyPress.K1, shift: true));
            Assert.AreEqual(" ", e.GetKeySymbol(KeyPress.SPACE));
            Assert.AreEqual("", e.GetKeySymbol(KeyPress.SHIFT)); // modifier produces no character
        }

        [Test]
        public void KeySymbol_NavigationCommandCodes()
        {
            var e = Engine();
            Assert.AreEqual("\b", e.GetKeySymbol(KeyPress.BACK));
            Assert.AreEqual("\n", e.GetKeySymbol(KeyPress.ENTER));
            Assert.AreEqual("_X", e.GetKeySymbol(KeyPress.DEL));
            Assert.AreEqual("_L", e.GetKeySymbol(KeyPress.LEFT));
            Assert.AreEqual("_R", e.GetKeySymbol(KeyPress.RIGHT));
            Assert.AreEqual("_U", e.GetKeySymbol(KeyPress.UP));
            Assert.AreEqual("_D", e.GetKeySymbol(KeyPress.DOWN));
        }

        [Test]
        public void TextEntryEnable_SeedsBufferAndCursor()
        {
            var e = Engine();
            Assert.IsFalse(e.IsTextEntryEnabled());

            e.TextEntryEnable(true, "hello");
            Assert.IsTrue(e.IsTextEntryEnabled());
            Assert.AreEqual("hello", e.TextEntryGetString());
            Assert.AreEqual(5, e.TextEntryGetCursor());

            e.TextEntryEnable(false);
            Assert.IsFalse(e.IsTextEntryEnabled());
        }

        [Test]
        public void Accessors_And_ConsoleDefaults()
        {
            var e = Engine();
            Assert.IsFalse(e.IsConsoleShowing());
            Assert.AreEqual(0u, e.GetFPS()); // no frames run yet
            Assert.AreEqual(KeyPress.NONE, e.ConvertKeycode(0));
            Assert.AreEqual(KeyPress.A, e.ConvertKeycode((int)KeyPress.A));
            Assert.IsNotNull(e.ConsoleOut()); // a writable output sink exists
        }
    }
}
