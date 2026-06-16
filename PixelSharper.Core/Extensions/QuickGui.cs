using System;
using System.Collections.Generic;
using System.IO;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.QuickGui;

// Port of olcPGEX_QuickGUI (olc::QuickGUI) — an immediate-mode-ish retained widget set. A Manager
// holds the theme + a list of controls and drives their Update/Draw/DrawDecal each frame. Controls
// store float positions; CPU primitives take Vector2d<int> (truncated here via Vi), decal draws take
// Vector2d<float> (Vf). Vector2d's scalar operators were removed, so the math is done component-wise.
//
// Complete: TextBox uses the engine's text-entry subsystem; ModalDialog is a System.IO file browser.

/// <summary>Abstract base for every QuickGUI widget: hold/pressed/released state, theme reference, and the Update/Draw/DrawDecal contract.</summary>
public abstract class BaseControl
{
    /// <summary>Mouse-button index for the left button.</summary>
    protected const int MouseLeft = 0;

    /// <summary>Whether the control is drawn and updated.</summary>
    public bool Visible = true;
    /// <summary>Set the frame the control was pressed.</summary>
    public bool Pressed;
    /// <summary>Set while the control is being held down.</summary>
    public bool Held;
    /// <summary>Set the frame the control was released.</summary>
    public bool Released;

    /// <summary>Interaction state driving the hover/click colour transition.</summary>
    protected enum ControlState
    {
        /// <summary>Disabled: the control ignores input and renders greyed.</summary>
        Disabled,
        /// <summary>Idle: no pointer interaction.</summary>
        Normal,
        /// <summary>The pointer is hovering over the control.</summary>
        Hover,
        /// <summary>The control is being pressed/held.</summary>
        Click
    }
    /// <summary>Current interaction state.</summary>
    protected ControlState State = ControlState.Normal;
    /// <summary>Normalised [0,1] hover animation progress.</summary>
    protected float Transition;
    /// <summary>Owning manager supplying the theme colours.</summary>
    protected readonly Manager ManagerRef;

    /// <summary>Construct and auto-register the control with its manager.</summary>
    /// <param name="manager">Owning <see cref="Manager"/>; the control adds itself via <see cref="Manager.AddControl"/>.</param>
    protected BaseControl(Manager manager)
    {
        ManagerRef = manager;
        ManagerRef.AddControl(this);
    }

    /// <summary>Greys-out / re-enables the control.</summary>
    /// <param name="enable"><c>true</c> to put the control in the normal interactive state; <c>false</c> to disable (grey out) it.</param>
    public void Enable(bool enable) => State = enable ? ControlState.Normal : ControlState.Disabled;

    /// <summary>Per-frame logic update (input/state).</summary>
    /// <param name="pge">Engine queried for mouse/keyboard input and elapsed time.</param>
    public abstract void Update(PixelGameEngine pge);
    /// <summary>Software (CPU primitive) render.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public abstract void Draw(PixelGameEngine pge);
    /// <summary>GPU (decal) render.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public abstract void DrawDecal(PixelGameEngine pge);

    /// <summary>Helper building a float vector for decal draws.</summary>
    /// <param name="x">X component.</param>
    /// <param name="y">Y component.</param>
    /// <returns>A <see cref="Vector2d{T}"/> of <see cref="float"/> with the given components.</returns>
    /// <seealso cref="Vi"/>
    protected static Vector2d<float> Vf(float x, float y) => new(x, y);
    /// <summary>Helper building a truncated int vector for CPU primitive draws.</summary>
    /// <param name="x">X component; truncated toward zero to <see cref="int"/>.</param>
    /// <param name="y">Y component; truncated toward zero to <see cref="int"/>.</param>
    /// <returns>A <see cref="Vector2d{T}"/> of <see cref="int"/> with the truncated components.</returns>
    /// <seealso cref="Vf"/>
    protected static Vector2d<int> Vi(float x, float y) => new((int)x, (int)y);
}

/// <summary>Groups controls, owns the theme, and fans Update/Draw/DrawDecal out to them.</summary>
public class Manager
{
    /// <summary>Fill colour in the normal state.</summary>
    public Pixel ColNormal = Pixel.DARK_BLUE;
    /// <summary>Fill colour in the hover state.</summary>
    public Pixel ColHover = Pixel.BLUE;
    /// <summary>Fill colour in the click state.</summary>
    public Pixel ColClick = Pixel.CYAN;
    /// <summary>Fill colour in the disabled state.</summary>
    public Pixel ColDisable = Pixel.DARK_GREY;
    /// <summary>Border colour.</summary>
    public Pixel ColBorder = Pixel.WHITE;
    /// <summary>Text colour.</summary>
    public Pixel ColText = Pixel.WHITE;
    /// <summary>Hover transition fade-in rate.</summary>
    public float HoverSpeedOn = 10.0f;
    /// <summary>Hover transition fade-out rate.</summary>
    public float HoverSpeedOff = 4.0f;
    /// <summary>Pixel radius used to grab the slider handle.</summary>
    public float GrabRad = 8.0f;

