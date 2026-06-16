using System;
using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.PopUp;

// Port of olcPGEX_PopUpMenu (olc::popup). A hierarchical, table-laid-out menu system: build a tree
// of Menu nodes (each a grid of items, optionally with submenus), call Build(), then drive it via a
// Manager that stacks open panels. Rendered from an 8x8 "patch" sprite sheet (border / scroll-arrow
// / sub-menu / cursor patches laid out in a grid).
/// <summary>A hierarchical, table-laid-out menu node: a grid of items (optionally with submenus), built and rendered from an 8x8 patch sprite sheet.</summary>
public class Menu
{
    /// <summary>Size in pixels of one patch cell in the sprite sheet.</summary>
    public const int Patch = 8;

    /// <summary>User-assigned item id (-1 if unset).</summary>
    private int _id = -1;
    /// <summary>Grid dimensions (columns, rows) for laying out child items.</summary>
    private Vector2d<int> _cellTable = new(1, 0);
    /// <summary>Maps child name to its index in the items list.</summary>
    private readonly Dictionary<string, int> _itemPointer = new();
    /// <summary>Child menu items.</summary>
    private readonly List<Menu> _items = new();
    /// <summary>Computed panel size in patches.</summary>
    private Vector2d<int> _sizeInPatches = new(0, 0);
    /// <summary>Computed cell size (longest child name) in patches.</summary>
    private Vector2d<int> _cellSize = new(0, 0);
    /// <summary>Inter-cell padding in patches.</summary>
    private Vector2d<int> _cellPadding = new(2, 0);
    /// <summary>Cursor cell column/row within the grid.</summary>
    private Vector2d<int> _cellCursor = new(0, 0);
    /// <summary>Flattened index of the cursor item.</summary>
    private int _cursorItem;
    /// <summary>Topmost visible row when scrolled.</summary>
    private int _topVisibleRow;
    /// <summary>Total number of grid rows.</summary>
    private int _totalRows;
    /// <summary>Single-patch size vector.</summary>
    private static readonly Vector2d<int> PatchSize = new(Patch, Patch);
    /// <summary>Display name of this menu/item.</summary>
    private string _name = "";
    /// <summary>Screen-space cursor position (set during DrawSelf).</summary>
    private Vector2d<int> _cursorPos = new(0, 0);
    /// <summary>Whether this item is selectable.</summary>
    private bool _enabled = true;

    /// <summary>Construct an unnamed (root) menu.</summary>
    /// <seealso cref="Menu(string)"/>
    public Menu() { }
    /// <summary>Construct a named menu item.</summary>
    /// <param name="name">Display name shown for this item, also its key in the parent's lookup table.</param>
    /// <seealso cref="Menu()"/>
    public Menu(string name) { _name = name; }

    /// <summary>Fluent: set the grid table dimensions (columns by rows) used to lay out child items.</summary>
    /// <param name="columns">Number of item columns per row.</param>
    /// <param name="rows">Number of visible item rows (the panel scrolls when children exceed this).</param>
    /// <returns>This <see cref="Menu"/>, to allow fluent chaining.</returns>
    public Menu SetTable(int columns, int rows) { _cellTable = new Vector2d<int>(columns, rows); return this; }
    /// <summary>Fluent: set this item's id.</summary>
    /// <param name="id">User-assigned identifier returned later by <see cref="GetID"/>.</param>
    /// <returns>This <see cref="Menu"/>, to allow fluent chaining.</returns>
    public Menu SetID(int id) { _id = id; return this; }
    /// <summary>Fluent: enable or disable this item.</summary>
    /// <param name="b"><c>true</c> to make the item selectable; <c>false</c> to grey it out.</param>
    /// <returns>This <see cref="Menu"/>, to allow fluent chaining.</returns>
    /// <seealso cref="Enabled"/>
    public Menu Enable(bool b) { _enabled = b; return this; }

