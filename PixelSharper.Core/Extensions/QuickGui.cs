using System;
using System.Collections.Generic;
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
// NOT ported: TextBox (needs the olc text-entry subsystem, which the engine doesn't have yet) and
// ModalDialog (a std::filesystem file browser built on that). Everything else is here.

public abstract class BaseControl
{
    protected const int MouseLeft = 0;

    public bool Visible = true;
    public bool Pressed;
    public bool Held;
    public bool Released;

    protected enum ControlState { Disabled, Normal, Hover, Click }
    protected ControlState State = ControlState.Normal;
    protected float Transition;
    protected readonly Manager ManagerRef;

    protected BaseControl(Manager manager)
    {
        ManagerRef = manager;
        ManagerRef.AddControl(this);
    }

    // Greys-out / re-enables the control.
    public void Enable(bool enable) => State = enable ? ControlState.Normal : ControlState.Disabled;

    public abstract void Update(PixelGameEngine pge);
    public abstract void Draw(PixelGameEngine pge);
    public abstract void DrawDecal(PixelGameEngine pge);

    protected static Vector2d<float> Vf(float x, float y) => new(x, y);
    protected static Vector2d<int> Vi(float x, float y) => new((int)x, (int)y);
}

// Groups controls, owns the theme, and fans Update/Draw/DrawDecal out to them.
public class Manager
{
    public Pixel ColNormal = Pixel.DARK_BLUE;
    public Pixel ColHover = Pixel.BLUE;
    public Pixel ColClick = Pixel.CYAN;
    public Pixel ColDisable = Pixel.DARK_GREY;
    public Pixel ColBorder = Pixel.WHITE;
    public Pixel ColText = Pixel.WHITE;
    public float HoverSpeedOn = 10.0f;
    public float HoverSpeedOff = 4.0f;
    public float GrabRad = 8.0f;

    private readonly List<BaseControl> _controls = new();

    // cleanUpForMe is a no-op in C# (the GC owns lifetimes) — kept for API parity.
    public Manager(bool cleanUpForMe = true) { }

    public void AddControl(BaseControl control) => _controls.Add(control);
    public int ControlCount => _controls.Count;

    public void Update(PixelGameEngine pge) { foreach (var c in _controls) c.Update(pge); }
    public void Draw(PixelGameEngine pge) { foreach (var c in _controls) c.Draw(pge); }
    public void DrawDecal(PixelGameEngine pge) { foreach (var c in _controls) c.DrawDecal(pge); }

    public void CopyThemeFrom(Manager m)
    {
        ColBorder = m.ColBorder; ColClick = m.ColClick; ColDisable = m.ColDisable;
        ColHover = m.ColHover; ColNormal = m.ColNormal; ColText = m.ColText;
        GrabRad = m.GrabRad; HoverSpeedOff = m.HoverSpeedOff; HoverSpeedOn = m.HoverSpeedOn;
    }
}

// Just text — optionally with a border/background, aligned within its box.
public class Label : BaseControl
{
    public Vector2d<float> Pos;
    public Vector2d<float> Size;
    public string Text;
    public bool HasBorder;
    public bool HasBackground;
    public enum Alignment { Left, Centre, Right }
    public Alignment Align = Alignment.Centre;

    public Label(Manager manager, string text, Vector2d<float> pos, Vector2d<float> size) : base(manager)
    {
        Pos = pos; Size = size; Text = text;
    }

    public override void Update(PixelGameEngine pge) { }

    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        if (HasBackground) pge.FillRect(Vi(Pos.X + 1, Pos.Y + 1), Vi(Size.X - 2, Size.Y - 2), ManagerRef.ColNormal);
        if (HasBorder) pge.DrawRect(Vi(Pos.X, Pos.Y), Vi(Size.X - 1, Size.Y - 1), ManagerRef.ColBorder);

        var t = pge.GetTextSizeProp(Text);
        DrawAligned(pge, t.X, t.Y, false);
    }

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

// A clickable, hover-animated, labelled rectangle.
public class Button : BaseControl
{
    public Vector2d<float> Pos;
    public Vector2d<float> Size;
    public string Text;

    public Button(Manager manager, string text, Vector2d<float> pos, Vector2d<float> size) : base(manager)
    {
        Pos = pos; Size = size; Text = text;
    }

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

// A button that toggles a checked flag, drawn with an inner mark when checked.
public class CheckBox : Button
{
    public bool Checked;

    public CheckBox(Manager manager, string text, bool check, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, text, pos, size)
    {
        Checked = check;
    }

    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        base.Update(pge);
        if (Pressed) Checked = !Checked;
    }

    public override void Draw(PixelGameEngine pge)
    {
        if (!Visible) return;
        base.Draw(pge);
        if (Checked) pge.DrawRect(Vi(Pos.X + 2, Pos.Y + 2), Vi(Size.X - 5, Size.Y - 5), ManagerRef.ColBorder);
    }

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

// A button that draws an icon (Renderable) on top.
public class ImageButton : Button
{
    protected readonly Renderable Icon;

    public ImageButton(Manager manager, Renderable icon, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, "", pos, size)
    {
        Icon = icon;
    }

    public override void Draw(PixelGameEngine pge)
    {
        base.Draw(pge);
        pge.DrawSprite(Vi(Pos.X + 4, Pos.Y + 4), Icon.Sprite);
    }

    public override void DrawDecal(PixelGameEngine pge)
    {
        base.DrawDecal(pge);
        pge.DrawDecal(Vf(Pos.X + 4, Pos.Y + 4), Icon.Decal);
    }
}

// An icon button that toggles a checked flag.
public class ImageCheckBox : ImageButton
{
    public bool Checked;

    public ImageCheckBox(Manager manager, Renderable icon, bool check, Vector2d<float> pos, Vector2d<float> size)
        : base(manager, icon, pos, size)
    {
        Checked = check;
    }

    public override void Update(PixelGameEngine pge)
    {
        if (State == ControlState.Disabled || !Visible) return;
        base.Update(pge);
        if (Pressed) Checked = !Checked;
    }

    public override void Draw(PixelGameEngine pge)
    {
        base.Draw(pge);
        if (Checked) pge.DrawRect(Vi(Pos.X + 2, Pos.Y + 2), Vi(Size.X - 5, Size.Y - 5), ManagerRef.ColBorder);
    }

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

// A grabbable handle that slides between two screen points, mapping to a [min,max] value.
public class Slider : BaseControl
{
    public float Min = -100.0f;
    public float Max = +100.0f;
    public float Value;
    public Vector2d<float> PosMin;
    public Vector2d<float> PosMax;

    public Slider(Manager manager, Vector2d<float> posMin, Vector2d<float> posMax, float valMin, float valMax, float value)
        : base(manager)
    {
        PosMin = posMin; PosMax = posMax; Min = valMin; Max = valMax; Value = value;
    }

    private Vector2d<float> HandlePos()
    {
        var t = (Value - Min) / (Max - Min);
        return Vf(PosMin.X + (PosMax.X - PosMin.X) * t, PosMin.Y + (PosMax.Y - PosMin.Y) * t);
    }

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

// A scrollable list of strings (backed by a caller-owned List<string>) with a built-in Slider.
public class ListBox : BaseControl
{
    public Vector2d<float> Pos;
    public Vector2d<float> Size;
    public bool HasBorder = true;
    public bool HasBackground = true;

    public int SelectedItem;
    public int PreviouslySelectedItem;
    public bool SelectionChanged;

    private readonly Manager _group = new();
    private readonly Slider _slider;
    private readonly List<string> _list;

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
