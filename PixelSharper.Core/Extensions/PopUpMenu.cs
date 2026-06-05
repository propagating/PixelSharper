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
public class Menu
{
    public const int Patch = 8;

    private int _id = -1;
    private Vector2d<int> _cellTable = new(1, 0);
    private readonly Dictionary<string, int> _itemPointer = new();
    private readonly List<Menu> _items = new();
    private Vector2d<int> _sizeInPatches = new(0, 0);
    private Vector2d<int> _cellSize = new(0, 0);
    private Vector2d<int> _cellPadding = new(2, 0);
    private Vector2d<int> _cellCursor = new(0, 0);
    private int _cursorItem;
    private int _topVisibleRow;
    private int _totalRows;
    private static readonly Vector2d<int> PatchSize = new(Patch, Patch);
    private string _name = "";
    private Vector2d<int> _cursorPos = new(0, 0);
    private bool _enabled = true;

    public Menu() { }
    public Menu(string name) { _name = name; }

    // Fluent configuration.
    public Menu SetTable(int columns, int rows) { _cellTable = new Vector2d<int>(columns, rows); return this; }
    public Menu SetID(int id) { _id = id; return this; }
    public Menu Enable(bool b) { _enabled = b; return this; }

    public int GetID() => _id;
    public string GetName() => _name;
    public bool Enabled() => _enabled;
    public bool HasChildren() => _items.Count > 0;
    public Vector2d<int> GetSize() => new(_name.Length, 1);
    public Vector2d<int> GetCursorPosition() => _cursorPos;

    // Access (creating if absent) a child item by name.
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

    // Recursively finalise sizes/layout. Call once after the tree is constructed.
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

    public void ClampCursor()
    {
        _cursorItem = _cellCursor.Y * _cellTable.X + _cellCursor.X;
        if (_cursorItem >= _items.Count)
        {
            _cellCursor = new Vector2d<int>(_items.Count % _cellTable.X - 1, _items.Count / _cellTable.X);
            _cursorItem = _items.Count - 1;
        }
    }

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

    public void OnLeft()
    {
        _cellCursor.X--;
        if (_cellCursor.X < 0) _cellCursor.X = 0;
        ClampCursor();
    }

    public void OnRight()
    {
        _cellCursor.X++;
        if (_cellCursor.X == _cellTable.X) _cellCursor.X = _cellTable.X - 1;
        ClampCursor();
    }

    // Returns the submenu to descend into (if the cursor item has children), else this menu
    // (signalling that the highlighted item itself was chosen).
    public Menu OnConfirm() => _items[_cursorItem].HasChildren() ? _items[_cursorItem] : this;

    public Menu GetSelectedItem() => _items[_cursorItem];
}

// Drives a Menu tree: stacks the open panels and routes navigation/confirm/back to the top one.
public class Manager : PGEX
{
    private readonly List<Menu> _panels = new();

    public Manager() : base(false) { }

    public void Open(Menu menu) { Close(); _panels.Add(menu); }
    public void Close() => _panels.Clear();

    public void OnUp() { if (_panels.Count > 0) _panels[^1].OnUp(); }
    public void OnDown() { if (_panels.Count > 0) _panels[^1].OnDown(); }
    public void OnLeft() { if (_panels.Count > 0) _panels[^1].OnLeft(); }
    public void OnRight() { if (_panels.Count > 0) _panels[^1].OnRight(); }
    public void OnBack() { if (_panels.Count > 0) _panels.RemoveAt(_panels.Count - 1); }

    // Returns the chosen leaf Menu (if an enabled item was selected), or null (opened a submenu,
    // hit a disabled item, or nothing open).
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