    /// <summary>This item's id.</summary>
    /// <returns>The user-assigned id set via <see cref="SetID"/>, or <c>-1</c> if unset.</returns>
    public int GetID() => _id;
    /// <summary>This item's display name.</summary>
    /// <returns>The display name, or an empty string for an unnamed root menu.</returns>
    public string GetName() => _name;
    /// <summary>Whether this item is enabled.</summary>
    /// <returns><c>true</c> if the item is selectable; <c>false</c> if disabled.</returns>
    /// <seealso cref="Enable"/>
    public bool Enabled() => _enabled;
    /// <summary>Whether this menu has child items.</summary>
    /// <returns><c>true</c> if this menu contains at least one child item; otherwise <c>false</c>.</returns>
    public bool HasChildren() => _items.Count > 0;
    /// <summary>Cell size of this item (name length wide, 1 row tall).</summary>
    /// <returns>A vector whose X is the name length and Y is <c>1</c>, in patch-grid units.</returns>
    public Vector2d<int> GetSize() => new(_name.Length, 1);
    /// <summary>Screen-space cursor position computed during the last <see cref="DrawSelf"/>.</summary>
    /// <returns>The pixel position the <see cref="Manager"/> should draw the cursor at.</returns>
    public Vector2d<int> GetCursorPosition() => _cursorPos;

    /// <summary>Access (creating if absent) a child item by name.</summary>
    /// <param name="name">Child item name; a new disabled-by-default child is created if none exists.</param>
    /// <value>The existing or newly created child <see cref="Menu"/> keyed by <paramref name="name"/>.</value>
    /// <remarks>
    /// <para>C# forbids a bare expression statement such as <c>menu["x"];</c>, so to create a
    /// value-less item purely for its side effect, discard the result: <c>_ = menu["x"]</c>.</para>
    /// </remarks>
    public Menu this[string name]
    {
        get
        {
            if (!_itemPointer.ContainsKey(name))
            {
                _itemPointer[name] = _items.Count;
                _items.Add(new Menu(name));
            }
            return _items[_itemPointer[name]];
        }
    }

    /// <summary>Recursively finalise sizes/layout. Call once after the tree is constructed.</summary>
    /// <remarks>
    /// <para>Walks every child (recursing into submenus), sizes each cell to the longest child
    /// name, then computes the panel size in patches and the total row count used for scrolling.</para>
    /// </remarks>
    public void Build()
    {
        foreach (var m in _items)
        {
            if (m.HasChildren()) m.Build();
            // Longest child name determines cell width.
            _cellSize = new Vector2d<int>(Math.Max(m.GetSize().X, _cellSize.X), Math.Max(m.GetSize().Y, _cellSize.Y));
        }

        _sizeInPatches = new Vector2d<int>(
            _cellTable.X * _cellSize.X + (_cellTable.X - 1) * _cellPadding.X + 2,
            _cellTable.Y * _cellSize.Y + (_cellTable.Y - 1) * _cellPadding.Y + 2);

        _totalRows = _items.Count / _cellTable.X + (_items.Count % _cellTable.X > 0 ? 1 : 0);
    }

