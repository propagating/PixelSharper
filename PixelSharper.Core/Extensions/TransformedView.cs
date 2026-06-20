using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions;

/// <summary>
/// Port of olcPGEX_TransformedView — a pan/zoom "camera" that maps between world and screen space
/// and offers World* draw wrappers over the engine's drawing API. Not auto-hooked; the user calls
/// Initialise() then HandlePanAndZoom() / the World draws each frame.
/// </summary>
/// <remarks>
/// <para>Two coordinate spaces are in play: <em>world space</em> (the caller's logical units, what every
/// draw wrapper takes) and <em>screen space</em> (engine pixels). <see cref="WorldToScreen"/> and
/// <see cref="ScreenToWorld"/> convert between them using <see cref="WorldOffset"/> and
/// <see cref="WorldScale"/>; the draw wrappers project their world arguments to screen space and
/// forward to the matching <see cref="PixelGameEngine"/> method.</para>
/// <para>Origin: javidx9's olcPGEX_TransformedView.</para>
/// </remarks>
public class TransformedView : PGEX
{
    /// <summary>World-space position currently mapped to the top-left of the view.</summary>
    protected Vector2d<float> WorldOffset = new(0, 0);
    /// <summary>Screen pixels per world unit (the zoom factor).</summary>
    protected Vector2d<float> WorldScale = new(1, 1);
    /// <summary>Reciprocal of the pixel scale, cached for sprite/decal sampling.</summary>
    protected Vector2d<float> RecipPixel = new(1, 1);
    /// <summary>Size in screen pixels of one source texel.</summary>
    protected Vector2d<float> PixelScale = new(1, 1);
    /// <summary>True while a pan drag is in progress.</summary>
    protected bool Panning;
    /// <summary>Screen position where the current pan drag began.</summary>
    protected Vector2d<float> StartPan = new(0, 0);
    /// <summary>Size of the view in screen pixels.</summary>
    protected Vector2d<int> ViewArea;
    /// <summary>When true, zoom is clamped to the min/max scale extents.</summary>
    protected bool ZoomClamp;
    /// <summary>Upper zoom bound used when clamping is enabled.</summary>
    protected Vector2d<float> MaxScale = new(0, 0);
    /// <summary>Lower zoom bound used when clamping is enabled.</summary>
    protected Vector2d<float> MinScale = new(0, 0);

    /// <summary>Returns the owning engine instance.</summary>
    /// <returns>The <see cref="PixelGameEngine"/> this view draws into (the inherited <c>Pge</c>).</returns>
    public PixelGameEngine GetPGE() => Pge;

    /// <summary>Sets up the view area and initial world/pixel scale.</summary>
    /// <param name="viewArea">Size of the view in screen pixels.</param>
    /// <param name="pixelScale">Screen pixels per source texel; defaults to <c>(1, 1)</c> when null.</param>
    /// <remarks>Establishes the initial <see cref="WorldScale"/>, <see cref="PixelScale"/> and the cached
    /// <see cref="RecipPixel"/> reciprocal used by the sprite/decal sampling wrappers.</remarks>
    public virtual void Initialise(Vector2d<int> viewArea, Vector2d<float>? pixelScale = null)
    {
        var ps = pixelScale ?? new Vector2d<float>(1, 1);
        SetViewArea(viewArea);
        SetWorldScale(ps);
        PixelScale = ps;
        RecipPixel = new Vector2d<float>(1.0f / ps.X, 1.0f / ps.Y);
    }

    // O--- Camera state ---O
    /// <summary>Sets the world offset (top-left world position) directly.</summary>
    /// <param name="offset">World-space position to map to the top-left of the view.</param>
    public void SetWorldOffset(Vector2d<float> offset) => WorldOffset = offset;
    /// <summary>Translates the world offset by a delta.</summary>
    /// <param name="delta">World-space displacement added to <see cref="WorldOffset"/>.</param>
    public void MoveWorldOffset(Vector2d<float> delta) => WorldOffset += delta;
    /// <summary>Sets the view area size in screen pixels.</summary>
    /// <param name="viewArea">New view size in screen pixels.</param>
    public void SetViewArea(Vector2d<int> viewArea) => ViewArea = viewArea;
    /// <summary>Returns the current world offset.</summary>
    /// <returns>The world-space position currently mapped to the view's top-left.</returns>
    public Vector2d<float> GetWorldOffset() => WorldOffset;
    /// <summary>Returns the current world scale (zoom).</summary>
    /// <returns>Screen pixels per world unit, per axis.</returns>
    public Vector2d<float> GetWorldScale() => WorldScale;
    /// <summary>Sets the min/max zoom extents used when scale clamping is enabled.</summary>
    /// <param name="min">Lower zoom bound applied when clamping is on.</param>
    /// <param name="max">Upper zoom bound applied when clamping is on.</param>
    /// <seealso cref="EnableScaleClamp"/>
    public void SetScaleExtents(Vector2d<float> min, Vector2d<float> max) { MinScale = min; MaxScale = max; }
    /// <summary>Enables or disables clamping the zoom to the scale extents.</summary>
    /// <param name="enable"><c>true</c> to clamp <see cref="WorldScale"/> to <see cref="MinScale"/>/<see cref="MaxScale"/>; <c>false</c> to leave zoom unbounded.</param>
    /// <seealso cref="SetScaleExtents"/>
    public void EnableScaleClamp(bool enable) => ZoomClamp = enable;

