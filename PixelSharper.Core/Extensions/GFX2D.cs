using PixelSharper.Core.Components;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions;

// Port of olcPGEX_Graphics2D (olc::GFX2D) — advanced 2D drawing: an affine Transform2D accumulator
// and a transform-aware sprite blit (rotate/scale/shear/translate/perspective via back-sampling).
public class GFX2D : PGEX
{
    // An affine transform built by appending operations. Uses olc's 4-matrix scheme:
    // matrices 0 & 1 ping-pong as accumulation source/target, 2 holds the immediate op,
    // 3 caches the inverse (regenerated lazily on Invert()).
    public class Transform2D
    {
        private readonly float[,,] _m = new float[4, 3, 3];
        private int _target;
        private int _source;
        private bool _dirty;

        public Transform2D() => Reset();

        public void Reset()
        {
            _target = 0;
            _source = 1;
            _dirty = true;
            for (var k = 0; k < 2; k++)
            {
                _m[k, 0, 0] = 1; _m[k, 1, 0] = 0; _m[k, 2, 0] = 0;
                _m[k, 0, 1] = 0; _m[k, 1, 1] = 1; _m[k, 2, 1] = 0;
                _m[k, 0, 2] = 0; _m[k, 1, 2] = 0; _m[k, 2, 2] = 1;
            }
        }

        private void Multiply()
        {
            for (var c = 0; c < 3; c++)
                for (var r = 0; r < 3; r++)
                    _m[_target, c, r] =
                        _m[2, 0, r] * _m[_source, c, 0] +
                        _m[2, 1, r] * _m[_source, c, 1] +
                        _m[2, 2, r] * _m[_source, c, 2];

            (_target, _source) = (_source, _target);
            _dirty = true; // any multiply invalidates the cached inverse
        }

        public void Rotate(float theta)
        {
            _m[2, 0, 0] = MathF.Cos(theta);  _m[2, 1, 0] = MathF.Sin(theta); _m[2, 2, 0] = 0;
            _m[2, 0, 1] = -MathF.Sin(theta); _m[2, 1, 1] = MathF.Cos(theta); _m[2, 2, 1] = 0;
            _m[2, 0, 2] = 0;                 _m[2, 1, 2] = 0;                _m[2, 2, 2] = 1;
            Multiply();
        }

        public void Scale(float sx, float sy)
        {
            _m[2, 0, 0] = sx; _m[2, 1, 0] = 0;  _m[2, 2, 0] = 0;
            _m[2, 0, 1] = 0;  _m[2, 1, 1] = sy; _m[2, 2, 1] = 0;
            _m[2, 0, 2] = 0;  _m[2, 1, 2] = 0;  _m[2, 2, 2] = 1;
            Multiply();
        }

        public void Shear(float sx, float sy)
        {
            _m[2, 0, 0] = 1;  _m[2, 1, 0] = sx; _m[2, 2, 0] = 0;
            _m[2, 0, 1] = sy; _m[2, 1, 1] = 1;  _m[2, 2, 1] = 0;
            _m[2, 0, 2] = 0;  _m[2, 1, 2] = 0;  _m[2, 2, 2] = 1;
            Multiply();
        }

        public void Translate(float ox, float oy)
        {
            _m[2, 0, 0] = 1; _m[2, 1, 0] = 0; _m[2, 2, 0] = ox;
            _m[2, 0, 1] = 0; _m[2, 1, 1] = 1; _m[2, 2, 1] = oy;
            _m[2, 0, 2] = 0; _m[2, 1, 2] = 0; _m[2, 2, 2] = 1;
            Multiply();
        }

        public void Perspective(float ox, float oy)
        {
            _m[2, 0, 0] = 1;  _m[2, 1, 0] = 0;  _m[2, 2, 0] = 0;
            _m[2, 0, 1] = 0;  _m[2, 1, 1] = 1;  _m[2, 2, 1] = 0;
            _m[2, 0, 2] = ox; _m[2, 1, 2] = oy; _m[2, 2, 2] = 1;
            Multiply();
        }

        // Forward transform of (inX, inY) through the accumulated matrix.
        public Vector2d<float> Forward(float inX, float inY)
        {
            var ox = inX * _m[_source, 0, 0] + inY * _m[_source, 1, 0] + _m[_source, 2, 0];
            var oy = inX * _m[_source, 0, 1] + inY * _m[_source, 1, 1] + _m[_source, 2, 1];
            var oz = inX * _m[_source, 0, 2] + inY * _m[_source, 1, 2] + _m[_source, 2, 2];
            if (oz != 0) { ox /= oz; oy /= oz; }
            return new Vector2d<float>(ox, oy);
        }