    /// <summary>Render the panel's 9-slice border, scroll markers, item text, sub-menu indicators, and compute the cursor screen position.</summary>
    /// <param name="pge">Engine used for the draw calls and pixel-mode switching.</param>
    /// <param name="sprGfx">8x8-patch sprite sheet supplying the border, scroll-arrow and sub-menu glyphs.</param>
    /// <param name="screenOffset">Top-left pixel position of this panel.</param>
    /// <remarks>
    /// <para>Temporarily switches the engine to <see cref="PixelDisplayMode.Mask"/> and restores the
    /// caller's mode on exit (olc captures the mode but forgets to restore it; fixed here).</para>
    /// <para>Side effect: updates the value returned by <see cref="GetCursorPosition"/>.</para>
    /// </remarks>
    public void DrawSelf(PixelGameEngine pge, Sprite sprGfx, Vector2d<int> screenOffset)
    {
        var currentMode = pge.GetPixelMode();
        pge.SetPixelMode(PixelDisplayMode.Mask);

        // === Panel & border (9-slice from the top-left 3x3 patches) ===
        for (var px = 0; px < _sizeInPatches.X; px++)
        {
            for (var py = 0; py < _sizeInPatches.Y; py++)
            {
                var screenLoc = new Vector2d<int>(px, py) * Patch + screenOffset;
                var sx = 0;
                var sy = 0;
                if (px > 0) sx = 1;
                if (px == _sizeInPatches.X - 1) sx = 2;
                if (py > 0) sy = 1;
                if (py == _sizeInPatches.Y - 1) sy = 2;
                pge.DrawPartialSprite(screenLoc, sprGfx, new Vector2d<int>(sx, sy) * Patch, PatchSize);
            }
        }

        // === Contents ===
        var topLeftItem = _topVisibleRow * _cellTable.X;
        var bottomRightItem = Math.Min(_items.Count, _cellTable.Y * _cellTable.X + topLeftItem);
        var visibleItems = bottomRightItem - topLeftItem;

        // Scroll markers (patch column 3, rows 0/2).
        if (_topVisibleRow > 0)
        {
            var loc = new Vector2d<int>(_sizeInPatches.X - 2, 0) * Patch + screenOffset;
            pge.DrawPartialSprite(loc, sprGfx, new Vector2d<int>(3, 0) * Patch, PatchSize);
        }
        if (_totalRows - _topVisibleRow > _cellTable.Y)
        {
            var loc = new Vector2d<int>(_sizeInPatches.X - 2, _sizeInPatches.Y - 1) * Patch + screenOffset;
            pge.DrawPartialSprite(loc, sprGfx, new Vector2d<int>(3, 2) * Patch, PatchSize);
        }

        for (var i = 0; i < visibleItems; i++)
        {
            var cellX = i % _cellTable.X;
            var cellY = i / _cellTable.X;
            var patchX = cellX * (_cellSize.X + _cellPadding.X) + 1;
            var patchY = cellY * (_cellSize.Y + _cellPadding.Y) + 1;
            var loc = new Vector2d<int>(patchX, patchY) * Patch + screenOffset;

            var item = _items[topLeftItem + i];
            pge.DrawString(loc, item._name, item._enabled ? Pixel.WHITE : Pixel.DARK_GREY);

            if (item.HasChildren())
            {
                // Sub-menu indicator (patch col 3, row 1) at the end of the cell.
                var ix = cellX * (_cellSize.X + _cellPadding.X) + 1 + _cellSize.X;
                var iy = cellY * (_cellSize.Y + _cellPadding.Y) + 1;
                var iloc = new Vector2d<int>(ix, iy) * Patch + screenOffset;
                pge.DrawPartialSprite(iloc, sprGfx, new Vector2d<int>(3, 1) * Patch, PatchSize);
            }
        }

        // Cursor position in screen space (so the manager can draw it).
        _cursorPos = new Vector2d<int>(
            _cellCursor.X * (_cellSize.X + _cellPadding.X) * Patch + screenOffset.X - Patch,
            (_cellCursor.Y - _topVisibleRow) * (_cellSize.Y + _cellPadding.Y) * Patch + screenOffset.Y + Patch);

        // Restore the caller's pixel mode (olc captures it but forgets to restore — fixed here).
        pge.SetPixelMode(currentMode);
    }

    /// <summary>Clamp the cursor cell to the last valid item when it overruns the item count.</summary>
    public void ClampCursor()
    {
        _cursorItem = _cellCursor.Y * _cellTable.X + _cellCursor.X;
        if (_cursorItem >= _items.Count)
        {
            _cellCursor = new Vector2d<int>(_items.Count % _cellTable.X - 1, _items.Count / _cellTable.X);
            _cursorItem = _items.Count - 1;
        }
    }

    /// <summary>Move the cursor up one row, scrolling the view if needed.</summary>
    public void OnUp()
    {
        _cellCursor.Y--;
        if (_cellCursor.Y < 0) _cellCursor.Y = 0;
        if (_cellCursor.Y < _topVisibleRow)
        {
            _topVisibleRow--;
            if (_topVisibleRow < 0) _topVisibleRow = 0;
        }
        ClampCursor();
    }

    /// <summary>Move the cursor down one row, scrolling the view if needed.</summary>
    public void OnDown()
    {
        _cellCursor.Y++;
        if (_cellCursor.Y == _totalRows) _cellCursor.Y = _totalRows - 1;
        if (_cellCursor.Y > _topVisibleRow + _cellTable.Y - 1)
        {
            _topVisibleRow++;
            if (_topVisibleRow > _totalRows - _cellTable.Y) _topVisibleRow = _totalRows - _cellTable.Y;
        }
        ClampCursor();
    }

    /// <summary>Move the cursor left one column.</summary>
    public void OnLeft()
    {
        _cellCursor.X--;
        if (_cellCursor.X < 0) _cellCursor.X = 0;
        ClampCursor();
    }

    /// <summary>Move the cursor right one column.</summary>
    public void OnRight()
    {
        _cellCursor.X++;
        if (_cellCursor.X == _cellTable.X) _cellCursor.X = _cellTable.X - 1;
        ClampCursor();
    }