    /// <summary>Sets the world scale (zoom), clamping to the extents if enabled.</summary>
    /// <param name="scale">New world scale (screen pixels per world unit) per axis.</param>
    public void SetWorldScale(Vector2d<float> scale)
    {
        WorldScale = scale;
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
    }

    /// <summary>World position at the top-left corner of the view.</summary>
    /// <returns>The world-space point under the view's top-left pixel.</returns>
    public Vector2d<float> GetWorldTL() => ScreenToWorld(new Vector2d<float>(0, 0));
    /// <summary>World position at the bottom-right corner of the view.</summary>
    /// <returns>The world-space point under the view's bottom-right pixel.</returns>
    public Vector2d<float> GetWorldBR() => ScreenToWorld(ViewArea.As<float>());
    /// <summary>World-space extent currently visible in the view.</summary>
    /// <returns>The bottom-right minus top-left world position, i.e. the visible world size.</returns>
    public Vector2d<float> GetWorldVisibleArea() => GetWorldBR() - GetWorldTL();

    /// <summary>Multiplies the zoom by a factor, keeping the given screen pos anchored in world space.</summary>
    /// <param name="deltaZoom">Multiplicative zoom factor (&gt; 1 zooms in, &lt; 1 zooms out).</param>
    /// <param name="pos">Screen-space pivot that stays fixed in world space across the zoom.</param>
    /// <remarks>Records the world point under <paramref name="pos"/>, rescales, then shifts
    /// <see cref="WorldOffset"/> so that point projects back to the same pixel.</remarks>
    public void ZoomAtScreenPos(float deltaZoom, Vector2d<int> pos)
    {
        var before = ScreenToWorld(pos.As<float>());
        WorldScale = new Vector2d<float>(WorldScale.X * deltaZoom, WorldScale.Y * deltaZoom);
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
        var after = ScreenToWorld(pos.As<float>());
        WorldOffset += before - after;
    }

    /// <summary>Sets an absolute zoom level, keeping the given world pos anchored.</summary>
    /// <param name="zoom">Absolute world scale to apply to both axes.</param>
    /// <param name="pos">World-space point that stays under the same screen pixel after the zoom.</param>
    /// <seealso cref="ZoomAtScreenPos"/>
    public void SetZoom(float zoom, Vector2d<float> pos)
    {
        var before = ScreenToWorld(pos);
        WorldScale = new Vector2d<float>(zoom, zoom);
        if (ZoomClamp) WorldScale = WorldScale.Clamp(MinScale, MaxScale);
        var after = ScreenToWorld(pos);
        WorldOffset += before - after;
    }

    /// <summary>Begins a pan drag anchored at the given screen position.</summary>
    /// <param name="pos">Screen-space position where the drag started.</param>
    /// <seealso cref="UpdatePan"/>
    /// <seealso cref="EndPan"/>
    public void StartPanning(Vector2d<int> pos) { Panning = true; StartPan = pos.As<float>(); }
    /// <summary>Continues an in-progress pan, shifting the world offset by the drag delta.</summary>
    /// <param name="pos">Current screen-space pointer position.</param>
    /// <remarks>No-op unless a pan is active (see <see cref="StartPanning"/>); converts the screen delta to
    /// world units via the current <see cref="WorldScale"/> and re-anchors <see cref="StartPan"/>.</remarks>
    public void UpdatePan(Vector2d<int> pos)
    {
        if (!Panning) return;
        WorldOffset -= (pos.As<float>() - StartPan) / WorldScale;
        StartPan = pos.As<float>();
    }
    /// <summary>Applies a final pan update and ends the drag.</summary>
    /// <param name="pos">Final screen-space pointer position.</param>
    public void EndPan(Vector2d<int> pos) { UpdatePan(pos); Panning = false; }

