using System.Collections.Generic;
using NUnit.Framework;
using PixelSharper.Core.Components;
using PixelSharper.Core.Extensions.QuickGui;
using PixelSharper.Core.Types;

namespace PixelSharperTests
{
    // QuickGUI controls are mouse/draw-driven, so behaviour needs a running engine; these cover the
    // headless-testable surface (construction, auto-registration, theming).
    [TestFixture]
    public class QuickGuiTests
    {
        private static Vector2d<float> V(float x, float y) => new(x, y);

        [Test]
        public void Manager_ThemeDefaults_And_CopyThemeFrom()
        {
            var m = new Manager();
            Assert.AreEqual(Pixel.DARK_BLUE.N, m.ColNormal.N);
            Assert.AreEqual(8.0f, m.GrabRad);

            m.ColNormal = Pixel.CYAN;
            m.GrabRad = 12.0f;
            var copy = new Manager();
            copy.CopyThemeFrom(m);
            Assert.AreEqual(Pixel.CYAN.N, copy.ColNormal.N);
            Assert.AreEqual(12.0f, copy.GrabRad);
        }

        [Test]
        public void Controls_AutoRegisterWithManager()
        {
            var m = new Manager();
            _ = new Label(m, "Hi", V(0, 0), V(100, 20));
            _ = new Button(m, "Go", V(0, 30), V(100, 20));
            Assert.AreEqual(2, m.ControlCount);
        }

        [Test]
        public void Label_And_CheckBox_Construction()
        {
            var m = new Manager();
            var lbl = new Label(m, "Hello", V(10, 20), V(100, 30));
            Assert.AreEqual("Hello", lbl.Text);
            Assert.AreEqual(10, lbl.Pos.X);
            Assert.AreEqual(Label.Alignment.Centre, lbl.Align);

            var cb = new CheckBox(m, "On", true, V(0, 0), V(20, 20));
            Assert.IsTrue(cb.Checked);
        }

        [Test]
        public void Slider_Construction()
        {
            var s = new Slider(new Manager(), V(0, 0), V(100, 0), 0f, 10f, 5f);
            Assert.AreEqual(0, s.Min);
            Assert.AreEqual(10, s.Max);
            Assert.AreEqual(5, s.Value);
        }

        [Test]
        public void ListBox_RegistersWithOuterManager_SliderInInternalGroup()
        {
            var list = new List<string> { "a", "b", "c" };
            var m = new Manager();
            _ = new ListBox(m, list, V(0, 0), V(100, 100));
            // The ListBox registers with the outer manager; its scroll Slider lives in a private group.
            Assert.AreEqual(1, m.ControlCount);
        }

        [Test]
        public void TextBox_ForcesLeftBorderNoBackground()
        {
            var m = new Manager();
            var tb = new TextBox(m, "edit me", V(0, 0), V(120, 16));
            Assert.AreEqual("edit me", tb.Text);
            Assert.AreEqual(Label.Alignment.Left, tb.Align);
            Assert.IsTrue(tb.HasBorder);
            Assert.IsFalse(tb.HasBackground);
            Assert.AreEqual(1, m.ControlCount);
        }

        [Test]
        public void ModalDialog_ConstructsHeadlessly()
        {
            // Builds its ListBoxes + reads the filesystem root without needing a running engine.
            Assert.DoesNotThrow(() => { _ = new ModalDialog(); });
        }
    }
}
