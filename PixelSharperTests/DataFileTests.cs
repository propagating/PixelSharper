using System.IO;
using NUnit.Framework;
using PixelSharper.Core.Utilities;

namespace PixelSharperTests
{
    [TestFixture]
    public class DataFileTests
    {
        [Test]
        public void Values_String_Real_Int_AndCount()
        {
            var df = new DataFile();
            df["name"].SetString("Bob");
            df["age"].SetInt(42);
            df["height"].SetReal(1.85);
            df["pos"].SetReal(1.0, 0);
            df["pos"].SetReal(2.0, 1);

            Assert.AreEqual("Bob", df["name"].GetString());
            Assert.AreEqual(42, df["age"].GetInt());
            Assert.AreEqual(1.85, df["height"].GetReal(), 1e-9);
            Assert.AreEqual(2, df["pos"].GetValueCount());
            Assert.AreEqual(2.0, df["pos"].GetReal(1), 1e-9);

            Assert.IsTrue(df.HasProperty("name"));
            Assert.IsFalse(df.HasProperty("missing"));
            Assert.AreEqual("", df["missing"].GetString()); // missing value -> empty/default
        }

        [Test]
        public void DottedPath_Access()
        {
            var df = new DataFile();
            df["a"]["b"]["c"].SetString("deep");
            Assert.AreEqual("deep", df.GetProperty("a.b.c").GetString());
            Assert.AreEqual("deep", df["a"]["b"]["c"].GetString());
        }

        [Test]
        public void RoundTrip_WriteThenRead()
        {
            var df = new DataFile();
            df["player"]["name"].SetString("Alice");
            df["player"]["score"].SetInt(100);
            df["player"]["pos"].SetReal(1.5, 0);
            df["player"]["pos"].SetReal(2.5, 1);
            df["title"].SetString("Hello, World"); // contains the separator -> must be quoted

            var tmp = Path.GetTempFileName();
            try
            {
                Assert.IsTrue(DataFile.Write(df, tmp));

                var loaded = new DataFile();
                Assert.IsTrue(DataFile.Read(loaded, tmp));

                Assert.AreEqual("Alice", loaded["player"]["name"].GetString());
                Assert.AreEqual(100, loaded["player"]["score"].GetInt());
                Assert.AreEqual(1.5, loaded["player"]["pos"].GetReal(0), 1e-6);
                Assert.AreEqual(2.5, loaded["player"]["pos"].GetReal(1), 1e-6);
                Assert.AreEqual("Hello, World", loaded["title"].GetString());
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Test]
        public void Read_MissingFile_ReturnsFalse()
        {
            Assert.IsFalse(DataFile.Read(new DataFile(), Path.Combine(Path.GetTempPath(), "definitely-not-here-12345.dat")));
        }
    }
}