    // O--- Transforms ---O
    /// <summary>Maps a world position to screen pixels.</summary>
    /// <param name="worldPos">Position in world space.</param>
    /// <returns>The corresponding screen-pixel position.</returns>
    /// <seealso cref="ScreenToWorld"/>
    public virtual Vector2d<float> WorldToScreen(Vector2d<float> worldPos) => (worldPos - WorldOffset) * WorldScale;
    /// <summary>Maps a screen position back to world space.</summary>
    /// <param name="screenPos">Position in screen pixels.</param>
    /// <returns>The corresponding world-space position.</returns>
    /// <seealso cref="WorldToScreen"/>
    public virtual Vector2d<float> ScreenToWorld(Vector2d<float> screenPos) => screenPos / WorldScale + WorldOffset;
    /// <summary>Converts a screen-space size to world-space.</summary>
    /// <param name="screenSize">A size measured in screen pixels.</param>
    /// <returns>The equivalent size in world units at the current zoom.</returns>
    public virtual Vector2d<float> ScaleToWorld(Vector2d<float> screenSize) => screenSize / WorldScale;
    /// <summary>Converts a world-space size to screen-space.</summary>
    /// <param name="worldSize">A size measured in world units.</param>
    /// <returns>The equivalent size in screen pixels at the current zoom.</returns>
    public virtual Vector2d<float> ScaleToScreen(Vector2d<float> worldSize) => worldSize * WorldScale;

    /// <summary>True if the world point projects inside the view area.</summary>
    /// <param name="pos">World-space point to test.</param>
    /// <returns><c>true</c> if the projected pixel lies within the view bounds; otherwise <c>false</c>.</returns>
    public virtual bool IsPointVisible(Vector2d<float> pos)
    {
        var s = WorldToScreen(pos).As<int>();
        return s.X >= 0 && s.X < ViewArea.X && s.Y >= 0 && s.Y < ViewArea.Y;
    }

    /// <summary>True if any part of the world-space rect projects into the view area.</summary>
    /// <param name="pos">Top-left corner of the rectangle in world space.</param>
    /// <param name="size">Rectangle size in world units.</param>
    /// <returns><c>true</c> if the projected rectangle overlaps the view bounds; otherwise <c>false</c>.</returns>
    public virtual bool IsRectVisible(Vector2d<float> pos, Vector2d<float> size)
    {
        var sp = WorldToScreen(pos).As<int>();
        var ss = (size * WorldScale).As<int>();
        return sp.X < ViewArea.X && sp.X + ss.X > 0 && sp.Y < ViewArea.Y && sp.Y + ss.Y > 0;
    }

    /// <summary>Convenience per-frame input driver: pans on the given mouse button and zooms on the wheel.</summary>
    /// <param name="mouseButton">Mouse button index used to drive panning (default <c>2</c>, the middle button).</param>
    /// <param name="zoomRate">Fractional zoom step applied per wheel notch (e.g. <c>0.1</c> = +/-10%).</param>
    /// <param name="pan"><c>true</c> to process pan input this frame.</param>
    /// <param name="zoom"><c>true</c> to process wheel zoom input this frame.</param>
    /// <remarks>Reads the engine's mouse state and dispatches to <see cref="StartPanning"/>/<see cref="UpdatePan"/>/<see cref="EndPan"/>
    /// and <see cref="ZoomAtScreenPos"/>; intended to be called once per frame.</remarks>
    public virtual void HandlePanAndZoom(int mouseButton = 2, float zoomRate = 0.1f, bool pan = true, bool zoom = true)
    {
        var mouse = Pge.GetMousePos();
        if (pan)
        {
            if (Pge.GetMouse(mouseButton).Pressed) StartPanning(mouse);
            if (Pge.GetMouse(mouseButton).Held) UpdatePan(mouse);
            if (Pge.GetMouse(mouseButton).Released) EndPan(mouse);
        }
        if (zoom)
        {
            if (Pge.GetMouseWheel() > 0) ZoomAtScreenPos(1.0f + zoomRate, mouse);
            if (Pge.GetMouseWheel() < 0) ZoomAtScreenPos(1.0f - zoomRate, mouse);
        }
    }

    // O--- World draw wrappers (software primitives) ---O
    /// <summary>Plots a single world-space pixel (float-coordinate overload).</summary>
    /// <param name="x">World-space X coordinate.</param>
    /// <param name="y">World-space Y coordinate.</param>
    /// <param name="p">Pixel colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <returns><c>true</c> if the pixel was written; <c>false</c> if it was clipped or rejected by the engine.</returns>
    /// <seealso cref="Draw(Vector2d{float}, System.Nullable{Pixel})"/>
    public bool Draw(float x, float y, Pixel? p = null) => Draw(new Vector2d<float>(x, y), p);
    /// <summary>Plots a single world-space pixel.</summary>
    /// <param name="pos">Position in world space.</param>
    /// <param name="p">Pixel colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <returns><c>true</c> if the pixel was written; <c>false</c> if it was clipped or rejected by the engine.</returns>
    /// <remarks>Projects to screen space and forwards to the engine's <c>Draw</c>.</remarks>
    public virtual bool Draw(Vector2d<float> pos, Pixel? p = null) => Pge.Draw(WorldToScreen(pos).As<int>(), p ?? Pixel.WHITE);

