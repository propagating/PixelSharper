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
/// <summary>An in-memory 16-bit/44100Hz WAV sample (normalised to float). Valid is false if loading failed.</summary>
public class AudioSample
{
    /// <summary>Samples per second (44100).</summary>
    public int SampleRate;
    /// <summary>Channel count (mono/stereo).</summary>
    public int NChannels;
    /// <summary>Number of samples per channel.</summary>
    public long NSamples;
    /// <summary>Interleaved sample data normalised to [-1,1].</summary>
    public float[] Sample;
    /// <summary>True once a WAV has been loaded successfully.</summary>
    public bool Valid;

    /// <summary>Creates an empty, invalid sample.</summary>
    public AudioSample() { }
    /// <summary>Creates a sample by loading the given WAV file.</summary>
    /// <param name="wavFile">Path to the 16-bit/44100Hz WAV file to load; <see cref="Valid"/> reflects success.</param>
    /// <seealso cref="LoadFromFile"/>
    public AudioSample(string wavFile) => LoadFromFile(wavFile);

    /// <summary>Parses a 16-bit/44100Hz WAV file into normalised float samples; returns false if unsupported or malformed.</summary>
    /// <param name="wavFile">Path to the WAV file to parse.</param>
    /// <returns><c>true</c> if the file was a supported 16-bit/44100Hz WAV and was loaded; <c>false</c> if its format is unsupported, the header is malformed, or an I/O error occurred.</returns>
    /// <remarks>Only 16-bit PCM at 44100 Hz is accepted; samples are normalised to the range [-1, 1]. On success, <see cref="Valid"/> is set to <c>true</c>.</remarks>
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

/// <summary>Static audio subsystem: a background-thread software mixer streaming mixed PCM blocks into a single OpenAL streaming source.</summary>
/// <remarks>
/// <para>The actual OpenAL output requires real audio hardware: the mixer thread and WAV loader run anywhere, but <see cref="InitialiseAudio"/> opens a device/context and queues PCM onto a streaming source via OpenTK.Audio.OpenAL.</para>
/// </remarks>
/// <seealso cref="AudioSample"/>
public static class Sound
{
    /// <summary>A live instance of a sample being played back, tracking its read position and stop/loop state.</summary>
    private sealed class PlayingSample
    {
        /// <summary>1-based id into the loaded sample list.</summary>
        public int AudioSampleId;
        /// <summary>Current per-channel read position.</summary>
        public long SamplePosition;
        /// <summary>True once playback has ended (pruned from the active list).</summary>
        public bool Finished;
        /// <summary>Whether to restart at the end.</summary>
        public bool Loop;
        /// <summary>Requests the sample stop on the next mix pass.</summary>
        public bool FlagForStop;
    }

    /// <summary>Mutex guarding the sample and playing-sample lists.</summary>
    private static readonly object Lock = new();
    /// <summary>Currently playing sample instances.</summary>
    private static readonly List<PlayingSample> ActiveSamples = new();
    /// <summary>All loaded samples (1-based ids index this list).</summary>
    private static readonly List<AudioSample> AudioSamples = new();
    /// <summary>Optional user synth: (channel, globalTime, timeStep) -&gt; sample.</summary>
    private static Func<int, float, float, float>? _userSynth;
    /// <summary>Optional user filter: (channel, globalTime, mixedSample) -&gt; sample.</summary>
    private static Func<int, float, float, float>? _userFilter;

    /// <summary>True while the mixing thread should keep running.</summary>
    private static volatile bool _audioThreadActive;
    /// <summary>Wall-clock time accumulated by the mixer thread.</summary>
    private static float _globalTime;
    /// <summary>The background mixing thread.</summary>
    private static Thread _audioThread;

    // OpenAL
    /// <summary>The opened OpenAL device.</summary>
    private static ALDevice _device;
    /// <summary>The OpenAL context.</summary>
    private static ALContext _context;
    /// <summary>The pool of streaming buffer ids.</summary>
    private static int[] _buffers;
    /// <summary>The single streaming source id.</summary>
    private static int _source;
    /// <summary>Buffer ids not currently queued on the source.</summary>
    private static readonly Queue<int> AvailableBuffers = new();
    /// <summary>Output sample rate, channel count, and per-block sample count.</summary>
    private static int _sampleRate, _channels, _blockSamples;
    /// <summary>Scratch PCM buffer for one mixed block.</summary>
    private static short[] _blockMemory;

