using System;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities;

/// <summary>Port of olcUTIL_Camera2D — a 2D camera that follows a target point in one of several modes and produces a world-space view rectangle.</summary>
/// <remarks>
/// <para>Pairs well with TransformedView's SetWorldOffset: feed <see cref="GetViewPosition"/> as the world offset.</para>
/// <para>olc tracks the target via a pointer; in C# you either set a value (<see cref="SetTarget(Vector2d{float})"/>) or supply a live provider (<see cref="SetTarget(Func{Vector2d{float}})"/>).</para>
/// <para>The follow behaviour is selected by <see cref="Mode"/> via <see cref="SetMode"/>; call <see cref="Update"/> once per frame to advance.</para>
/// </remarks>
// Port of olcUTIL_Camera2D — a 2D camera that follows a target point in one of several modes and
// produces a world-space view rectangle (pair well with TransformedView's SetWorldOffset).
// olc tracks the target via a pointer; in C# you either set a value (SetTarget(value)) or supply
// a live provider (SetTarget(Func)).
public class Camera2D
{
    /// <summary>How the camera tracks its target.</summary>
    /// <remarks>
    /// The five follow modes:
    /// <list type="bullet">
    /// <item><description><see cref="Simple"/> — directly settable, no motion.</description></item>
    /// <item><description><see cref="EdgeMove"/> — moves only when the target drifts past <see cref="GetEdgeTriggerDistance"/>.</description></item>
    /// <item><description><see cref="LazyFollow"/> — eases toward the target at <see cref="GetLazyFollowRate"/>.</description></item>
    /// <item><description><see cref="FixedScreens"/> — snaps between fixed "screen" cells.</description></item>
    /// <item><description><see cref="SlideScreens"/> — slides quickly between fixed "screen" cells.</description></item>
    /// </list>
    /// </remarks>
    public enum Mode : byte
    {
        /// <summary>Directly settable, no motion.</summary>
        Simple,        // directly settable, no motion
        /// <summary>Moves as the target crosses a boundary.</summary>
        EdgeMove,      // moves as the target crosses a boundary
        /// <summary>Eases toward the target.</summary>
        LazyFollow,    // eases toward the target
        /// <summary>Snaps between fixed "screens".</summary>
        FixedScreens,  // snaps between fixed "screens"
        /// <summary>Slides quickly between fixed "screens".</summary>
        SlideScreens   // slides quickly between fixed "screens"
    }

    /// <summary>Current focus point the view is centred on.</summary>
    private Vector2d<float> _position;
    /// <summary>Size of the world-space view rectangle.</summary>
    private Vector2d<float> _viewSize;
    /// <summary>Top-left of the world-space view rectangle.</summary>
    private Vector2d<float> _viewPos;
    /// <summary>Active follow mode.</summary>
    private Mode _mode = Mode.Simple;

    /// <summary>Target value used when no provider is set.</summary>
    private Vector2d<float> _localTarget;
    /// <summary>Optional live target provider (olc's ref-pointer equivalent).</summary>
    private Func<Vector2d<float>> _targetProvider;

    /// <summary>Whether the view is clamped to a world boundary.</summary>
    private bool _worldBoundary;
    /// <summary>World boundary top-left.</summary>
    private Vector2d<float> _worldBoundaryPos = new(0, 0);
    /// <summary>World boundary size.</summary>
    private Vector2d<float> _worldBoundarySize = new(256, 240);

    /// <summary>Half-extent the target may drift before EdgeMove nudges the camera.</summary>
    private Vector2d<float> _edgeTriggerDistance = new(1, 1);
    /// <summary>Easing rate for LazyFollow/SlideScreens.</summary>
    private float _lazyFollowRate = 4.0f;
    /// <summary>Fixed-screen cell size in world units.</summary>
    private Vector2d<int> _screenSize = new(16, 15);

    /// <summary>Creates a camera with default view size.</summary>
    public Camera2D() { }

