using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OpenTK.Audio.OpenAL;

namespace PixelSharper.Core.Extensions.Audio;

// Port of olcPGEX_Sound (olc::SOUND) using olc's USE_OPENAL backend (via OpenTK.Audio.OpenAL). A
// background thread runs the software mixer (GetMixerOutput) — combining all playing samples plus an
// optional user synth/filter — and streams blocks of 16-bit PCM into a single OpenAL streaming
// source's buffer queue. Loads 16-bit / 44100 Hz WAV files only (olc's restriction).
public class AudioSample
{
    public int SampleRate;
    public int NChannels;
    public long NSamples;
    public float[] Sample;
    public bool Valid;

    public AudioSample() { }
    public AudioSample(string wavFile) => LoadFromFile(wavFile);

    public bool LoadFromFile(string wavFile)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(wavFile));
            if (new string(br.ReadChars(4)) != "RIFF") return false;
            br.ReadInt32();                                   // overall size (ignored)
            if (new string(br.ReadChars(4)) != "WAVE") return false;
            if (new string(br.ReadChars(4)) != "fmt ") return false;

            var headerSize = br.ReadInt32();
            var fmt = br.ReadBytes(headerSize);
            var channels = BitConverter.ToInt16(fmt, 2);
            var samplesPerSec = BitConverter.ToInt32(fmt, 4);
            var bitsPerSample = BitConverter.ToInt16(fmt, 14);
            if (bitsPerSample != 16 || samplesPerSec != 44100) return false;

            // Skip non-"data" chunks until we reach the audio data.
            var chunkName = new string(br.ReadChars(4));
            var chunkSize = br.ReadInt32();
            while (chunkName != "data")
            {
                br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                chunkName = new string(br.ReadChars(4));
                chunkSize = br.ReadInt32();
            }

            NChannels = channels;
            SampleRate = samplesPerSec;
            NSamples = chunkSize / (channels * (bitsPerSample / 8));
            Sample = new float[NSamples * channels];
            for (long i = 0; i < NSamples; i++)
                for (var c = 0; c < channels; c++)
                    Sample[i * channels + c] = br.ReadInt16() / (float)short.MaxValue;

            Valid = true;
            return true;
        }
        catch (IOException) { return false; }
    }
}

public static class Sound
{
    private sealed class PlayingSample
    {
        public int AudioSampleId;
        public long SamplePosition;
        public bool Finished;
        public bool Loop;
        public bool FlagForStop;
    }

    private static readonly object Lock = new();
    private static readonly List<PlayingSample> ActiveSamples = new();
    private static readonly List<AudioSample> AudioSamples = new();
    private static Func<int, float, float, float>? _userSynth;
    private static Func<int, float, float, float>? _userFilter;

    private static volatile bool _audioThreadActive;
    private static float _globalTime;
    private static Thread _audioThread;

    // OpenAL
    private static ALDevice _device;
    private static ALContext _context;
    private static int[] _buffers;
    private static int _source;
    private static readonly Queue<int> AvailableBuffers = new();
    private static int _sampleRate, _channels, _blockSamples;
    private static short[] _blockMemory;

    public static void SetUserSynthFunction(Func<int, float, float, float>? func) => _userSynth = func;
    public static void SetUserFilterFunction(Func<int, float, float, float>? func) => _userFilter = func;

    // Loads a 16-bit/44100Hz WAV into memory; returns a 1-based sample id, or -1 on failure.
    public static int LoadAudioSample(string wavFile)
    {
        var a = new AudioSample(wavFile);
        if (!a.Valid) return -1;
        lock (Lock) { AudioSamples.Add(a); return AudioSamples.Count; }
    }

    public static void PlaySample(int id, bool loop = false)
    {
        lock (Lock) ActiveSamples.Add(new PlayingSample { AudioSampleId = id, Loop = loop });
    }

    public static void StopSample(int id)
    {
        lock (Lock)
            foreach (var s in ActiveSamples)
                if (s.AudioSampleId == id) { s.FlagForStop = true; break; }
    }

