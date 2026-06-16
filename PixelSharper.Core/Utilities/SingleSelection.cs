using System;
using System.Collections.Generic;

namespace PixelSharper.Core.Utilities;

/// <summary>Port of olcUTIL_Container::SingleSelection — a List{T} that also tracks a single "selected" item and supports reordering it (think image layers in an editor). Operates on indices, not iterators.</summary>
/// <typeparam name="T">The element type.</typeparam>
public class SingleSelection<T> : List<T>
{
    /// <summary>Index of the currently selected item.</summary>
    private int _selected;

    /// <summary>Creates an empty selection list.</summary>
    public SingleSelection() { }
    /// <summary>Creates a selection list populated from the given items.</summary>
    /// <param name="items">The initial items to copy into the list.</param>
    public SingleSelection(IEnumerable<T> items) : base(items) { }

    /// <summary>Index of the currently selected item.</summary>
    /// <value>The selected index; <c>0</c> for an empty list.</value>
    public int Selection => _selected;
    /// <summary>The currently selected item.</summary>
    /// <value>The element at <see cref="Selection"/>.</value>
    public T Selected => this[_selected];

    /// <summary>Sets the selection index, clamped to the valid range.</summary>
    /// <param name="item">The desired selection index; clamped to <c>[0, Count-1]</c> (or <c>0</c> when empty).</param>
    public void Select(int item) => _selected = Count == 0 ? 0 : Math.Clamp(item, 0, Count - 1);

    /// <summary>Moves the selected item toward the end of the list, keeping it selected.</summary>
    /// <seealso cref="MoveUp(int)"/>
    public void MoveUp() { if (MoveUp(_selected)) _selected++; }
    /// <summary>Moves the selected item toward the start of the list, keeping it selected.</summary>
    /// <seealso cref="MoveDown(int)"/>
    public void MoveDown() { if (MoveDown(_selected)) _selected--; }

    /// <summary>Swaps the item at index toward the start of the list; returns false if it can't move.</summary>
    /// <param name="item">Index of the item to move toward the start.</param>
    /// <returns><c>true</c> if the item was swapped; <c>false</c> if it is already first or the list has fewer than two items.</returns>
    public bool MoveDown(int item)
    {
        if (Count >= 2 && item > 0)
        {
            (this[item - 1], this[item]) = (this[item], this[item - 1]);
            return true;
        }
        return false;
    }

    /// <summary>Swaps the item at index toward the end of the list; returns false if it can't move.</summary>
    /// <param name="item">Index of the item to move toward the end.</param>
    /// <returns><c>true</c> if the item was swapped; <c>false</c> if it is already last or the list has fewer than two items.</returns>
    public bool MoveUp(int item)
    {
        if (Count >= 2 && item < Count - 1)
        {
            (this[item + 1], this[item]) = (this[item], this[item + 1]);
            return true;
        }
        return false;
    }

    /// <summary>Inserts a value at the given index.</summary>
    /// <param name="idx">The zero-based index at which to insert.</param>
    /// <param name="value">The value to insert.</param>
    public void InsertAfter(int idx, T value) => Insert(idx, value);
}