    /// <summary>Draws a world-space line, with optional dashed bit pattern.</summary>
    /// <param name="pos1">Start point in world space.</param>
    /// <param name="pos2">End point in world space.</param>
    /// <param name="p">Line colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <param name="pattern">32-bit dash bit pattern (default solid); rotated one bit per plotted pixel.</param>
    /// <remarks>Projects both endpoints to screen space and forwards to the engine's <c>DrawLine</c>.</remarks>
    public void DrawLine(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null, uint pattern = 0xFFFFFFFF)
        => Pge.DrawLine(WorldToScreen(pos1).As<int>(), WorldToScreen(pos2).As<int>(), p ?? Pixel.WHITE, pattern);

    /// <summary>Draws a world-space circle outline (radius scaled by zoom).</summary>
    /// <param name="pos">Circle centre in world space.</param>
    /// <param name="radius">Circle radius in world units (scaled by the X zoom factor for the screen draw).</param>
    /// <param name="p">Outline colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <param name="mask">Octant bitmask selecting which eighths of the circle to draw (default all).</param>
    /// <seealso cref="FillCircle"/>
    public void DrawCircle(Vector2d<float> pos, float radius, Pixel? p = null, byte mask = 0xFF)
        => Pge.DrawCircle(WorldToScreen(pos).As<int>(), (int)(radius * WorldScale.X), p ?? Pixel.WHITE, mask);

    /// <summary>Draws a filled world-space circle (radius scaled by zoom).</summary>
    /// <param name="pos">Circle centre in world space.</param>
    /// <param name="radius">Circle radius in world units (scaled by the X zoom factor for the screen draw).</param>
    /// <param name="p">Fill colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawCircle"/>
    public void FillCircle(Vector2d<float> pos, float radius, Pixel? p = null)
        => Pge.FillCircle(WorldToScreen(pos).As<int>(), (int)(radius * WorldScale.X), p ?? Pixel.WHITE);

    /// <summary>Draws a world-space rectangle outline.</summary>
    /// <param name="pos">Top-left corner in world space.</param>
    /// <param name="size">Rectangle size in world units (rounded to whole screen pixels).</param>
    /// <param name="p">Outline colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="FillRect"/>
    public void DrawRect(Vector2d<float> pos, Vector2d<float> size, Pixel? p = null)
    {
        var sz = new Vector2d<int>(
            (int)MathF.Floor(size.X * WorldScale.X + 0.5f),
            (int)MathF.Floor(size.Y * WorldScale.Y + 0.5f));
        Pge.DrawRect(WorldToScreen(pos).As<int>(), sz, p ?? Pixel.WHITE);
    }

    /// <summary>Draws a filled world-space rectangle.</summary>
    /// <param name="pos">Top-left corner in world space.</param>
    /// <param name="size">Rectangle size in world units.</param>
    /// <param name="p">Fill colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawRect"/>
    public void FillRect(Vector2d<float> pos, Vector2d<float> size, Pixel? p = null)
        => Pge.FillRect(WorldToScreen(pos).As<int>(), (size * WorldScale).As<int>(), p ?? Pixel.WHITE);

    /// <summary>Draws a world-space triangle outline.</summary>
    /// <param name="p1">First vertex in world space.</param>
    /// <param name="p2">Second vertex in world space.</param>
    /// <param name="p3">Third vertex in world space.</param>
    /// <param name="p">Outline colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="FillTriangle"/>
    public void DrawTriangle(Vector2d<float> p1, Vector2d<float> p2, Vector2d<float> p3, Pixel? p = null)
        => Pge.DrawTriangle(WorldToScreen(p1).As<int>(), WorldToScreen(p2).As<int>(), WorldToScreen(p3).As<int>(), p ?? Pixel.WHITE);

    /// <summary>Draws a filled world-space triangle.</summary>
    /// <param name="p1">First vertex in world space.</param>
    /// <param name="p2">Second vertex in world space.</param>
    /// <param name="p3">Third vertex in world space.</param>
    /// <param name="p">Fill colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawTriangle"/>
    public void FillTriangle(Vector2d<float> p1, Vector2d<float> p2, Vector2d<float> p3, Pixel? p = null)
        => Pge.FillTriangle(WorldToScreen(p1).As<int>(), WorldToScreen(p2).As<int>(), WorldToScreen(p3).As<int>(), p ?? Pixel.WHITE);