    /// <summary>The controls this manager drives.</summary>
    private readonly List<BaseControl> _controls = new();

    /// <summary>Construct; cleanUpForMe is a no-op in C# (the GC owns lifetimes) — kept for API parity.</summary>
    /// <param name="cleanUpForMe">Ignored; present only to mirror the olc constructor signature.</param>
    public Manager(bool cleanUpForMe = true) { }

    /// <summary>Register a control with this manager.</summary>
    /// <param name="control">Control to add to the managed set; called automatically by <see cref="BaseControl"/>'s constructor.</param>
    public void AddControl(BaseControl control) => _controls.Add(control);
    /// <summary>Number of registered controls.</summary>
    /// <value>The count of controls driven by this manager.</value>
    public int ControlCount => _controls.Count;

    /// <summary>Update every registered control.</summary>
    /// <param name="pge">Engine passed through to each control's <see cref="BaseControl.Update"/>.</param>
    public void Update(PixelGameEngine pge) { foreach (var c in _controls) c.Update(pge); }
    /// <summary>Software-draw every registered control.</summary>
    /// <param name="pge">Engine passed through to each control's <see cref="BaseControl.Draw"/>.</param>
    /// <seealso cref="DrawDecal"/>
    public void Draw(PixelGameEngine pge) { foreach (var c in _controls) c.Draw(pge); }
    /// <summary>Decal-draw every registered control.</summary>
    /// <param name="pge">Engine passed through to each control's <see cref="BaseControl.DrawDecal"/>.</param>
    /// <seealso cref="Draw"/>
    public void DrawDecal(PixelGameEngine pge) { foreach (var c in _controls) c.DrawDecal(pge); }

    /// <summary>Copy all theme colours/speeds from another manager.</summary>
    /// <param name="m">Source manager whose colours, hover speeds and grab radius are copied into this one.</param>
    public void CopyThemeFrom(Manager m)
    {
        ColBorder = m.ColBorder; ColClick = m.ColClick; ColDisable = m.ColDisable;
        ColHover = m.ColHover; ColNormal = m.ColNormal; ColText = m.ColText;
        GrabRad = m.GrabRad; HoverSpeedOff = m.HoverSpeedOff; HoverSpeedOn = m.HoverSpeedOn;
    }
}

/// <summary>Just text — optionally with a border/background, aligned within its box.</summary>
public class Label : BaseControl
{
    /// <summary>Top-left position.</summary>
    public Vector2d<float> Pos;
    /// <summary>Box dimensions.</summary>
    public Vector2d<float> Size;
    /// <summary>Displayed text.</summary>
    public string Text;
    /// <summary>Whether to draw a border rectangle.</summary>
    public bool HasBorder;
    /// <summary>Whether to fill a background.</summary>
    public bool HasBackground;
    /// <summary>Horizontal text alignment within the box.</summary>
    public enum Alignment
    {
        /// <summary>Align text to the left edge.</summary>
        Left,
        /// <summary>Centre text horizontally.</summary>
        Centre,
        /// <summary>Align text to the right edge.</summary>
        Right
    }
    /// <summary>Current horizontal alignment.</summary>
    public Alignment Align = Alignment.Centre;

    /// <summary>Construct a label at the given position and size.</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="text">Initial label text.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Box dimensions in pixels.</param>
    public Label(Manager manager, string text, Vector2d<float> pos, Vector2d<float> size) : base(manager)
    {
        Pos = pos; Size = size; Text = text;
    }

    /// <summary>Labels are inert — no per-frame logic.</summary>
    /// <param name="pge">Unused; labels do not respond to input.</param>
    public override void Update(PixelGameEngine pge) { }

    /// <summary>Software-draw the optional background/border then the aligned text.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRect(Vi(Pos.X + 1, Pos.Y + 1), Vi(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder) pge.DrawRect(Vi(Pos.X, Pos.Y), Vi(Size.X - 1, Size.Y - 1), ManagerRef.ColBorder);

