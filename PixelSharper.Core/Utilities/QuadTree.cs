using System.Collections;
using System.Collections.Generic;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_QuadTree (coordinate type fixed to float, olc's default CTYPE). A dynamic
// quadtree spatially indexes rectangular items for fast region queries. olc uses std::list
// iterators as stable per-item locators; C# LinkedList<T>/LinkedListNode<T> are the direct
// equivalent (stable references, O(1) removal).

// Stable handle to an item stored in a quadtree node's list, used for O(1) removal/relocation.
public struct QuadTreeItemLocation<T>
{
    public LinkedList<(Rect<float> Area, T Item)> Container;
    public LinkedListNode<(Rect<float> Area, T Item)> Node;
}

// A recursive quadtree node: holds items that don't fit a child quadrant, plus up to 4 children
// created lazily as items are inserted into them.
public class DynamicQuadTree<T>
{
    private readonly int _depth;
    private readonly int _maxDepth;
    private Rect<float> _rect;
    private readonly Rect<float>[] _childRect = new Rect<float>[4];
    private readonly DynamicQuadTree<T>[] _child = new DynamicQuadTree<T>[4];
    private readonly LinkedList<(Rect<float> Area, T Item)> _items = new();

    public DynamicQuadTree(Rect<float> size, int depth = 0, int maxDepth = 8)
    {
        _depth = depth;
        _maxDepth = maxDepth;
        Resize(size);
    }

    public Rect<float> Area => _rect;

    public void Resize(Rect<float> area)
    {
        Clear();
        _rect = area;
        var childSize = new Vector2d<float>(area.Size.X / 2, area.Size.Y / 2);
        _childRect[0] = new Rect<float>(area.Pos, childSize);
        _childRect[1] = new Rect<float>(new Vector2d<float>(area.Pos.X + childSize.X, area.Pos.Y), childSize);
        _childRect[2] = new Rect<float>(new Vector2d<float>(area.Pos.X, area.Pos.Y + childSize.Y), childSize);
        _childRect[3] = new Rect<float>(area.Pos + childSize, childSize);
    }

    // Insert an item with its bounding rect; descends into a child quadrant if it fits and depth
    // allows, otherwise stores it here. Returns a stable locator for later removal.
    public QuadTreeItemLocation<T> Insert(T item, Rect<float> itemSize)
    {
        for (var i = 0; i < 4; i++)
        {
            if (Geom2D.Contains(_childRect[i], itemSize) && _depth + 1 < _maxDepth)
            {
                _child[i] ??= new DynamicQuadTree<T>(_childRect[i], _depth + 1, _maxDepth);
                return _child[i].Insert(item, itemSize);
            }
        }

        var node = _items.AddLast((itemSize, item));
        return new QuadTreeItemLocation<T> { Container = _items, Node = node };
    }

    public int Size()
    {
        var count = _items.Count;
        for (var i = 0; i < 4; i++)
            if (_child[i] != null) count += _child[i].Size();
        return count;
    }

    // Collect items whose area overlaps the search rect (recursing only into overlapping children).
    public void Search(Rect<float> area, List<T> result)
    {
        foreach (var p in _items)
            if (Geom2D.Overlaps(area, p.Area)) result.Add(p.Item);

        for (var i = 0; i < 4; i++)
        {
            if (_child[i] == null) continue;
            if (Geom2D.Contains(area, _childRect[i])) _child[i].Items(result); // fully inside -> take all
            else if (Geom2D.Overlaps(_childRect[i], area)) _child[i].Search(area, result);
        }
    }

    public void Items(List<T> result)
    {
        foreach (var p in _items) result.Add(p.Item);
        for (var i = 0; i < 4; i++)
            if (_child[i] != null) _child[i].Items(result);
    }

    // Empties this node and drops its children. (olc's clear() kept child nodes, which left stale
    // child areas after a Resize; dropping them keeps Resize correct.)
    public void Clear()
    {
        _items.Clear();
        for (var i = 0; i < 4; i++)
        {
            _child[i]?.Clear();
            _child[i] = null;
        }
    }
}

// Owns the items in a flat list and keeps the quadtree of locators in sync, so items can be
// enumerated directly and removed/relocated in O(1). Enumerable over the stored items.
public sealed class QuadTreeContainer<T> : IEnumerable<T>
{
    internal sealed class Entry
    {
        public T Value = default!;
        public QuadTreeItemLocation<LinkedListNode<Entry>> Locator;
    }

    // Opaque handle returned by Insert, used for Remove/Relocate.
    public readonly struct Handle
    {
        internal readonly LinkedListNode<Entry> Node;
        internal Handle(LinkedListNode<Entry> node) => Node = node;
    }

    private readonly DynamicQuadTree<LinkedListNode<Entry>> _root;
    private readonly LinkedList<Entry> _allItems = new();

    public QuadTreeContainer(Rect<float> size, int depth = 0, int maxDepth = 8)
        => _root = new DynamicQuadTree<LinkedListNode<Entry>>(size, depth, maxDepth);

    public void Resize(Rect<float> area) => _root.Resize(area);

    public Handle Insert(T item, Rect<float> itemSize)
    {
        var node = _allItems.AddLast(new Entry { Value = item });
        node.Value.Locator = _root.Insert(node, itemSize);
        return new Handle(node);
    }

    public List<T> Search(Rect<float> area)
    {
        var nodes = new List<LinkedListNode<Entry>>();
        _root.Search(area, nodes);
        var result = new List<T>(nodes.Count);
        foreach (var n in nodes) result.Add(n.Value.Value);
        return result;
    }

    public void Remove(Handle handle)
    {
        var node = handle.Node;
        node.Value.Locator.Container.Remove(node.Value.Locator.Node);
        _allItems.Remove(node);
    }

    public void Relocate(Handle handle, Rect<float> itemSize)
    {
        var node = handle.Node;
        node.Value.Locator.Container.Remove(node.Value.Locator.Node);
        node.Value.Locator = _root.Insert(node, itemSize);
    }

    public int Size => _root.Size();
    public Rect<float> Area => _root.Area;
    public void Clear() { _allItems.Clear(); _root.Clear(); }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var e in _allItems) yield return e.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