    /// <summary>Draws a sprite at a world position, back-sampling per screen pixel across the zoomed footprint.</summary>
    /// <param name="pos">Top-left world-space position of the sprite.</param>
    /// <param name="sprite">Source sprite to draw.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="flip">Mirror mode for the sprite (currently accepted for API parity).</param>
    /// <remarks>Skips drawing when the footprint is off-screen (see <see cref="IsRectVisible"/>), then for each
    /// covered screen pixel back-samples the source via <c>Sprite.SamplePixel</c>.</remarks>
    /// <seealso cref="DrawPartialSprite"/>
    public void DrawSprite(Vector2d<float> pos, Sprite sprite, Vector2d<float>? scale = null, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var spriteSize = new Vector2d<float>(sprite.Width, sprite.Height);
        if (!IsRectVisible(pos, spriteSize * s)) return;

        var scaledSize = spriteSize * RecipPixel * WorldScale * s;
        var pixelStart = WorldToScreen(pos).As<int>();
        var pixelEnd = WorldToScreen(spriteSize * s + pos).As<int>();
        var screenStart = Vector2d<int>.Max(pixelStart, new Vector2d<int>(0, 0));
        var screenEnd = Vector2d<int>.Min(pixelEnd, new Vector2d<int>(Pge.ScreenWidth(), Pge.ScreenHeight()));
        var step = new Vector2d<float>(1.0f / scaledSize.X, 1.0f / scaledSize.Y);

        for (var y = screenStart.Y; y < screenEnd.Y; y++)
            for (var x = screenStart.X; x < screenEnd.X; x++)
            {
                var sample = (new Vector2d<int>(x, y) - pixelStart).As<float>() * step;
                Pge.Draw(new Vector2d<int>(x, y), sprite.SamplePixel(sample.X, sample.Y));
            }
    }

    /// <summary>Draws a sub-rectangle of a sprite at a world position, back-sampling per screen pixel.</summary>
    /// <param name="pos">Top-left world-space position of the sub-sprite.</param>
    /// <param name="sprite">Source sprite to sample from.</param>
    /// <param name="sourcePos">Top-left texel of the source sub-rectangle.</param>
    /// <param name="size">Size of the source sub-rectangle in texels.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="flip">Mirror mode for the sprite (currently accepted for API parity).</param>
    /// <remarks>Skips drawing when the footprint is off-screen, then back-samples the source sub-rectangle per
    /// covered screen pixel. Used by <see cref="DrawString"/> to blit individual font glyphs.</remarks>
    /// <seealso cref="DrawSprite"/>
    public void DrawPartialSprite(Vector2d<float> pos, Sprite sprite, Vector2d<int> sourcePos, Vector2d<int> size,
        Vector2d<float>? scale = null, SpriteMirrorMode flip = SpriteMirrorMode.None)
    {
        var s = scale ?? new Vector2d<float>(1, 1);
        var sizeF = size.As<float>();
        if (!IsRectVisible(pos, sizeF * s)) return;

        var scaledSize = sizeF * RecipPixel * WorldScale * s;
        var spritePixelStep = new Vector2d<float>(1.0f / sprite.Width, 1.0f / sprite.Height);
        var start = WorldToScreen(pos).As<int>();
        var end = scaledSize.As<int>() + start;
        var screenStep = new Vector2d<float>(1.0f / scaledSize.X, 1.0f / scaledSize.Y);

        for (var y = start.Y; y < end.Y; y++)
            for (var x = start.X; x < end.X; x++)
            {
                var sample = (new Vector2d<int>(x, y) - start).As<float>() * screenStep * sizeF * spritePixelStep
                             + sourcePos.As<float>() * spritePixelStep;
                Pge.Draw(new Vector2d<int>(x, y), sprite.SamplePixel(sample.X, sample.Y));
            }
    }

    /// <summary>Draws masked text at a world position using the engine font sprite.</summary>
    /// <param name="pos">Top-left world-space position of the first glyph.</param>
    /// <param name="text">Text to render; <c>\n</c> advances to the next line.</param>
    /// <param name="col">Glyph colour (applied via a temporary masking pixel mode).</param>
    /// <param name="scale">Per-axis glyph size multiplier in world units.</param>
    /// <remarks>Temporarily installs a masking pixel mode so only set font texels draw, blits each glyph
    /// through <see cref="DrawPartialSprite"/> from the engine font sprite, then restores the prior pixel mode.</remarks>
    public void DrawString(Vector2d<float> pos, string text, Pixel col, Vector2d<float> scale)
    {
        float offX = 0, offY = 0;
        var m = Pge.GetPixelMode();
        Pge.SetPixelMode((x, y, source, dest) => source.Red > 1 ? col : dest);
        foreach (var c in text)
        {
            if (c == '\n') { offX = 0; offY += 8.0f * RecipPixel.Y * scale.Y; }
            else
            {
                var ox = (c - 32) % 16 * 8;
                var oy = (c - 32) / 16 * 8;
                DrawPartialSprite(new Vector2d<float>(pos.X + offX, pos.Y + offY), Pge.GetFontSprite(),
                    new Vector2d<int>(ox, oy), new Vector2d<int>(8, 8), scale);
                offX += 8.0f * RecipPixel.X * scale.X;
            }
        }
        Pge.SetPixelMode(m);
    }