        var t = pge.GetTextSizeProp(Text);
        DrawAligned(pge, t.X, t.Y, false);
    }

    /// <summary>Decal-draw the optional background/border then the aligned text.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder)
        {
            pge.SetDecalMode(DecalMode.Wireframe);
            pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColBorder);
            pge.SetDecalMode(DecalMode.Normal);
        }
        var t = pge.GetTextSizeProp(Text);
        DrawAligned(pge, t.X, t.Y, true);
    }

    /// <summary>Compute the aligned text origin and draw it (CPU or decal).</summary>
    /// <param name="pge">Engine used for the string draw call.</param>
    /// <param name="tx">Measured text width in pixels.</param>
    /// <param name="ty">Measured text height in pixels.</param>
    /// <param name="decal"><c>true</c> to draw via decal (<c>DrawStringPropDecal</c>); <c>false</c> for the CPU primitive.</param>
    private void DrawAligned(PixelGameEngine pge, float tx, float ty, bool decal)
    {
        float x = Align switch
        {
            Alignment.Left => Pos.X + 2.0f,
            Alignment.Right => Pos.X + Size.X - tx - 2.0f,
            _ => Pos.X + (Size.X - tx) * 0.5f
        };
        var y = Pos.Y + (Size.Y - ty) * 0.5f;
        if (decal) pge.DrawStringPropDecal(Vf(x, y), Text, ManagerRef.ColText);
        else pge.DrawStringProp(Vi(x, y), Text, ManagerRef.ColText);
    }
}

/// <summary>A clickable, hover-animated, labelled rectangle.</summary>
public class Button : BaseControl
{
    /// <summary>Top-left position.</summary>
    public Vector2d<float> Pos;
    /// <summary>Button dimensions.</summary>
    public Vector2d<float> Size;
    /// <summary>Button caption.</summary>
    public string Text;

    /// <summary>Construct a button at the given position and size.</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="text">Button caption.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Button dimensions in pixels.</param>
    public Button(Manager manager, string text, Vector2d<float> pos, Vector2d<float> size) : base(manager)
    {
        Pos = pos; Size = size; Text = text;
    }

    /// <summary>Hit-test the mouse, drive the hover/click state machine and pressed/held/released edges.</summary>
    /// <param name="pge">Engine queried for mouse position, button state and elapsed time.</param>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        Pressed = false; Released = false;
        var dt = pge.GetElapsedTime();
        var mp = pge.GetMousePos();
        float mx = mp.X, my = mp.Y;

        if (State != ControlState.Click)
        {
            if (mx >= Pos.X && mx < Pos.X + Size.X && my >= Pos.Y && my < Pos.Y + Size.Y)
            {
                Transition += dt * ManagerRef.HoverSpeedOn;
                State = ControlState.Hover;
                Pressed = pge.GetMouse(MouseLeft).Pressed;
                if (Pressed) State = ControlState.Click;
                Held = pge.GetMouse(MouseLeft).Held;
            }
            else
            {
                Transition -= dt * ManagerRef.HoverSpeedOff;
                State = ControlState.Normal;
            }
        }
        else
        {
            Held = pge.GetMouse(MouseLeft).Held;
            Released = pge.GetMouse(MouseLeft).Released;
            if (Released) State = ControlState.Normal;
        }

        Transition = Math.Clamp(Transition, 0.0f, 1.0f);
    }

    /// <summary>Software-draw the state-coloured fill, border and centred caption.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        var fill = State switch
        {
            ControlState.Disabled => ManagerRef.ColDisable,
            ControlState.Click => ManagerRef.ColClick,
            _ => Pixel.LinearInterpolation(ManagerRef.ColNormal, ManagerRef.ColHover, Transition)
        };
        pge.FillRect(Vi(Pos.X, Pos.Y), Vi(Size.X, Size.Y), fill);
        pge.DrawRect(Vi(Pos.X, Pos.Y), Vi(Size.X - 1, Size.Y - 1), ManagerRef.ColBorder);
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringProp(Vi(Pos.X + (Size.X - t.X) * 0.5f, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }

    /// <summary>Decal-draw the state-coloured fill, border and centred caption.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        var fill = State switch
        {
            ControlState.Disabled => ManagerRef.ColDisable,
            ControlState.Click => ManagerRef.ColClick,
            _ => Pixel.LinearInterpolation(ManagerRef.ColNormal, ManagerRef.ColHover, Transition)
        };
        pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), fill);
        pge.SetDecalMode(DecalMode.Wireframe);
        pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColBorder);
        pge.SetDecalMode(DecalMode.Normal);
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringPropDecal(Vf(Pos.X + (Size.X - t.X) * 0.5f, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }
}

