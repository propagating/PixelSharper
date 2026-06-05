using System;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_Camera2D — a 2D camera that follows a target point in one of several modes and
// produces a world-space view rectangle (pair well with TransformedView's SetWorldOffset).
// olc tracks the target via a pointer; in C# you either set a value (SetTarget(value)) or supply
// a live provider (SetTarget(Func)).
public class Camera2D
{
    public enum Mode : byte
    {
        Simple,        // directly settable, no motion
        EdgeMove,      // moves as the target crosses a boundary
        LazyFollow,    // eases toward the target
        FixedScreens,  // snaps between fixed "screens"
        SlideScreens   // slides quickly between fixed "screens"
    }

    private Vector2d<float> _position;
    private Vector2d<float> _viewSize;
    private Vector2d<float> _viewPos;
    private Mode _mode = Mode.Simple;

    private Vector2d<float> _localTarget;
    private Func<Vector2d<float>> _targetProvider;

    private bool _worldBoundary;
    private Vector2d<float> _worldBoundaryPos = new(0, 0);
    private Vector2d<float> _worldBoundarySize = new(256, 240);

    private Vector2d<float> _edgeTriggerDistance = new(1, 1);
    private float _lazyFollowRate = 4.0f;
    private Vector2d<int> _screenSize = new(16, 15);

    public Camera2D() { }

    public Camera2D(Vector2d<float> viewSize, Vector2d<float> viewPos = default)
    {
        _viewSize = viewSize;
        _viewPos = viewPos;
    }

    public void SetMode(Mode mode) => _mode = mode;
    public Mode GetMode() => _mode;

    // Track a fixed value, or a live provider (olc's ref-pointer equivalent).
    public void SetTarget(Vector2d<float> target) { _localTarget = target; _targetProvider = null; }
    public void SetTarget(Func<Vector2d<float>> provider) => _targetProvider = provider;
    public Vector2d<float> GetTarget() => _targetProvider != null ? _targetProvider() : _localTarget;

    public Vector2d<float> GetPosition() => _position;
    public Vector2d<float> GetViewPosition() => _viewPos;
    public Vector2d<float> GetViewSize() => _viewSize;

    public void SetWorldBoundary(Vector2d<float> pos, Vector2d<float> size) { _worldBoundaryPos = pos; _worldBoundarySize = size; }
    public void EnableWorldBoundary(bool enable) => _worldBoundary = enable;
    public bool IsWorldBoundaryEnabled() => _worldBoundary;
    public Vector2d<float> GetWorldBoundaryPosition() => _worldBoundaryPos;
    public Vector2d<float> GetWorldBoundarySize() => _worldBoundarySize;

    public void SetLazyFollowRate(float rate) => _lazyFollowRate = rate;
    public float GetLazyFollowRate() => _lazyFollowRate;
    public void SetEdgeTriggerDistance(Vector2d<float> edge) => _edgeTriggerDistance = edge;
    public Vector2d<float> GetEdgeTriggerDistance() => _edgeTriggerDistance;

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

    // Snaps a target point to the centre of its containing fixed "screen".
    private Vector2d<float> ScreenCentre(Vector2d<float> target)
    {
        var tileX = (int)(target.X / _screenSize.X) * _screenSize.X;
        var tileY = (int)(target.Y / _screenSize.Y) * _screenSize.Y;
        return new Vector2d<float>(tileX + _viewSize.X * 0.5f, tileY + _viewSize.Y * 0.5f);
    }
}