    /// <summary>Creates a camera with the given view size and optional initial view position.</summary>
    /// <param name="viewSize">Size of the world-space view rectangle.</param>
    /// <param name="viewPos">Initial top-left of the world-space view rectangle; defaults to the origin.</param>
    public Camera2D(Vector2d<float> viewSize, Vector2d<float> viewPos = default)
    {
        _viewSize = viewSize;
        _viewPos = viewPos;
    }

    /// <summary>Sets the follow mode.</summary>
    /// <param name="mode">The <see cref="Mode"/> governing how the camera tracks its target.</param>
    public void SetMode(Mode mode) => _mode = mode;
    /// <summary>Returns the active follow mode.</summary>
    /// <returns>The current <see cref="Mode"/>.</returns>
    public Mode GetMode() => _mode;

    /// <summary>Tracks a fixed target value, clearing any live provider.</summary>
    /// <param name="target">The fixed world-space point to follow.</param>
    // Track a fixed value, or a live provider (olc's ref-pointer equivalent).
    public void SetTarget(Vector2d<float> target) { _localTarget = target; _targetProvider = null; }
    /// <summary>Tracks a live target provider (olc's ref-pointer equivalent).</summary>
    /// <param name="provider">A delegate evaluated each <see cref="GetTarget"/> call to supply the current world-space point.</param>
    public void SetTarget(Func<Vector2d<float>> provider) => _targetProvider = provider;
    /// <summary>Returns the current target, from the provider if set else the stored value.</summary>
    /// <returns>The world-space target point.</returns>
    public Vector2d<float> GetTarget() => _targetProvider != null ? _targetProvider() : _localTarget;

    /// <summary>Returns the current focus position.</summary>
    /// <returns>The world-space focus point the view is centred on.</returns>
    public Vector2d<float> GetPosition() => _position;
    /// <summary>Returns the world-space view rectangle top-left.</summary>
    /// <returns>The top-left corner of the world-space view rectangle.</returns>
    public Vector2d<float> GetViewPosition() => _viewPos;
    /// <summary>Returns the world-space view rectangle size.</summary>
    /// <returns>The size of the world-space view rectangle.</returns>
    public Vector2d<float> GetViewSize() => _viewSize;

    /// <summary>Sets the world boundary the view is clamped to.</summary>
    /// <param name="pos">World boundary top-left.</param>
    /// <param name="size">World boundary size.</param>
    /// <seealso cref="EnableWorldBoundary"/>
    public void SetWorldBoundary(Vector2d<float> pos, Vector2d<float> size) { _worldBoundaryPos = pos; _worldBoundarySize = size; }
    /// <summary>Enables or disables world boundary clamping.</summary>
    /// <param name="enable"><c>true</c> to clamp the view to the world boundary; <c>false</c> to disable clamping.</param>
    public void EnableWorldBoundary(bool enable) => _worldBoundary = enable;
    /// <summary>Returns whether world boundary clamping is enabled.</summary>
    /// <returns><c>true</c> if the view is clamped to the world boundary; otherwise <c>false</c>.</returns>
    public bool IsWorldBoundaryEnabled() => _worldBoundary;
    /// <summary>Returns the world boundary top-left.</summary>
    /// <returns>The top-left corner of the world boundary.</returns>
    public Vector2d<float> GetWorldBoundaryPosition() => _worldBoundaryPos;
    /// <summary>Returns the world boundary size.</summary>
    /// <returns>The size of the world boundary.</returns>
    public Vector2d<float> GetWorldBoundarySize() => _worldBoundarySize;

    /// <summary>Sets the LazyFollow/SlideScreens easing rate.</summary>
    /// <param name="rate">Easing rate applied per second in <see cref="Mode.LazyFollow"/> and <see cref="Mode.SlideScreens"/>.</param>
    public void SetLazyFollowRate(float rate) => _lazyFollowRate = rate;
    /// <summary>Returns the easing rate.</summary>
    /// <returns>The easing rate used by <see cref="Mode.LazyFollow"/> and <see cref="Mode.SlideScreens"/>.</returns>
    public float GetLazyFollowRate() => _lazyFollowRate;
    /// <summary>Sets the EdgeMove trigger half-extent.</summary>
    /// <param name="edge">Half-extent the target may drift before <see cref="Mode.EdgeMove"/> nudges the camera.</param>
    public void SetEdgeTriggerDistance(Vector2d<float> edge) => _edgeTriggerDistance = edge;
    /// <summary>Returns the EdgeMove trigger half-extent.</summary>
    /// <returns>The half-extent before <see cref="Mode.EdgeMove"/> begins moving the camera.</returns>
    public Vector2d<float> GetEdgeTriggerDistance() => _edgeTriggerDistance;