/// <summary>A button that toggles a checked flag, drawn with an inner mark when checked.</summary>
public class CheckBox : Button
{
    /// <summary>Current checked state.</summary>
    public bool Checked;

    /// <summary>Construct a checkbox with an initial checked state.</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="text">Checkbox caption.</param>
    /// <param name="check">Initial value of <see cref="Checked"/>.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Checkbox dimensions in pixels.</param>
    public CheckBox(Manager manager, string text, bool check, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, text, pos, size)
    {
        Checked = check;
    }

    /// <summary>Run button logic and toggle Checked on a press.</summary>
    /// <param name="pge">Engine queried for mouse position, button state and elapsed time.</param>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        base.Update(pge);
        if (Pressed) Checked = !Checked;
    }

    /// <summary>Software-draw the button plus the inner check mark when checked.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        base.Draw(pge);
        if (Checked) pge.DrawRect(Vi(Pos.X + 2, Pos.Y + 2), Vi(Size.X - 5, Size.Y - 5), ManagerRef.ColBorder);
    }

    /// <summary>Decal-draw the button plus the inner check mark when checked.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        base.DrawDecal(pge);
        if (Checked)
        {
            pge.SetDecalMode(DecalMode.Wireframe);
            pge.FillRectDecal(Vf(Pos.X + 2, Pos.Y + 2), Vf(Size.X - 4, Size.Y - 4), ManagerRef.ColBorder);
            pge.SetDecalMode(DecalMode.Normal);
        }
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringPropDecal(Vf(Pos.X + (Size.X - t.X) * 0.5f, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }
}

/// <summary>A button that draws an icon (Renderable) on top.</summary>
public class ImageButton : Button
{
    /// <summary>The icon drawn over the button.</summary>
    protected readonly Renderable Icon;

    /// <summary>Construct an icon button (caption is empty).</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="icon"><see cref="Renderable"/> whose sprite/decal is drawn over the button.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Button dimensions in pixels.</param>
    public ImageButton(Manager manager, Renderable icon, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, "", pos, size)
    {
        Icon = icon;
    }

    /// <summary>Software-draw the button then the icon sprite.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        base.Draw(pge);
        pge.DrawSprite(Vi(Pos.X + 4, Pos.Y + 4), Icon.Sprite);
    }

    /// <summary>Decal-draw the button then the icon decal.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        base.DrawDecal(pge);
        pge.DrawDecal(Vf(Pos.X + 4, Pos.Y + 4), Icon.Decal);
    }
}

/// <summary>An icon button that toggles a checked flag.</summary>
public class ImageCheckBox : ImageButton
{
    /// <summary>Current checked state.</summary>
    public bool Checked;

    /// <summary>Construct an icon checkbox with an initial checked state.</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="icon"><see cref="Renderable"/> whose sprite/decal is drawn over the button.</param>
    /// <param name="check">Initial value of <see cref="Checked"/>.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Checkbox dimensions in pixels.</param>
    public ImageCheckBox(Manager manager, Renderable icon, bool check, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, icon, pos, size)
    {
        Checked = check;
    }

    /// <summary>Run button logic and toggle Checked on a press.</summary>
    /// <param name="pge">Engine queried for mouse position, button state and elapsed time.</param>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        base.Update(pge);
        if (Pressed) Checked = !Checked;
    }

    /// <summary>Software-draw the icon button plus the inner check mark when checked.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        base.Draw(pge);
        if (Checked) pge.DrawRect(Vi(Pos.X + 2, Pos.Y + 2), Vi(Size.X - 5, Size.Y - 5), ManagerRef.ColBorder);
    }

    /// <summary>Decal-draw the icon button with a highlight fill/icon when checked plus the inner mark.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        base.DrawDecal(pge);
        if (Checked)
        {
            pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColClick);
            pge.DrawDecal(Vf(Pos.X + 4, Pos.Y + 4), Icon.Decal);
        }
        pge.SetDecalMode(DecalMode.Wireframe);
        pge.FillRectDecal(Vf(Pos.X + 2, Pos.Y + 2), Vf(Size.X - 4, Size.Y - 4), ManagerRef.ColBorder);
        pge.SetDecalMode(DecalMode.Normal);
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringPropDecal(Vf(Pos.X + (Size.X - t.X) * 0.5f, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }
}