    // O--- World draw wrappers (GPU decals) ---O
    /// <summary>Converts a caller scale into the equivalent decal scale at the current zoom.</summary>
    /// <param name="scale">Caller-supplied world-unit scale.</param>
    /// <returns>The scale to pass to the engine decal draw, folding in <see cref="WorldScale"/> and <see cref="RecipPixel"/>.</returns>
    private Vector2d<float> DecalScale(Vector2d<float> scale) => scale * WorldScale * RecipPixel;

    /// <summary>Draws a decal at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">Top-left world-space position of the decal.</param>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawPartialDecal(Vector2d{float}, Decal, Vector2d{float}, Vector2d{float}, System.Nullable{Vector2d{float}}, System.Nullable{Pixel})"/>
    public void DrawDecal(Vector2d<float> pos, Decal decal, Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawDecal(WorldToScreen(pos), decal, DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    /// <summary>Draws a sub-rectangle of a decal at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">Top-left world-space position of the decal.</param>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="sourcePos">Top-left texel of the source sub-rectangle.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle in texels.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawDecal"/>
    public void DrawPartialDecal(Vector2d<float> pos, Decal decal, Vector2d<float> sourcePos, Vector2d<float> sourceSize,
        Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawPartialDecal(WorldToScreen(pos), decal, sourcePos, sourceSize, DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    /// <summary>Draws a sub-rectangle of a decal stretched to an explicit world-space size.</summary>
    /// <param name="pos">Top-left world-space position of the decal.</param>
    /// <param name="size">Destination size in world units (the sub-rectangle is stretched to fit).</param>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="sourcePos">Top-left texel of the source sub-rectangle.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle in texels.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawPartialDecal(Vector2d{float}, Decal, Vector2d{float}, Vector2d{float}, System.Nullable{Vector2d{float}}, System.Nullable{Pixel})"/>
    public void DrawPartialDecal(Vector2d<float> pos, Vector2d<float> size, Decal decal, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
        => Pge.DrawPartialDecal(WorldToScreen(pos), size * WorldScale * RecipPixel, decal, sourcePos, sourceSize, tint ?? Pixel.WHITE);

    /// <summary>Draws a decal with explicit per-vertex world positions, UVs, and colours.</summary>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="pos">Per-vertex world-space positions (at least <paramref name="elements"/> entries).</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="col">Per-vertex colours.</param>
    /// <param name="elements">Number of vertices to draw (default 4).</param>
    /// <remarks>Projects each of the first <paramref name="elements"/> positions to screen space before forwarding.</remarks>
    public void DrawExplicitDecal(Decal decal, Vector2d<float>[] pos, Vector2d<float>[] uv, Pixel[] col, int elements = 4)
    {
        var transformed = new Vector2d<float>[elements];
        for (var n = 0; n < elements; n++) transformed[n] = WorldToScreen(pos[n]);
        Pge.DrawExplicitDecal(decal, transformed, uv, col, elements);
    }

    /// <summary>Draws a decal warped onto four world-space corner points (projective).</summary>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="pos">Four world-space corner positions (indices 0..3) the decal is warped onto.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawPartialWarpedDecal"/>
    public void DrawWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Pixel? tint = null)
    {
        var t = new[] { WorldToScreen(pos[0]), WorldToScreen(pos[1]), WorldToScreen(pos[2]), WorldToScreen(pos[3]) };
        Pge.DrawWarpedDecal(decal, t, tint ?? Pixel.WHITE);
    }

    /// <summary>Draws a sub-rectangle of a decal warped onto four world-space corner points.</summary>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="pos">Four world-space corner positions (indices 0..3) the decal is warped onto.</param>
    /// <param name="sourcePos">Top-left texel of the source sub-rectangle.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle in texels.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawWarpedDecal"/>
    public void DrawPartialWarpedDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, Vector2d<float> sourcePos,
        Vector2d<float> sourceSize, Pixel? tint = null)
    {
        var t = new[] { WorldToScreen(pos[0]), WorldToScreen(pos[1]), WorldToScreen(pos[2]), WorldToScreen(pos[3]) };
        Pge.DrawPartialWarpedDecal(decal, t, sourcePos, sourceSize, tint ?? Pixel.WHITE);
    }

    /// <summary>Draws a decal rotated about a centre at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">World-space anchor position of the decal.</param>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Rotation pivot in decal-local texels; defaults to <c>(0, 0)</c> when null.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawPartialRotatedDecal"/>
    public void DrawRotatedDecal(Vector2d<float> pos, Decal decal, float angle, Vector2d<float>? center = null,
        Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawRotatedDecal(WorldToScreen(pos), decal, angle, center ?? new Vector2d<float>(0, 0),
            DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    /// <summary>Draws a rotated sub-rectangle of a decal at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">World-space anchor position of the decal.</param>
    /// <param name="decal">Decal (GPU texture) to draw.</param>
    /// <param name="angle">Rotation angle in radians.</param>
    /// <param name="center">Rotation pivot in decal-local texels.</param>
    /// <param name="sourcePos">Top-left texel of the source sub-rectangle.</param>
    /// <param name="sourceSize">Size of the source sub-rectangle in texels.</param>
    /// <param name="scale">Per-axis size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <param name="tint">Colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawRotatedDecal"/>
    public void DrawPartialRotatedDecal(Vector2d<float> pos, Decal decal, float angle, Vector2d<float> center,
        Vector2d<float> sourcePos, Vector2d<float> sourceSize, Vector2d<float>? scale = null, Pixel? tint = null)
        => Pge.DrawPartialRotatedDecal(WorldToScreen(pos), decal, angle, center, sourcePos, sourceSize,
            DecalScale(scale ?? new Vector2d<float>(1, 1)), tint ?? Pixel.WHITE);

    /// <summary>Draws a monospaced decal string at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">Top-left world-space position of the first glyph.</param>
    /// <param name="text">Text to render.</param>
    /// <param name="col">Glyph colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <param name="scale">Per-axis glyph size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <seealso cref="DrawStringPropDecal"/>
    public void DrawStringDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
        => Pge.DrawStringDecal(WorldToScreen(pos), text, col ?? Pixel.WHITE, DecalScale(scale ?? new Vector2d<float>(1, 1)));

    /// <summary>Draws a proportional decal string at a world position with zoom-adjusted scale.</summary>
    /// <param name="pos">Top-left world-space position of the first glyph.</param>
    /// <param name="text">Text to render.</param>
    /// <param name="col">Glyph colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <param name="scale">Per-axis glyph size multiplier in world units; defaults to <c>(1, 1)</c> when null.</param>
    /// <seealso cref="DrawStringDecal"/>
    public void DrawStringPropDecal(Vector2d<float> pos, string text, Pixel? col = null, Vector2d<float>? scale = null)
        => Pge.DrawStringPropDecal(WorldToScreen(pos), text, col ?? Pixel.WHITE, DecalScale(scale ?? new Vector2d<float>(1, 1)));

    /// <summary>Draws a filled colour-quad rectangle decal in world space.</summary>
    /// <param name="pos">Top-left corner in world space.</param>
    /// <param name="size">Rectangle size in world units (rounded up to whole screen pixels).</param>
    /// <param name="col">Fill colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawRectDecal"/>
    public void FillRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
        => Pge.FillRectDecal(WorldToScreen(pos), Ceil(size * WorldScale), col ?? Pixel.WHITE);

    /// <summary>Draws a colour-quad rectangle outline decal in world space.</summary>
    /// <param name="pos">Top-left corner in world space.</param>
    /// <param name="size">Rectangle size in world units (rounded up to whole screen pixels).</param>
    /// <param name="col">Outline colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="FillRectDecal"/>
    public void DrawRectDecal(Vector2d<float> pos, Vector2d<float> size, Pixel? col = null)
        => Pge.DrawRectDecal(WorldToScreen(pos), Ceil(size * WorldScale), col ?? Pixel.WHITE);

    /// <summary>Draws a colour-quad line decal in world space.</summary>
    /// <param name="pos1">Start point in world space.</param>
    /// <param name="pos2">End point in world space.</param>
    /// <param name="p">Line colour; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    public void DrawLineDecal(Vector2d<float> pos1, Vector2d<float> pos2, Pixel? p = null)
        => Pge.DrawLineDecal(WorldToScreen(pos1), WorldToScreen(pos2), p ?? Pixel.WHITE);

    /// <summary>Draws a per-corner gradient-filled rectangle decal in world space.</summary>
    /// <param name="pos">Top-left corner in world space.</param>
    /// <param name="size">Rectangle size in world units.</param>
    /// <param name="colTL">Colour at the top-left corner.</param>
    /// <param name="colBL">Colour at the bottom-left corner.</param>
    /// <param name="colBR">Colour at the bottom-right corner.</param>
    /// <param name="colTR">Colour at the top-right corner.</param>
    public void GradientFillRectDecal(Vector2d<float> pos, Vector2d<float> size,
        Pixel colTL, Pixel colBL, Pixel colBR, Pixel colTR)
        => Pge.GradientFillRectDecal(WorldToScreen(pos), size * WorldScale, colTL, colBL, colBR, colTR);

    /// <summary>Draws a textured polygon decal from world-space positions with a single tint.</summary>
    /// <param name="decal">Decal (GPU texture) to sample.</param>
    /// <param name="pos">World-space vertex positions.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="tint">Single colour multiplier; defaults to <see cref="Pixel.WHITE"/> when null.</param>
    /// <seealso cref="DrawPolygonDecal(Decal, IReadOnlyList{Vector2d{float}}, IReadOnlyList{Vector2d{float}}, IReadOnlyList{Pixel})"/>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, Pixel? tint = null)
        => Pge.DrawPolygonDecal(decal, Transform(pos), uv, tint ?? Pixel.WHITE);

    /// <summary>Draws a textured polygon decal from world-space positions with per-vertex tints.</summary>
    /// <param name="decal">Decal (GPU texture) to sample.</param>
    /// <param name="pos">World-space vertex positions.</param>
    /// <param name="uv">Per-vertex texture coordinates.</param>
    /// <param name="tints">Per-vertex colour multipliers.</param>
    /// <seealso cref="DrawPolygonDecal(Decal, IReadOnlyList{Vector2d{float}}, IReadOnlyList{Vector2d{float}}, System.Nullable{Pixel})"/>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> tints)
        => Pge.DrawPolygonDecal(decal, Transform(pos), uv, tints);

    /// <summary>Maps a list of world positions to screen space into a new array.</summary>
    /// <param name="pos">World-space positions to project.</param>
    /// <returns>A new array of the positions mapped to screen space, in input order.</returns>
    private Vector2d<float>[] Transform(IReadOnlyList<Vector2d<float>> pos)
    {
        var t = new Vector2d<float>[pos.Count];
        for (var n = 0; n < pos.Count; n++) t[n] = WorldToScreen(pos[n]);
        return t;
    }

    /// <summary>Component-wise ceiling of a vector.</summary>
    /// <param name="v">Vector whose components are rounded up.</param>
    /// <returns>A vector with each component rounded toward positive infinity.</returns>
    private static Vector2d<float> Ceil(Vector2d<float> v) => new(MathF.Ceiling(v.X), MathF.Ceiling(v.Y));
}

/// <summary>
/// Tile-oriented variant: world units are tiles. Initialise with a tile size and query visible tiles.
/// </summary>
/// <remarks>Extends <see cref="TransformedView"/> so one world unit equals one tile; the
/// <see cref="TransformedView.Initialise"/> pixel scale becomes the tile size in pixels.</remarks>
public class TileTransformedView : TransformedView
{
    /// <summary>Creates an uninitialised tile view; call Initialise before use.</summary>
    /// <seealso cref="TransformedView.Initialise"/>
    public TileTransformedView() { }
    /// <summary>Creates and initialises a tile view from a view area and tile size.</summary>
    /// <param name="viewArea">Size of the view in screen pixels.</param>
    /// <param name="tileSize">Tile size in pixels, passed through as the pixel scale.</param>
    public TileTransformedView(Vector2d<int> viewArea, Vector2d<int> tileSize) => Initialise(viewArea, tileSize.As<float>());

    /// <summary>Tile coordinate at the top-left corner of the view.</summary>
    /// <returns>The integer tile under the view's top-left pixel (floored).</returns>
    public Vector2d<int> GetTopLeftTile() => Floor(ScreenToWorld(new Vector2d<float>(0, 0)));
    /// <summary>Tile coordinate just past the bottom-right corner of the view.</summary>
    /// <returns>The integer tile bounding the view's bottom-right (ceiled).</returns>
    public Vector2d<int> GetBottomRightTile() => Ceiling(ScreenToWorld(ViewArea.As<float>()));
    /// <summary>Count of tiles currently visible in each axis.</summary>
    /// <returns>Bottom-right minus top-left tile, i.e. the visible tile span per axis.</returns>
    public Vector2d<int> GetVisibleTiles() => GetBottomRightTile() - GetTopLeftTile();
    /// <summary>Tile coordinate under the given screen position.</summary>
    /// <param name="pos">Screen-space position to query.</param>
    /// <returns>The integer tile under <paramref name="pos"/> (floored).</returns>
    public Vector2d<int> GetTileUnderScreenPos(Vector2d<int> pos) => Floor(ScreenToWorld(pos.As<float>()));

    /// <summary>Sub-tile pixel offset for smooth scrolling of the tile grid.</summary>
    /// <returns>The fractional part of the world offset converted to screen pixels, for aligning the tile grid.</returns>
    public Vector2d<int> GetTileOffset() => new(
        (int)((WorldOffset.X - MathF.Floor(WorldOffset.X)) * WorldScale.X),
        (int)((WorldOffset.Y - MathF.Floor(WorldOffset.Y)) * WorldScale.Y));

    /// <summary>Component-wise floor of a vector to integer tiles.</summary>
    /// <param name="v">Vector whose components are floored.</param>
    /// <returns>An integer vector with each component rounded toward negative infinity.</returns>
    private static Vector2d<int> Floor(Vector2d<float> v) => new((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y));
    /// <summary>Component-wise ceiling of a vector to integer tiles.</summary>
    /// <param name="v">Vector whose components are rounded up.</param>
    /// <returns>An integer vector with each component rounded toward positive infinity.</returns>
    private static Vector2d<int> Ceiling(Vector2d<float> v) => new((int)MathF.Ceiling(v.X), (int)MathF.Ceiling(v.Y));
}
