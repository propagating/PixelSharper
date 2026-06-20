using PixelSharper.Core.Actions;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Core.Utilities.Animate2D;

// Port of olcUTIL_Animate2D — a small sprite-animation state machine: Frames reference a source
// image + rect; a FrameSequence traverses them over time in a Style; an Animation<TState> maps
// named states to sequences and mutates a lightweight AnimationState token.

/// <summary>A single animation frame: a source image (Renderable) and the sub-rect within it. A "source-less" frame (null source) is valid for applying a common layout to many images.</summary>
public class Frame
{
    /// <summary>The source image this frame samples (null for a layout-only frame).</summary>
    public Renderable? Source { get; }
    /// <summary>The sub-rectangle within the source image.</summary>
    public Rect<int> SourceRect { get; }

    /// <summary>Creates a frame; an unspecified rect defaults to the whole source image.</summary>
    /// <param name="source">The source image to sample, or <c>null</c> for a layout-only frame.</param>
    /// <param name="sourceRect">The sub-rectangle within the source; a default (zero-size) rect uses the whole image when <paramref name="source"/> is non-null.</param>
    public Frame(Renderable? source, Rect<int> sourceRect = default)
    {
        Source = source;
        // No source rect specified -> use the whole image (ignored for source-less frames).
        if (source != null && sourceRect.Size.X == 0)
            sourceRect.Size = source.Sprite.Size();
        SourceRect = sourceRect;
    }
}

/// <summary>How frames are traversed in time.</summary>
public enum Style : byte
{
    /// <summary>Cycle, wrapping back to the start.</summary>
    Repeat,
    /// <summary>Play once, hold on the final frame.</summary>
    OneShot,
    /// <summary>Forwards then backwards.</summary>
    PingPong,
    /// <summary>Cycle backwards.</summary>
    Reverse
}

/// <summary>An ordered set of frames traversed over time in a given Style.</summary>
public class FrameSequence
{
    /// <summary>The traversal style.</summary>
    private readonly Style _style;
    /// <summary>The frames in play order.</summary>
    private readonly List<Frame> _frames = new();
    /// <summary>Seconds each frame is shown.</summary>
    private float _frameDuration;
    /// <summary>Frames per second (reciprocal of duration).</summary>
    private float _frameRate;

    /// <summary>Creates a sequence with the given per-frame duration and traversal style.</summary>
    /// <param name="frameDuration">Seconds each frame is shown.</param>
    /// <param name="style">The traversal style used to map time to frames.</param>
    public FrameSequence(float frameDuration = 0.1f, Style style = Style.Repeat)
    {
        SetFrameDuration(frameDuration);
        _style = style;
    }

    /// <summary>Sets the per-frame duration and derives the frame rate.</summary>
    /// <param name="frameDuration">Seconds each frame is shown; its reciprocal becomes the frame rate.</param>
    public void SetFrameDuration(float frameDuration = 0.1f)
    {
        _frameDuration = frameDuration;
        _frameRate = 1.0f / frameDuration;
    }

    /// <summary>Appends a frame to the sequence.</summary>
    /// <param name="frame">The frame to append in play order.</param>
    public void AddFrame(Frame frame) => _frames.Add(frame);
    /// <summary>Returns the frame shown at the given elapsed time.</summary>
    /// <param name="time">Elapsed time in seconds since the sequence began.</param>
    /// <returns>The frame mapped from <paramref name="time"/> by the traversal <see cref="Style"/>.</returns>
    public Frame GetFrame(float time) => _frames[ConvertTimeToFrame(time)];
    /// <summary>Whether the sequence has reached the end of one full pass.</summary>
    /// <param name="time">Elapsed time in seconds since the sequence began.</param>
    /// <returns><c>true</c> if <paramref name="time"/> has reached the end of one full pass; otherwise <c>false</c>.</returns>
    public bool Complete(float time) => Math.Abs(time - _frames.Count * _frameDuration) < 0.01f;

    /// <summary>Maps elapsed time to a frame index according to the traversal style.</summary>
    /// <param name="time">Elapsed time in seconds since the sequence began.</param>
    /// <returns>The zero-based frame index for <paramref name="time"/> under the current <see cref="Style"/>.</returns>
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

/// <summary>A lightweight token attached to an animated entity; mutated only by Animation{TState}.</summary>
public class AnimationState
{
    /// <summary>Index of the current sequence within the owning Animation.</summary>
    internal int Index;
    /// <summary>Elapsed time in the current sequence.</summary>
    internal float Time;
    /// <summary>Whether the current sequence has completed.</summary>
    internal bool IsComplete;

    /// <summary>Whether the current sequence has completed a full pass.</summary>
    /// <value><c>true</c> once the current sequence has completed a full pass; otherwise <c>false</c>.</value>
    public bool Complete => IsComplete;
}

/// <summary>Holds named frame sequences (keyed by a state enum) and drives an AnimationState token.</summary>
/// <typeparam name="TState">The state key type (typically an enum).</typeparam>
public class Animation<TState> where TState : notnull
{
    /// <summary>The registered sequences, indexed in registration order.</summary>
    private readonly List<FrameSequence> _sequences = new();
    /// <summary>Maps a state key to its sequence index.</summary>
    private readonly Dictionary<TState, int> _stateIndices = new();

    /// <summary>Switches the token to a different state (resetting time). Returns true if it actually changed.</summary>
    /// <param name="state">The token to mutate.</param>
    /// <param name="stateName">The target state key, previously registered via <see cref="AddState"/>.</param>
    /// <returns><c>true</c> if the token's state changed; <c>false</c> if it was already in <paramref name="stateName"/>.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when <paramref name="stateName"/> has not been registered.</exception>
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

    /// <summary>Advances the token's time and updates its completion flag.</summary>
    /// <param name="state">The token to advance.</param>
    /// <param name="elapsedTime">Seconds to add to the token's elapsed time.</param>
    public void UpdateState(AnimationState state, float elapsedTime)
    {
        state.Time += elapsedTime;
        state.IsComplete = _sequences[state.Index].Complete(state.Time);
    }

    /// <summary>Returns the frame for the token's current state and time.</summary>
    /// <param name="state">The token whose current state and time select the frame.</param>
    /// <returns>The frame for the token's current sequence at its elapsed time.</returns>
    public Frame GetFrame(AnimationState state) => _sequences[state.Index].GetFrame(state.Time);

    /// <summary>Registers a frame sequence under a state key.</summary>
    /// <param name="stateName">The state key to register the sequence under.</param>
    /// <param name="sequence">The sequence played while in this state.</param>
    public void AddState(TState stateName, FrameSequence sequence)
    {
        _sequences.Add(sequence);
        _stateIndices[stateName] = _sequences.Count - 1;
    }
}