/// <summary>A grabbable handle that slides between two screen points, mapping to a [min,max] value.</summary>
public class Slider : BaseControl
{
    /// <summary>Value at the PosMin end.</summary>
    public float Min = -100.0f;
    /// <summary>Value at the PosMax end.</summary>
    public float Max = +100.0f;
    /// <summary>Current value.</summary>
    public float Value;
    /// <summary>Screen point mapping to the minimum value.</summary>
    public Vector2d<float> PosMin;
    /// <summary>Screen point mapping to the maximum value.</summary>
    public Vector2d<float> PosMax;

    /// <summary>Construct a slider between two screen points over a value range.</summary>
    /// <param name="manager">Owning manager supplying the theme and grab radius.</param>
    /// <param name="posMin">Screen point mapping to <paramref name="valMin"/>.</param>
    /// <param name="posMax">Screen point mapping to <paramref name="valMax"/>.</param>
    /// <param name="valMin">Value at the <paramref name="posMin"/> end.</param>
    /// <param name="valMax">Value at the <paramref name="posMax"/> end.</param>
    /// <param name="value">Initial value (clamped to the range during <see cref="Update"/>).</param>
    public Slider(Manager manager, Vector2d<float> posMin, Vector2d<float> posMax, float valMin, float valMax, float value)
        : base(manager)
    {
        PosMin = posMin; PosMax = posMax; Min = valMin; Max = valMax; Value = value;
    }

    /// <summary>Screen position of the handle for the current value.</summary>
    /// <returns>The handle's pixel position interpolated between <see cref="PosMin"/> and <see cref="PosMax"/> by the normalised value.</returns>
    private Vector2d<float> HandlePos()
    {
        var t = (Value - Min) / (Max - Min);
        return Vf(PosMin.X + (PosMax.X - PosMin.X) * t, PosMin.Y + (PosMax.Y - PosMin.Y) * t);
    }

    /// <summary>Grab/drag the handle, projecting the mouse onto the track to update Value.</summary>
    /// <param name="pge">Engine queried for mouse position, button state and elapsed time.</param>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        var dt = pge.GetElapsedTime();
        var mp = pge.GetMousePos();
        float mx = mp.X, my = mp.Y;
        Held = false;

        if (State == ControlState.Click)
        {
            float dx = PosMax.X - PosMin.X, dy = PosMax.Y - PosMin.Y;
            var mag2 = dx * dx + dy * dy;
            var u = (dx * (mx - PosMin.X) + dy * (my - PosMin.Y)) / mag2;
            Value = u * (Max - Min) + Min;
            Held = true;
        }
        else
        {
            var handle = HandlePos();
            var grab = (int)ManagerRef.GrabRad;
            var d2 = (mx - handle.X) * (mx - handle.X) + (my - handle.Y) * (my - handle.Y);
            if (d2 <= grab * grab)
            {
                Transition += dt * ManagerRef.HoverSpeedOn;
                State = ControlState.Hover;
                if (pge.GetMouse(MouseLeft).Pressed) { State = ControlState.Click; Pressed = true; }
            }
            else State = ControlState.Normal;
        }

        if (pge.GetMouse(MouseLeft).Released) { State = ControlState.Normal; Released = true; }

        if (State == ControlState.Normal)
        {
            Transition -= dt * ManagerRef.HoverSpeedOff;
            Held = false;
        }

        Value = Math.Clamp(Value, Min, Max);
        Transition = Math.Clamp(Transition, 0.0f, 1.0f);
    }

    /// <summary>Software-draw the track line and the state-coloured handle circle.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        pge.DrawLine(Vi(PosMin.X, PosMin.Y), Vi(PosMax.X, PosMax.Y), ManagerRef.ColBorder);
        var handle = HandlePos();
        var grab = (int)ManagerRef.GrabRad;
        var fill = State switch
        {
            ControlState.Disabled => ManagerRef.ColDisable,
            ControlState.Click => ManagerRef.ColClick,
            _ => Pixel.LinearInterpolation(ManagerRef.ColNormal, ManagerRef.ColHover, Transition)
        };
        pge.FillCircle(Vi(handle.X, handle.Y), grab, fill);
        pge.DrawCircle(Vi(handle.X, handle.Y), grab, ManagerRef.ColBorder);
    }

    /// <summary>Decal-draw the track line and the state-coloured handle square.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        pge.DrawLineDecal(PosMin, PosMax, ManagerRef.ColBorder);
        var handle = HandlePos();
        var grab = ManagerRef.GrabRad;
        var fill = State switch
        {
            ControlState.Disabled => ManagerRef.ColDisable,
            ControlState.Click => ManagerRef.ColClick,
            _ => Pixel.LinearInterpolation(ManagerRef.ColNormal, ManagerRef.ColHover, Transition)
        };
        pge.FillRectDecal(Vf(handle.X - grab, handle.Y - grab), Vf(grab * 2, grab * 2), fill);
        pge.SetDecalMode(DecalMode.Wireframe);
        pge.FillRectDecal(Vf(handle.X - grab, handle.Y - grab), Vf(grab * 2, grab * 2), ManagerRef.ColBorder);
        pge.SetDecalMode(DecalMode.Normal);
    }
}

