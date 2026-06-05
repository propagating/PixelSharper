using System;
using System.Collections.Generic;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_Container::SingleSelection — a List<T> that also tracks a single "selected" item
// and supports reordering it (think image layers in an editor). Operates on indices, not iterators.
public class SingleSelection<T> : List<T>
{
    private int _selected;

    public SingleSelection() { }
    public SingleSelection(IEnumerable<T> items) : base(items) { }

    public int Selection => _selected;
    public T Selected => this[_selected];

    public void Select(int item) => _selected = Count == 0 ? 0 : Math.Clamp(item, 0, Count - 1);

    // Move the selected item toward the end / start of the list.
    public void MoveUp() { if (MoveUp(_selected)) _selected++; }
    public void MoveDown() { if (MoveDown(_selected)) _selected--; }

    public bool MoveDown(int item)
    {
        if (Count >= 2 && item > 0)
        {
            (this[item - 1], this[item]) = (this[item], this[item - 1]);
            return true;
        }
        return false;
    }

    public bool MoveUp(int item)
    {
        if (Count >= 2 && item < Count - 1)
        {
            (this[item + 1], this[item]) = (this[item], this[item + 1]);
            return true;
        }
        return false;
    }

    public void InsertAfter(int idx, T value) => Insert(idx, value);
}