    /// <summary>Returns the submenu to descend into (if the cursor item has children), else this menu (signalling that the highlighted item itself was chosen).</summary>
    /// <returns>The cursor item's submenu if it <see cref="HasChildren"/>; otherwise this same <see cref="Menu"/>.</returns>
    /// <seealso cref="GetSelectedItem"/>
    public Menu OnConfirm() => _items[_cursorItem].HasChildren() ? _items[_cursorItem] : this;

    /// <summary>The item currently under the cursor.</summary>
    /// <returns>The child <see cref="Menu"/> at the current cursor index.</returns>
    public Menu GetSelectedItem() => _items[_cursorItem];
}

/// <summary>Drives a Menu tree: stacks the open panels and routes navigation/confirm/back to the top one.</summary>
public class Manager : PGEX
{
    /// <summary>Stack of open menu panels (top = active).</summary>
    private readonly List<Menu> _panels = new();

    /// <summary>Construct without auto-hooking the engine lifecycle.</summary>
    public Manager() : base(false) { }

    /// <summary>Close any open panels and open the given menu as the root panel.</summary>
    /// <param name="menu">Root <see cref="Menu"/> to open; should already have been <see cref="Menu.Build"/>-finalised.</param>
    /// <seealso cref="Close"/>
    public void Open(Menu menu) { Close(); _panels.Add(menu); }
    /// <summary>Close all open panels.</summary>
    /// <seealso cref="Open"/>
    public void Close() => _panels.Clear();

    /// <summary>Route an up-navigation to the top panel.</summary>
    public void OnUp() { if (_panels.Count > 0) _panels[^1].OnUp(); }
    /// <summary>Route a down-navigation to the top panel.</summary>
    public void OnDown() { if (_panels.Count > 0) _panels[^1].OnDown(); }
    /// <summary>Route a left-navigation to the top panel.</summary>
    public void OnLeft() { if (_panels.Count > 0) _panels[^1].OnLeft(); }
    /// <summary>Route a right-navigation to the top panel.</summary>
    public void OnRight() { if (_panels.Count > 0) _panels[^1].OnRight(); }
    /// <summary>Pop the top panel (back out of a submenu).</summary>
    public void OnBack() { if (_panels.Count > 0) _panels.RemoveAt(_panels.Count - 1); }

    /// <summary>Returns the chosen leaf Menu (if an enabled item was selected), or null (opened a submenu, hit a disabled item, or nothing open).</summary>
    /// <returns>
    /// The selected enabled leaf <see cref="Menu"/> when an item is chosen; otherwise <c>null</c>
    /// (a submenu was pushed onto the stack, a disabled item was hit, or no panel is open).
    /// </returns>
    /// <seealso cref="Menu.OnConfirm"/>
    public Menu OnConfirm()
    {
        if (_panels.Count == 0) return null;
        var back = _panels[^1];
        var next = back.OnConfirm();
        if (ReferenceEquals(next, back))
        {
            if (back.GetSelectedItem().Enabled()) return back.GetSelectedItem();
        }
        else if (next.Enabled())
        {
            _panels.Add(next);
        }
        return null;
    }

    /// <summary>Draw the stacked panels cascaded by a fixed offset, then the cursor over the top panel.</summary>
    /// <param name="sprGfx">8x8-patch sprite sheet supplying the panel and cursor glyphs.</param>
    /// <param name="screenOffset">Top-left pixel position of the root panel; each deeper panel is offset by a further 10 pixels in each axis.</param>
    /// <remarks>
    /// <para>Does nothing if no panel is open. Restores the engine's pixel mode after drawing the
    /// cursor (which uses <see cref="PixelDisplayMode.Alpha"/>).</para>
    /// </remarks>
    public void Draw(Sprite sprGfx, Vector2d<int> screenOffset)
    {
        if (_panels.Count == 0) return;

        foreach (var p in _panels)
        {
            p.DrawSelf(Pge, sprGfx, screenOffset);
            screenOffset += new Vector2d<int>(10, 10);
        }

        // Cursor (patch cols 4-5, rows 0-1 -> a 2x2-patch sprite), drawn over the top panel.
        var currentMode = Pge.GetPixelMode();
        Pge.SetPixelMode(PixelDisplayMode.Alpha);
        Pge.DrawPartialSprite(_panels[^1].GetCursorPosition(), sprGfx,
            new Vector2d<int>(4, 0) * Menu.Patch, new Vector2d<int>(Menu.Patch * 2, Menu.Patch * 2));
        Pge.SetPixelMode(currentMode);
    }
}