/// <summary>A scrollable list of strings (backed by a caller-owned list) with a built-in Slider.</summary>
public class ListBox : BaseControl
{
    /// <summary>Top-left position.</summary>
    public Vector2d<float> Pos;
    /// <summary>Box dimensions.</summary>
    public Vector2d<float> Size;
    /// <summary>Whether to draw a border.</summary>
    public bool HasBorder = true;
    /// <summary>Whether to fill a background.</summary>
    public bool HasBackground = true;

    /// <summary>Index of the currently selected item.</summary>
    public int SelectedItem;
    /// <summary>Selection from the previous frame (used to detect changes).</summary>
    public int PreviouslySelectedItem;
    /// <summary>Set the frame the selection changed.</summary>
    public bool SelectionChanged;

    /// <summary>Private manager owning the internal scroll slider with a copied theme.</summary>
    private readonly Manager _group = new();
    /// <summary>The scroll slider.</summary>
    private readonly Slider _slider;
    /// <summary>Caller-owned backing list of item strings.</summary>
    private readonly List<string> _list;

    /// <summary>Construct a list box over a caller-owned list, building the scroll slider.</summary>
    /// <param name="manager">Owning manager supplying the theme; its theme is copied to the internal slider group.</param>
    /// <param name="list">Caller-owned backing list of item strings; the list box reads it live and does not copy it.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Box dimensions in pixels.</param>
    public ListBox(Manager manager, List<string> list, Vector2d<float> pos, Vector2d<float> size) : base(manager)
    {
        _list = list;
        _group.CopyThemeFrom(ManagerRef);
        Pos = pos; Size = size;
        _slider = new Slider(_group,
            Vf(pos.X + size.X - ManagerRef.GrabRad - 1, pos.Y + ManagerRef.GrabRad + 1),
            Vf(pos.X + size.X - ManagerRef.GrabRad - 1, pos.Y + size.Y - ManagerRef.GrabRad - 1),
            0, _list.Count, 0);
    }

    /// <summary>Handle item-click selection, clamp it, flag changes and update the scroll slider.</summary>
    /// <param name="pge">Engine queried for mouse position and button state, and passed to the internal slider group.</param>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;

        PreviouslySelectedItem = SelectedItem;
        var mp = pge.GetMousePos();
        float mx = mp.X - Pos.X + 2, my = mp.Y - Pos.Y;
        if (pge.GetMouse(MouseLeft).Pressed)
        {
            if (mx >= 0 && mx < Size.X - _group.GrabRad * 2 && my >= 0 && my < Size.Y)
                SelectedItem = (int)(_slider.Value + my / 10);
        }

        if (_list.Count > 0) SelectedItem = Math.Clamp(SelectedItem, 0, _list.Count - 1);
        SelectionChanged = SelectedItem != PreviouslySelectedItem;

        _slider.Max = _list.Count;
        _group.Update(pge);
    }

    /// <summary>Software-draw the background/border, the visible item rows (highlighting the selection) and the slider.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRect(Vi(Pos.X + 1, Pos.Y + 1), Vi(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder) pge.DrawRect(Vi(Pos.X, Pos.Y), Vi(Size.X - 1, Size.Y - 1), ManagerRef.ColBorder);

        var idx0 = (int)_slider.Value;
        var idx1 = Math.Min(idx0 + (int)((Size.Y - 4) / 10), _list.Count);
        float ty = Pos.Y + 2;
        for (var idx = idx0; idx < idx1; idx++)
        {
            if (idx == SelectedItem)
                pge.FillRect(Vi(Pos.X + 2 - 1, ty - 1), Vi(Size.X - _group.GrabRad * 2, 10), _group.ColHover);
            pge.DrawStringProp(Vi(Pos.X + 2, ty), _list[idx], ManagerRef.ColText);
            ty += 10;
        }
        _group.Draw(pge);
    }

    /// <summary>Decal-draw the background/border, the visible item rows (highlighting the selection) and the slider.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);

        var idx0 = (int)_slider.Value;
        var idx1 = Math.Min(idx0 + (int)((Size.Y - 4) / 10), _list.Count);
        float ty = Pos.Y + 2;
        for (var idx = idx0; idx < idx1; idx++)
        {
            if (idx == SelectedItem)
                pge.FillRectDecal(Vf(Pos.X + 2 - 1, ty - 1), Vf(Size.X - _group.GrabRad * 2, 10), _group.ColHover);
            pge.DrawStringPropDecal(Vf(Pos.X + 2, ty), _list[idx], ManagerRef.ColText);
            ty += 10;
        }

        if (HasBorder)
        {
            pge.SetDecalMode(DecalMode.Wireframe);
            pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColBorder);
            pge.SetDecalMode(DecalMode.Normal);
        }
        _group.DrawDecal(pge);
    }
}