    /// <summary>Advances the camera by the chosen mode, applies world-boundary clamping, and returns true if the target is visible.</summary>
    /// <param name="elapsedTime">Seconds elapsed since the previous update; scales the eased motion in <see cref="Mode.LazyFollow"/> and <see cref="Mode.SlideScreens"/>.</param>
    /// <returns><c>true</c> if the target lies within the resulting view rectangle; otherwise <c>false</c>.</returns>
    /// <remarks>Behaviour depends on the active <see cref="Mode"/>; call once per frame after setting the target.</remarks>
    // Updates the camera, applies world boundary clamping, and returns true if the target is visible.
    public virtual bool Update(float elapsedTime)
    {
        var target = GetTarget();
        switch (_mode)
        {
            case Mode.Simple:
                _position = target;
                break;

            case Mode.EdgeMove:
            {
                var overlap = target - _position;
                if (overlap.X > _edgeTriggerDistance.X) _position.X += overlap.X - _edgeTriggerDistance.X;
                if (overlap.X < -_edgeTriggerDistance.X) _position.X += overlap.X + _edgeTriggerDistance.X;
                if (overlap.Y > _edgeTriggerDistance.Y) _position.Y += overlap.Y - _edgeTriggerDistance.Y;
                if (overlap.Y < -_edgeTriggerDistance.Y) _position.Y += overlap.Y + _edgeTriggerDistance.Y;
                break;
            }

            case Mode.LazyFollow:
                _position += new Vector2d<float>(
                    (target.X - _position.X) * _lazyFollowRate * elapsedTime,
                    (target.Y - _position.Y) * _lazyFollowRate * elapsedTime);
                break;

            case Mode.FixedScreens:
                _position = ScreenCentre(target);
                break;

            case Mode.SlideScreens:
            {
                var screen = ScreenCentre(target);
                _position += new Vector2d<float>(
                    (screen.X - _position.X) * _lazyFollowRate * 2.0f * elapsedTime,
                    (screen.Y - _position.Y) * _lazyFollowRate * 2.0f * elapsedTime);
                break;
            }
        }

        // Centre the view on the focus point.
        _viewPos = new Vector2d<float>(_position.X - _viewSize.X * 0.5f, _position.Y - _viewSize.Y * 0.5f);

        if (_worldBoundary)
        {
            var maxPos = _worldBoundaryPos + _worldBoundarySize - _viewSize;
            _viewPos = Vector2d<float>.Min(Vector2d<float>.Max(_viewPos, _worldBoundaryPos), maxPos);
        }

        var t = GetTarget();
        return t.X >= _viewPos.X && t.X < _viewPos.X + _viewSize.X &&
               t.Y >= _viewPos.Y && t.Y < _viewPos.Y + _viewSize.Y;
    }

    /// <summary>Snaps a target point to the centre of its containing fixed "screen".</summary>
    /// <param name="target">The world-space point to snap.</param>
    /// <returns>The world-space centre of the fixed-screen cell containing <paramref name="target"/>.</returns>
    // Snaps a target point to the centre of its containing fixed "screen".
    private Vector2d<float> ScreenCentre(Vector2d<float> target)
    {
        var tileX = (int)(target.X / _screenSize.X) * _screenSize.X;
        var tileY = (int)(target.Y / _screenSize.Y) * _screenSize.Y;
        return new Vector2d<float>(tileX + _viewSize.X * 0.5f, tileY + _viewSize.Y * 0.5f);
    }
}