    /// <summary>Sets the optional user synth function (null to clear).</summary>
    /// <param name="func">A callback <c>(channel, globalTime, timeStep) =&gt; sample</c> mixed in alongside playing samples, or null to clear it.</param>
    /// <seealso cref="GetMixerOutput"/>
    public static void SetUserSynthFunction(Func<int, float, float, float>? func) => _userSynth = func;
    /// <summary>Sets the optional user filter function (null to clear).</summary>
    /// <param name="func">A callback <c>(channel, globalTime, mixedSample) =&gt; sample</c> applied to the final mix per channel, or null to clear it.</param>
    /// <seealso cref="GetMixerOutput"/>
    public static void SetUserFilterFunction(Func<int, float, float, float>? func) => _userFilter = func;

    /// <summary>Loads a 16-bit/44100Hz WAV into memory; returns a 1-based sample id, or -1 on failure.</summary>
    /// <param name="wavFile">Path to the WAV file to load.</param>
    /// <returns>A 1-based sample id for use with <see cref="PlaySample"/>/<see cref="StopSample"/>, or -1 if the file could not be loaded.</returns>
    public static int LoadAudioSample(string wavFile)
    {
        var a = new AudioSample(wavFile);
        if (!a.Valid) return -1;
        lock (Lock) { AudioSamples.Add(a); return AudioSamples.Count; }
    }

    /// <summary>Begins playing a loaded sample, optionally looping.</summary>
    /// <param name="id">The 1-based sample id returned by <see cref="LoadAudioSample"/>.</param>
    /// <param name="loop">When <c>true</c>, the sample restarts from the beginning when it ends.</param>
    public static void PlaySample(int id, bool loop = false)
    {
        lock (Lock) ActiveSamples.Add(new PlayingSample { AudioSampleId = id, Loop = loop });
    }

    /// <summary>Flags the first active instance of the given sample id to stop.</summary>
    /// <param name="id">The 1-based sample id whose first active instance should stop.</param>
    public static void StopSample(int id)
    {
        lock (Lock)
            foreach (var s in ActiveSamples)
                if (s.AudioSampleId == id) { s.FlagForStop = true; break; }
    }

    /// <summary>Flags every active sample to stop.</summary>
    public static void StopAll()
    {
        lock (Lock)
            foreach (var s in ActiveSamples) s.FlagForStop = true;
    }

    /// <summary>Mixes one output channel at the given time: all active samples + the optional user synth, passed through the optional user filter.</summary>
    /// <param name="channel">The output channel index being mixed.</param>
    /// <param name="globalTime">Wall-clock time accumulated by the mixer, in seconds.</param>
    /// <param name="timeStep">The duration of one output sample, in seconds (1 / sample rate).</param>
    /// <returns>The mixed sample value for the channel, after the optional user synth and filter are applied.</returns>
    /// <remarks>Advances each active sample's read position; finished non-looping samples are pruned from the active list.</remarks>
    /// <seealso cref="SetUserSynthFunction"/>
    /// <seealso cref="SetUserFilterFunction"/>
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

    /// <summary>Opens the OpenAL device/context, allocates streaming buffers + the source, and starts the mixer thread; returns false on device failure.</summary>
    /// <param name="sampleRate">Output sample rate in Hz.</param>
    /// <param name="channels">Output channel count (1 = mono, 2 = stereo).</param>
    /// <param name="blocks">Number of streaming buffers to allocate for the source queue.</param>
    /// <param name="blockSamples">Number of samples per mixed block.</param>
    /// <returns><c>true</c> if the OpenAL device opened and the mixer thread started; <c>false</c> if no audio device could be opened.</returns>
    /// <remarks>Requires real audio hardware to open an OpenAL device. On failure it tail-calls <see cref="DestroyAudio"/>, which always returns <c>false</c>.</remarks>
    /// <seealso cref="DestroyAudio"/>
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

    /// <summary>Stops the mixer thread and tears down all OpenAL resources; always returns false (so InitialiseAudio can tail-call it on failure).</summary>
    /// <returns>Always <c>false</c>, so <see cref="InitialiseAudio"/> can tail-call it on a failure path.</returns>
    /// <seealso cref="InitialiseAudio"/>
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

    /// <summary>Clamps a sample to [-max, max].</summary>
    /// <param name="sample">The sample value to clamp.</param>
    /// <param name="max">The symmetric magnitude limit.</param>
    /// <returns><paramref name="sample"/> clamped to the range [<c>-max</c>, <c>max</c>].</returns>
    private static float Clip(float sample, float max) => sample >= 0 ? MathF.Min(sample, max) : MathF.Max(sample, -max);

    /// <summary>Keeps the OpenAL source's buffer queue topped up with freshly mixed blocks.</summary>
    /// <remarks>Runs on the background mixer thread until the audio subsystem is torn down; recycles processed buffers, mixes one block via <see cref="GetMixerOutput"/>, and re-queues it on the source.</remarks>
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