        // Inverse transform (requires a prior Invert()).
        public Vector2d<float> Backward(float inX, float inY)
        {
            var ox = inX * _m[3, 0, 0] + inY * _m[3, 1, 0] + _m[3, 2, 0];
            var oy = inX * _m[3, 0, 1] + inY * _m[3, 1, 1] + _m[3, 2, 1];
            var oz = inX * _m[3, 0, 2] + inY * _m[3, 1, 2] + _m[3, 2, 2];
            if (oz != 0) { ox /= oz; oy /= oz; }
            return new Vector2d<float>(ox, oy);
        }

        public void Invert()
        {
            if (!_dirty) return; // costly, so only when the matrix changed

            var s = _source;
            var det = _m[s, 0, 0] * (_m[s, 1, 1] * _m[s, 2, 2] - _m[s, 1, 2] * _m[s, 2, 1]) -
                      _m[s, 1, 0] * (_m[s, 0, 1] * _m[s, 2, 2] - _m[s, 2, 1] * _m[s, 0, 2]) +
                      _m[s, 2, 0] * (_m[s, 0, 1] * _m[s, 1, 2] - _m[s, 1, 1] * _m[s, 0, 2]);

            var idet = 1.0f / det;
            _m[3, 0, 0] = (_m[s, 1, 1] * _m[s, 2, 2] - _m[s, 1, 2] * _m[s, 2, 1]) * idet;
            _m[3, 1, 0] = (_m[s, 2, 0] * _m[s, 1, 2] - _m[s, 1, 0] * _m[s, 2, 2]) * idet;
            _m[3, 2, 0] = (_m[s, 1, 0] * _m[s, 2, 1] - _m[s, 2, 0] * _m[s, 1, 1]) * idet;
            _m[3, 0, 1] = (_m[s, 2, 1] * _m[s, 0, 2] - _m[s, 0, 1] * _m[s, 2, 2]) * idet;
            _m[3, 1, 1] = (_m[s, 0, 0] * _m[s, 2, 2] - _m[s, 2, 0] * _m[s, 0, 2]) * idet;
            _m[3, 2, 1] = (_m[s, 0, 1] * _m[s, 2, 0] - _m[s, 0, 0] * _m[s, 2, 1]) * idet;
            _m[3, 0, 2] = (_m[s, 0, 1] * _m[s, 1, 2] - _m[s, 0, 2] * _m[s, 1, 1]) * idet;
            _m[3, 1, 2] = (_m[s, 0, 2] * _m[s, 1, 0] - _m[s, 0, 0] * _m[s, 1, 2]) * idet;
            _m[3, 2, 2] = (_m[s, 0, 0] * _m[s, 1, 1] - _m[s, 0, 1] * _m[s, 1, 0]) * idet;
            _dirty = false;
        }
    }

    // Draws a sprite with the transform applied: bound the transformed quad, then for each
    // destination pixel back-sample the source texel.
    public static void DrawSprite(Sprite sprite, Transform2D transform)
    {
        if (sprite == null) return;

        // Bounding rectangle of the four transformed corners.
        var p = transform.Forward(0, 0);
        float sx = p.X, sy = p.Y, ex = p.X, ey = p.Y;
        void Bound(Vector2d<float> q)
        {
            sx = Math.Min(sx, q.X); sy = Math.Min(sy, q.Y);
            ex = Math.Max(ex, q.X); ey = Math.Max(ey, q.Y);
        }
        Bound(transform.Forward(sprite.Width, sprite.Height));
        Bound(transform.Forward(0, sprite.Height));
        Bound(transform.Forward(sprite.Width, 0));

        transform.Invert();
        if (ex < sx) (ex, sx) = (sx, ex);
        if (ey < sy) (ey, sy) = (sy, ey);

        for (var i = sx; i < ex; i++)
            for (var j = sy; j < ey; j++)
            {
                var o = transform.Backward(i, j);
                Pge.Draw((int)i, (int)j, sprite.GetPixel((int)(o.X + 0.5f), (int)(o.Y + 0.5f)));
            }
    }
}
