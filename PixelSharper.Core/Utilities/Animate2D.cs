using System;
using System.Collections.Generic;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Core.Utilities.Animate2D;

// Port of olcUTIL_Animate2D — a small sprite-animation state machine: Frames reference a source
// image + rect; a FrameSequence traverses them over time in a Style; an Animation<TState> maps
// named states to sequences and mutates a lightweight AnimationState token.

// A single animation frame: a source image (Renderable) and the sub-rect within it. A "source-less"
// frame (null source) is valid for applying a common layout to many images.
public class Frame
{
    public Renderable? Source { get; }
    public Rect<int> SourceRect { get; }

    public Frame(Renderable? source, Rect<int> sourceRect = default)
    {
        Source = source;
        // No source rect specified -> use the whole image (ignored for source-less frames).
        if (source != null && sourceRect.Size.X == 0)
            sourceRect.Size = source.Sprite.Size();
        SourceRect = sourceRect;
    }
}

// How frames are traversed in time.
public enum Style : byte
{
    Repeat,   // cycle, wrapping back to the start
    OneShot,  // play once, hold on the final frame
    PingPong, // forwards then backwards
    Reverse   // cycle backwards
}

public class FrameSequence
{
    private readonly Style _style;
    private readonly List<Frame> _frames = new();
    private float _frameDuration;
    private float _frameRate;

    public FrameSequence(float frameDuration = 0.1f, Style style = Style.Repeat)
    {
        SetFrameDuration(frameDuration);
        _style = style;
    }

    public void SetFrameDuration(float frameDuration = 0.1f)
    {
        _frameDuration = frameDuration;
        _frameRate = 1.0f / frameDuration;
    }

    public void AddFrame(Frame frame) => _frames.Add(frame);
    public Frame GetFrame(float time) => _frames[ConvertTimeToFrame(time)];
    public bool Complete(float time) => Math.Abs(time - _frames.Count * _frameDuration) < 0.01f;

    private int ConvertTimeToFrame(float time)
    {
        var n = _frames.Count;
        switch (_style)
        {
            case Style.Repeat:
                return (int)(time * _frameRate) % n;
            case Style.OneShot:
                return Math.Clamp((int)(time * _frameRate), 0, n - 1);
            case Style.PingPong:
            {
                var frame = (int)(_frameRate * time) % (n * 2 - 1);
                return frame >= n ? n - frame % n - 1 : frame;
            }
            case Style.Reverse:
                return n - 1 - (int)(time * _frameRate) % n;
        }
        return 0;
    }
}

// A lightweight token attached to an animated entity; mutated only by Animation<TState>.
public class AnimationState
{
    internal int Index;
    internal float Time;
    internal bool IsComplete;

    public bool Complete => IsComplete;
}

// Holds named frame sequences (keyed by a state enum) and drives an AnimationState token.
public class Animation<TState> where TState : notnull
{
    private readonly List<FrameSequence> _sequences = new();
    private readonly Dictionary<TState, int> _stateIndices = new();

    // Switch the token to a different state (resetting time). Returns true if it actually changed.
    public bool ChangeState(AnimationState state, TState stateName)
    {
        var idx = _stateIndices[stateName];
        if (state.Index != idx)
        {
            state.Time = 0.0f;
            state.Index = idx;
            state.IsComplete = false;
            return true;
        }
        return false;
    }

    public void UpdateState(AnimationState state, float elapsedTime)
    {
        state.Time += elapsedTime;
        state.IsComplete = _sequences[state.Index].Complete(state.Time);
    }

    public Frame GetFrame(AnimationState state) => _sequences[state.Index].GetFrame(state.Time);

    public void AddState(TState stateName, FrameSequence sequence)
    {
        _sequences.Add(sequence);
        _stateIndices[stateName] = _sequences.Count - 1;
    }
}