/// <summary>An editable single-line text field. Clicking it begins engine text entry; clicking away (or the field losing entry) commits the typed string back into Text.</summary>
public class TextBox : Label
{
    /// <summary>Whether this field currently owns the engine text-entry session.</summary>
    private bool _textEdit;

    /// <summary>Construct a left-aligned, bordered text box.</summary>
    /// <param name="manager">Owning manager supplying the theme.</param>
    /// <param name="text">Initial field contents.</param>
    /// <param name="pos">Top-left position in pixels.</param>
    /// <param name="size">Box dimensions in pixels.</param>
    public TextBox(Manager manager, string text, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, text, pos, size)
    {
        Align = Alignment.Left;
        HasBorder = true;
        HasBackground = false;
    }

    /// <summary>Toggle engine text entry on click, commit on click-away, and sync the live string while editing.</summary>
    /// <param name="pge">Engine queried for mouse state and driving the text-entry subsystem.</param>
    /// <remarks>
    /// <para>Clicking the box takes over the engine's single text-entry session from any other field;
    /// clicking away commits the typed string back into <see cref="Label.Text"/>.</para>
    /// </remarks>
    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        Pressed = false;
        Released = false;
        var mp = pge.GetMousePos();
        float mx = mp.X, my = mp.Y;

        if (mx >= Pos.X && mx < Pos.X + Size.X && my >= Pos.Y && my < Pos.Y + Size.Y)
        {
            Pressed = pge.GetMouse(MouseLeft).Pressed;
            Released = pge.GetMouse(MouseLeft).Released;
            // Clicking this box takes over text entry from any other field.
            if (Pressed && pge.IsTextEntryEnabled() && !_textEdit) pge.TextEntryEnable(false);
            if (Pressed && !pge.IsTextEntryEnabled() && !_textEdit) { pge.TextEntryEnable(true, Text); _textEdit = true; }
            Held = pge.GetMouse(MouseLeft).Held;
        }
        else
        {
            Pressed = pge.GetMouse(MouseLeft).Pressed;
            Released = pge.GetMouse(MouseLeft).Released;
            Held = pge.GetMouse(MouseLeft).Held;
            // Clicking away commits the edit.
            if (Pressed && _textEdit) { Text = pge.TextEntryGetString(); pge.TextEntryEnable(false); _textEdit = false; }
        }

        if (_textEdit && pge.IsTextEntryEnabled()) Text = pge.TextEntryGetString();
    }

    /// <summary>Software-draw the background/border, a caret while editing, and the text.</summary>
    /// <param name="pge">Engine used for the CPU draw primitives.</param>
    /// <seealso cref="DrawDecal"/>
    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRect(Vi(Pos.X + 1, Pos.Y + 1), Vi(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder) pge.DrawRect(Vi(Pos.X, Pos.Y), Vi(Size.X - 1, Size.Y - 1), ManagerRef.ColBorder);
        if (_textEdit && pge.IsTextEntryEnabled())
        {
            var c = pge.GetTextSizeProp(Text.Substring(0, Math.Min(pge.TextEntryGetCursor(), Text.Length)));
            pge.FillRect(Vi(Pos.X + 2 + c.X, Pos.Y + (Size.Y - 10) * 0.5f), Vi(2, 10), ManagerRef.ColText);
        }
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringProp(Vi(Pos.X + 2, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }

    /// <summary>Decal-draw the background/border, a caret while editing, and the text.</summary>
    /// <param name="pge">Engine used for the decal draw calls.</param>
    /// <seealso cref="Draw"/>
    public override void DrawDecal(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder)
        {
            pge.SetDecalMode(DecalMode.Wireframe);
            pge.FillRectDecal(Vf(Pos.X + 1, Pos.Y + 1), Vf(Size.X - 2, Size.Y - 2), ManagerRef.ColBorder);
            pge.SetDecalMode(DecalMode.Normal);
        }
        if (_textEdit && pge.IsTextEntryEnabled())
        {
            var c = pge.GetTextSizeProp(Text.Substring(0, Math.Min(pge.TextEntryGetCursor(), Text.Length)));
            pge.FillRectDecal(Vf(Pos.X + 2 + c.X, Pos.Y + (Size.Y - 10) * 0.5f), Vf(2, 10), ManagerRef.ColText);
        }
        var t = pge.GetTextSizeProp(Text);
        pge.DrawStringPropDecal(Vf(Pos.X + 2, Pos.Y + (Size.Y - t.Y) * 0.5f), Text, ManagerRef.ColText);
    }
}

