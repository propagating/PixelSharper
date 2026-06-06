using System.IO;
using NUnit.Framework;
using PixelSharper.Core.Extensions.Audio;

namespace PixelSharperTests
{
    // Covers the headless-testable surface (WAV loader + software mixer). The OpenAL output path
    // needs real audio hardware, so it's build-verified only.
    [TestFixture]
    public class SoundTests
    {
        private static string WriteWav(short[] samples, short channels = 1, int rate = 44100)
        {
            var path = Path.GetTempFileName();
            using var bw = new BinaryWriter(File.Open(path, FileMode.Create));
            const short bits = 16;
            var dataSize = samples.Length * 2;
            bw.Write("RIFF".ToCharArray());
            bw.Write(36 + dataSize);
            bw.Write("WAVE".ToCharArray());
            bw.Write("fmt ".ToCharArray());
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write(channels);
            bw.Write(rate);
            bw.Write(rate * channels * bits / 8);
            bw.Write((short)(channels * bits / 8));
            bw.Write(bits);
            bw.Write("data".ToCharArray());
            bw.Write(dataSize);
            foreach (var s in samples) bw.Write(s);
            return path;
        }

        [Test]
        public void AudioSample_LoadsAndNormalisesWav()
        {
            var path = WriteWav(new short[] { 0, short.MaxValue, 16384 });
            try
            {
                var a = new AudioSample(path);
                Assert.IsTrue(a.Valid);
                Assert.AreEqual(1, a.NChannels);
                Assert.AreEqual(3, a.NSamples);
                Assert.AreEqual(44100, a.SampleRate);
                Assert.AreEqual(0f, a.Sample[0], 1e-4);
                Assert.AreEqual(1f, a.Sample[1], 1e-4);
                Assert.AreEqual(0.5f, a.Sample[2], 1e-3);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void LoadAudioSample_ReturnsIdOrMinusOne()
        {
            Assert.AreEqual(-1, Sound.LoadAudioSample("definitely-not-a-file.wav"));
            var path = WriteWav(new short[] { 100, 200 });
            try { Assert.Greater(Sound.LoadAudioSample(path), 0); }
            finally { File.Delete(path); }
        }

        [Test]
        public void Mixer_AppliesSynthThenFilter()
        {
            Sound.SetUserSynthFunction((ch, t, dt) => 0.5f);
            Sound.SetUserFilterFunction(null);
            Assert.AreEqual(0.5f, Sound.GetMixerOutput(0, 0f, 0f), 1e-5);

            Sound.SetUserFilterFunction((ch, t, sample) => sample * 2f);
            Assert.AreEqual(1.0f, Sound.GetMixerOutput(0, 0f, 0f), 1e-5);

            // Reset shared static state so other tests aren't affected.
            Sound.SetUserSynthFunction(null);
            Sound.SetUserFilterFunction(null);
        }
    }
}
