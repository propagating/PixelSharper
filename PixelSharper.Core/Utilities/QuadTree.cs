using System.Collections;
using PixelSharper.Core.Types;
using PixelSharper.Core.Utilities.Geometry;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_QuadTree (coordinate type fixed to float, olc's default CTYPE). A dynamic
// quadtree spatially indexes rectangular items for fast region queries. olc uses std::list
// iterators as stable per-item locators; C# LinkedList<T>/LinkedListNode<T> are the direct
// equivalent (stable references, O(1) removal).

/// <summary>Stable handle to an item stored in a quadtree node's list, used for O(1) removal/relocation.</summary>
/// <remarks>
/// <para>olc uses <c>std::list</c> iterators as stable per-item locators; the C# port maps that to a <see cref="LinkedList{T}"/> plus the owning <see cref="LinkedListNode{T}"/>, which give the same stable reference and O(1) removal.</para>
/// </remarks>
/// <typeparam name="T">The stored item type.</typeparam>
public struct QuadTreeItemLocation<T>
{
    /// <summary>The node-owned list the item lives in.</summary>
    public LinkedList<(Rect<float> Area, T Item)> Container;
    /// <summary>The list node holding the item's area and value.</summary>
    public LinkedListNode<(Rect<float> Area, T Item)> Node;
}

/// <summary>A recursive quadtree node: holds items that don't fit a child quadrant, plus up to 4 children created lazily as items are inserted into them.</summary>
/// <typeparam name="T">The stored item type.</typeparam>
public class DynamicQuadTree<T>
{
    /// <summary>This node's depth in the tree.</summary>
    private readonly int _depth;
    /// <summary>Maximum tree depth before items stay at the current node.</summary>
    private readonly int _maxDepth;
    /// <summary>This node's covered area.</summary>
    private Rect<float> _rect;
    /// <summary>The four child quadrant areas.</summary>
    private readonly Rect<float>[] _childRect = new Rect<float>[4];
    /// <summary>The four lazily-created child nodes.</summary>
    private readonly DynamicQuadTree<T>?[] _child = new DynamicQuadTree<T>[4];
    /// <summary>Items held directly at this node (didn't fit a child).</summary>
    private readonly LinkedList<(Rect<float> Area, T Item)> _items = new();

    /// <summary>Creates a node covering the given area at the given depth.</summary>
    /// <param name="size">The area this node covers.</param>
    /// <param name="depth">This node's depth in the tree.</param>
    /// <param name="maxDepth">Maximum depth before items stay at the current node instead of descending.</param>
    public DynamicQuadTree(Rect<float> size, int depth = 0, int maxDepth = 8)
    {
        _depth = depth;
        _maxDepth = maxDepth;
        Resize(size);
    }

    /// <summary>This node's covered area.</summary>
    /// <value>The rectangle this node covers.</value>
    public Rect<float> Area => _rect;

    /// <summary>Clears the node and recomputes its area and the four child quadrant rects.</summary>
    /// <param name="area">The new area for this node; its four quadrants become the child rects.</param>
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

    /// <summary>Inserts an item with its bounding rect; descends into a child quadrant if it fits and depth allows, otherwise stores it here. Returns a stable locator for later removal.</summary>
    /// <param name="item">The item to store.</param>
    /// <param name="itemSize">The item's bounding rectangle, used to choose its quadrant.</param>
    /// <returns>A stable <see cref="QuadTreeItemLocation{T}"/> for later removal or relocation.</returns>
    public QuadTreeItemLocation<T> Insert(T item, Rect<float> itemSize)
    {
        for (var i = 0; i < 4; i++)
        {
            if (Geom2D.Contains(_childRect[i], itemSize) && _depth + 1 < _maxDepth)
            {
                _child[i] ??= new DynamicQuadTree<T>(_childRect[i], _depth + 1, _maxDepth);
                return _child[i]!.Insert(item, itemSize);
            }
        }

        var node = _items.AddLast((itemSize, item));
        return new QuadTreeItemLocation<T> { Container = _items, Node = node };
    }

    /// <summary>Total item count across this node and all descendants.</summary>
    /// <returns>The number of items held by this node and every descendant.</returns>
    public int Size()
    {
        var count = _items.Count;
        for (var i = 0; i < 4; i++)
            if (_child[i] != null) count += _child[i]!.Size();
        return count;
    }

    /// <summary>Collects items whose area overlaps the search rect (recursing only into overlapping children).</summary>
    /// <param name="area">The query rectangle.</param>
    /// <param name="result">The list that matching items are appended to.</param>
    /// <seealso cref="Items"/>
    public void Search(Rect<float> area, List<T> result)
    {
        foreach (var p in _items)
            if (Geom2D.Overlaps(area, p.Area)) result.Add(p.Item);

        for (var i = 0; i < 4; i++)
        {
            if (_child[i] == null) continue;
            if (Geom2D.Contains(area, _childRect[i])) _child[i]!.Items(result); // fully inside -> take all
            else if (Geom2D.Overlaps(_childRect[i], area)) _child[i]!.Search(area, result);
        }
    }

    /// <summary>Appends every item in this node and all descendants to the result list.</summary>
    /// <param name="result">The list that all items are appended to.</param>
    /// <seealso cref="Search"/>
    public void Items(List<T> result)
    {
        foreach (var p in _items) result.Add(p.Item);
        for (var i = 0; i < 4; i++)
            if (_child[i] != null) _child[i]!.Items(result);
    }

    /// <summary>Empties this node and drops its children. (olc's clear() kept child nodes, which left stale child areas after a Resize; dropping them keeps Resize correct.)</summary>
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

/// <summary>Owns the items in a flat list and keeps the quadtree of locators in sync, so items can be enumerated directly and removed/relocated in O(1). Enumerable over the stored items.</summary>
/// <typeparam name="T">The stored item type.</typeparam>
public sealed class QuadTreeContainer<T> : IEnumerable<T>
{
    /// <summary>A stored item paired with its locator in the quadtree.</summary>
    internal sealed class Entry
    {
        /// <summary>The user item value.</summary>
        public T Value = default!;
        /// <summary>The locator pointing back into the quadtree node list.</summary>
        public QuadTreeItemLocation<LinkedListNode<Entry>> Locator;
    }

    /// <summary>Opaque handle returned by Insert, used for Remove/Relocate.</summary>
    /// <seealso cref="Insert"/>
    /// <seealso cref="Remove"/>
    /// <seealso cref="Relocate"/>
    public readonly struct Handle
    {
        /// <summary>The backing entry node in the flat item list.</summary>
        internal readonly LinkedListNode<Entry> Node;
        /// <summary>Wraps an entry node as a handle.</summary>
        /// <param name="node">The entry node to wrap.</param>
        internal Handle(LinkedListNode<Entry> node) => Node = node;
    }

    /// <summary>The quadtree indexing item entry nodes by area.</summary>
    private readonly DynamicQuadTree<LinkedListNode<Entry>> _root;
    /// <summary>The flat list owning every stored item.</summary>
    private readonly LinkedList<Entry> _allItems = new();

    /// <summary>Creates a container backed by a quadtree over the given area.</summary>
    /// <param name="size">The area covered by the backing quadtree.</param>
    /// <param name="depth">The starting depth of the quadtree root.</param>
    /// <param name="maxDepth">Maximum quadtree depth before items stay at the current node.</param>
    public QuadTreeContainer(Rect<float> size, int depth = 0, int maxDepth = 8)
        => _root = new DynamicQuadTree<LinkedListNode<Entry>>(size, depth, maxDepth);

    /// <summary>Resizes the underlying quadtree (clears it).</summary>
    /// <param name="area">The new area for the quadtree; existing index entries are cleared.</param>
    public void Resize(Rect<float> area) => _root.Resize(area);

    /// <summary>Adds an item with its bounding rect and returns a handle for later removal/relocation.</summary>
    /// <param name="item">The item to store.</param>
    /// <param name="itemSize">The item's bounding rectangle.</param>
    /// <returns>A <see cref="Handle"/> identifying the stored item for <see cref="Remove"/> or <see cref="Relocate"/>.</returns>
    public Handle Insert(T item, Rect<float> itemSize)
    {
        var node = _allItems.AddLast(new Entry { Value = item });
        node.Value.Locator = _root.Insert(node, itemSize);
        return new Handle(node);
    }

    /// <summary>Returns all stored items whose area overlaps the search rect.</summary>
    /// <param name="area">The query rectangle.</param>
    /// <returns>A new list of items whose bounding rects overlap <paramref name="area"/>.</returns>
    public List<T> Search(Rect<float> area)
    {
        var nodes = new List<LinkedListNode<Entry>>();
        _root.Search(area, nodes);
        var result = new List<T>(nodes.Count);
        foreach (var n in nodes) result.Add(n.Value.Value);
        return result;
    }

    /// <summary>Removes the item identified by the handle from both the quadtree and the flat list.</summary>
    /// <param name="handle">The handle returned by <see cref="Insert"/> for the item to remove.</param>
    /// <seealso cref="Insert"/>
    public void Remove(Handle handle)
    {
        var node = handle.Node;
        node.Value.Locator.Container.Remove(node.Value.Locator.Node);
        _allItems.Remove(node);
    }

    /// <summary>Re-inserts the handle's item under a new bounding rect (e.g. after it moved).</summary>
    /// <param name="handle">The handle returned by <see cref="Insert"/> for the item to relocate.</param>
    /// <param name="itemSize">The item's new bounding rectangle.</param>
    public void Relocate(Handle handle, Rect<float> itemSize)
    {
        var node = handle.Node;
        node.Value.Locator.Container.Remove(node.Value.Locator.Node);
        node.Value.Locator = _root.Insert(node, itemSize);
    }

    /// <summary>Total number of stored items.</summary>
    /// <value>The number of items currently stored.</value>
    public int Size => _root.Size();
    /// <summary>The area covered by the underlying quadtree.</summary>
    /// <value>The rectangle covered by the backing quadtree.</value>
    public Rect<float> Area => _root.Area;
    /// <summary>Removes all items and empties the quadtree.</summary>
    public void Clear() { _allItems.Clear(); _root.Clear(); }

    /// <summary>Enumerates the stored items in insertion order.</summary>
    /// <returns>An enumerator over the stored items in insertion order.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var e in _allItems) yield return e.Value;
    }

    /// <summary>Non-generic enumerator over the stored items.</summary>
    /// <returns>A non-generic enumerator over the stored items.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