/// <summary>A self-hooking (PGEX) modal file-open browser built from two ListBoxes. ShowFileOpen() opens it; it blocks the user frame while shown (BACK = parent folder, a directory click descends, ESC closes).</summary>
public class ModalDialog : PGEX
{
    /// <summary>Whether the dialog is currently shown.</summary>
    private bool _showDialog;
    /// <summary>Manager owning the two list boxes.</summary>
    private readonly Manager _fileSelect = new();
    /// <summary>List box listing subdirectories.</summary>
    private readonly ListBox _listDirectory;
    /// <summary>List box listing files in the current folder.</summary>
    private readonly ListBox _listFiles;
    /// <summary>Backing list of subdirectory names.</summary>
    private readonly List<string> _directory = new();
    /// <summary>Backing list of file names.</summary>
    private readonly List<string> _files = new();
    /// <summary>Current browsing path.</summary>
    private string _path;

    /// <summary>Construct and auto-hook the dialog, starting at the current drive root.</summary>
    /// <remarks>
    /// <para>Registers as a hooked <see cref="PGEX"/> so the engine calls <see cref="OnBeforeUserUpdate"/>
    /// each frame; the dialog stays hidden until <see cref="ShowFileOpen"/> is called.</para>
    /// </remarks>
    public ModalDialog() : base(true)
    {
        _listDirectory = new ListBox(_fileSelect, _directory, new Vector2d<float>(20, 20), new Vector2d<float>(300, 500));
        _listFiles = new ListBox(_fileSelect, _files, new Vector2d<float>(330, 20), new Vector2d<float>(300, 500));
        _path = Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? Path.DirectorySeparatorChar.ToString();
        Populate();
    }

    /// <summary>Open the file-open dialog.</summary>
    /// <param name="path">Requested start path. Currently ignored — the dialog opens at its existing browsing path.</param>
    public void ShowFileOpen(string path) => _showDialog = true;

    /// <summary>Refresh the directory/file lists for the current path.</summary>
    /// <remarks>
    /// <para>Swallows <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/> so an
    /// inaccessible folder leaves the lists empty rather than throwing.</para>
    /// </remarks>
    private void Populate()
    {
        _directory.Clear();
        _files.Clear();
        try
        {
            foreach (var d in Directory.GetDirectories(_path)) _directory.Add(Path.GetFileName(d.TrimEnd(Path.DirectorySeparatorChar)));
            foreach (var f in Directory.GetFiles(_path)) _files.Add(Path.GetFileName(f));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>While shown, drive/draw the browser and block the user frame; handle BACK/descend/ESC navigation.</summary>
    /// <param name="elapsedTime">Frame delta time (passed by ref to match the <see cref="PGEX"/> hook signature); unused here.</param>
    /// <returns>
    /// <c>true</c> while the dialog is shown, which blocks the user's <c>OnUpdate</c> for that frame;
    /// <c>false</c> when the dialog is hidden or just closed (via ESCAPE).
    /// </returns>
    protected internal override bool OnBeforeUserUpdate(ref float elapsedTime)
    {
        if (!_showDialog) return false;

        _fileSelect.Update(Pge);

        if (Pge.GetKey(KeyPress.BACK).Pressed)
        {
            var parent = Directory.GetParent(_path.TrimEnd(Path.DirectorySeparatorChar));
            if (parent != null) { _path = parent.FullName; Populate(); }
        }

        if (_listDirectory.SelectionChanged && _listDirectory.SelectedItem < _directory.Count)
        {
            _path = Path.Combine(_path, _directory[_listDirectory.SelectedItem]);
            Populate();
        }

        Pge.DrawStringDecal(new Vector2d<float>(0, 0), _path);
        _fileSelect.DrawDecal(Pge);

        if (Pge.GetKey(KeyPress.ESCAPE).Pressed)
        {
            _showDialog = false;
            return false;
        }
        return true;
    }
}