    public static void StopAll()
    {
        lock (Lock)
            foreach (var s in ActiveSamples) s.FlagForStop = true;
    }

    // Mixes one output channel at the given time: all active samples + the optional user synth,
    // passed through the optional user filter.
    public static float GetMixerOutput(int channel, float globalTime, float timeStep)
    {
        var mix = 0f;
        lock (Lock)
        {
            foreach (var s in ActiveSamples)
            {
                if (s.FlagForStop) { s.Loop = false; s.Finished = true; continue; }
                var sample = AudioSamples[s.AudioSampleId - 1];
                s.SamplePosition += (long)MathF.Round(sample.SampleRate * timeStep);
                if (s.SamplePosition < sample.NSamples)
                {
                    var idx = s.SamplePosition * sample.NChannels + Math.Min(channel, sample.NChannels - 1);
                    if (idx >= 0 && idx < sample.Sample.Length) mix += sample.Sample[idx];
                }
                else if (s.Loop) s.SamplePosition = 0;
                else s.Finished = true;
            }
            ActiveSamples.RemoveAll(s => s.Finished);
        }

        if (_userSynth != null) mix += _userSynth(channel, globalTime, timeStep);
        return _userFilter != null ? _userFilter(channel, globalTime, mix) : mix;
    }

    public static bool InitialiseAudio(int sampleRate = 44100, int channels = 1, int blocks = 8, int blockSamples = 512)
    {
        _audioThreadActive = false;
        _sampleRate = sampleRate;
        _channels = channels;
        _blockSamples = blockSamples;

        _device = ALC.OpenDevice(null);
        if (_device == ALDevice.Null) return DestroyAudio();
        _context = ALC.CreateContext(_device, (int[])null);
        ALC.MakeContextCurrent(_context);

        _buffers = AL.GenBuffers(blocks);
        _source = AL.GenSource();
        AvailableBuffers.Clear();
        foreach (var b in _buffers) AvailableBuffers.Enqueue(b);

        lock (Lock) ActiveSamples.Clear();
        _blockMemory = new short[blockSamples];

        _audioThreadActive = true;
        _audioThread = new Thread(AudioThread) { IsBackground = true };
        _audioThread.Start();
        return true;
    }

    public static bool DestroyAudio()
    {
        _audioThreadActive = false;
        _audioThread?.Join();

        if (_buffers != null) AL.DeleteBuffers(_buffers);
        if (_source != 0) AL.DeleteSource(_source);
        ALC.MakeContextCurrent(ALContext.Null);
        if (_context != ALContext.Null) ALC.DestroyContext(_context);
        if (_device != ALDevice.Null) ALC.CloseDevice(_device);
        _buffers = null;
        _source = 0;
        return false;
    }

    private static float Clip(float sample, float max) => sample >= 0 ? MathF.Min(sample, max) : MathF.Max(sample, -max);

    // Keeps the OpenAL source's buffer queue topped up with freshly mixed blocks.
    private static void AudioThread()
    {
        _globalTime = 0f;
        var timeStep = 1f / _sampleRate;
        var maxSample = (float)short.MaxValue;

        while (_audioThreadActive)
        {
            AL.GetSource(_source, ALGetSourcei.SourceState, out var state);
            AL.GetSource(_source, ALGetSourcei.BuffersProcessed, out var processed);

            for (var i = 0; i < processed; i++)
                AvailableBuffers.Enqueue(AL.SourceUnqueueBuffer(_source));

            if (AvailableBuffers.Count == 0) { Thread.Sleep(1); continue; }

            for (var n = 0; n < _blockSamples; n += _channels)
            {
                for (var c = 0; c < _channels; c++)
                    _blockMemory[n + c] = (short)(Clip(GetMixerOutput(c, _globalTime, timeStep), 1f) * maxSample);
                _globalTime += timeStep;
            }

            var buf = AvailableBuffers.Dequeue();
            AL.BufferData(buf, _channels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16, _blockMemory, _sampleRate);
            AL.SourceQueueBuffer(_source, buf);

            if (state != (int)ALSourceState.Playing) AL.SourcePlay(_source);
        }
    }
}
