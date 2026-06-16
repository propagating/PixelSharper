using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PixelSharper.Core.Utilities;

/// <summary>Port of olcUTIL_DataFile — a hierarchical key/value store with a simple text serialisation (nodes in { } blocks, properties as "name = a, b, c", '#' comments). Each property holds an ordered list of string values, accessed as string/real/int; child nodes are accessed by name.</summary>
/// <remarks>
/// <para>Text format: a node is a name followed by a <c>{ }</c> block; a property is <c>name = a, b, c</c> with comma-separated values (values containing the separator are quoted); a line beginning with <c>#</c> is a comment, preserved verbatim for round-tripping. Indentation is cosmetic.</para>
/// <para>Each <see cref="DataFile"/> is simultaneously a property (an ordered list of string values) and a node (an ordered, name-keyed map of child <see cref="DataFile"/>s).</para>
/// </remarks>
public class DataFile
{
    /// <summary>The list of string values that make up this property's value.</summary>
    private readonly List<string> _content = new();
    /// <summary>Ordered child nodes (an "ordered map": the list preserves insertion order, the dictionary maps name to index for fast lookup).</summary>
    private readonly List<(string Name, DataFile Node)> _objects = new();
    /// <summary>Maps a child node name to its index in the ordered list.</summary>
    private readonly Dictionary<string, int> _mapObjects = new();
    /// <summary>Comments are preserved for round-tripping but have no assignment.</summary>
    private bool _isComment;

    /// <summary>Creates an empty node.</summary>
    public DataFile() { }

    // O--- Value access ---O
    /// <summary>Sets the string value at the given list index, growing the list as needed.</summary>
    /// <param name="value">The string value to store.</param>
    /// <param name="item">The zero-based value index; intervening slots are filled with empty strings.</param>
    public void SetString(string value, int item = 0)
    {
        while (item >= _content.Count) _content.Add(string.Empty);
        _content[item] = value;
    }

    /// <summary>Gets the string value at the given list index (empty string if out of range).</summary>
    /// <param name="item">The zero-based value index.</param>
    /// <returns>The stored value, or an empty string if <paramref name="item"/> is out of range.</returns>
    public string GetString(int item = 0) => item >= 0 && item < _content.Count ? _content[item] : string.Empty;

    /// <summary>Parses the value at the index as a double (0 if unparseable).</summary>
    /// <param name="item">The zero-based value index.</param>
    /// <returns>The parsed double, or <c>0.0</c> if the value is missing or unparseable.</returns>
    public double GetReal(int item = 0)
        => double.TryParse(GetString(item), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    /// <summary>Stores a double value at the given index (invariant culture).</summary>
    /// <param name="value">The double value to store.</param>
    /// <param name="item">The zero-based value index.</param>
    public void SetReal(double value, int item = 0) => SetString(value.ToString(CultureInfo.InvariantCulture), item);

    /// <summary>Parses the value at the index as an int (0 if unparseable).</summary>
    /// <param name="item">The zero-based value index.</param>
    /// <returns>The parsed int, or <c>0</c> if the value is missing or unparseable.</returns>
    public int GetInt(int item = 0)
        => int.TryParse(GetString(item), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    /// <summary>Stores an int value at the given index (invariant culture).</summary>
    /// <param name="value">The int value to store.</param>
    /// <param name="item">The zero-based value index.</param>
    public void SetInt(int value, int item = 0) => SetString(value.ToString(CultureInfo.InvariantCulture), item);

    /// <summary>Number of string values held by this property.</summary>
    /// <returns>The count of string values in this property.</returns>
    public int GetValueCount() => _content.Count;

    // O--- Child access ---O
    /// <summary>Whether a child node with the given name exists.</summary>
    /// <param name="name">The child node name to test.</param>
    /// <returns><c>true</c> if a child node named <paramref name="name"/> exists; otherwise <c>false</c>.</returns>
    public bool HasProperty(string name) => _mapObjects.ContainsKey(name);

    /// <summary>Accesses (creating if absent) a child node by name.</summary>
    /// <param name="name">The child node name; a new empty node is created if it does not exist.</param>
    /// <value>The child node named <paramref name="name"/>.</value>
    /// <returns>The existing or newly created child node.</returns>
    public DataFile this[string name]
    {
        get
        {
            if (!_mapObjects.ContainsKey(name))
            {
                _mapObjects[name] = _objects.Count;
                _objects.Add((name, new DataFile()));
            }
            return _objects[_mapObjects[name]].Node;
        }
    }

    /// <summary>Accesses a descendant node via a dotted path - "root.node.something.property".</summary>
    /// <param name="name">A dotted path (e.g. <c>node.child.leaf</c>); missing nodes along the path are created.</param>
    /// <returns>The descendant node at the end of the path.</returns>
    /// <seealso cref="GetIndexedProperty"/>
    public DataFile GetProperty(string name)
    {
        var x = name.IndexOf('.');
        if (x < 0) return this[name];
        var head = name.Substring(0, x);
        return HasProperty(head) ? this[head].GetProperty(name.Substring(x + 1)) : this[head];
    }

    /// <summary>Accesses an indexed child node by the convention "name[index]".</summary>
    /// <param name="name">The base child node name.</param>
    /// <param name="index">The index appended as <c>name[index]</c>.</param>
    /// <returns>The descendant node named <c>name[index]</c>.</returns>
    /// <seealso cref="GetProperty"/>
    public DataFile GetIndexedProperty(string name, int index) => GetProperty($"{name}[{index}]");

    // O--- Serialisation ---O
    /// <summary>Writes a node tree to a text file; returns false on I/O error.</summary>
    /// <param name="node">The root node to serialise.</param>
    /// <param name="fileName">The destination file path.</param>
    /// <param name="indent">The indentation string emitted per nesting level.</param>
    /// <param name="listSep">The separator character placed between list values.</param>
    /// <returns><c>true</c> on success; <c>false</c> if an <see cref="IOException"/> occurs.</returns>
    /// <seealso cref="Read"/>
    public static bool Write(DataFile node, string fileName, string indent = "\t", char listSep = ',')
    {
        try
        {
            using var file = new StreamWriter(fileName);
            WriteNode(node, file, indent, listSep, 0);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Recursively serialises a node's properties and child blocks to the writer.</summary>
    /// <param name="n">The node whose children are serialised.</param>
    /// <param name="file">The writer to emit text to.</param>
    /// <param name="indent">The indentation string emitted per nesting level.</param>
    /// <param name="listSep">The separator character placed between list values.</param>
    /// <param name="level">The current nesting level (controls indentation depth).</param>
    private static void WriteNode(DataFile n, StreamWriter file, string indent, char listSep, int level)
    {
        var sep = listSep + " ";
        string Ind(int count)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++) sb.Append(indent);
            return sb.ToString();
        }

        foreach (var (name, prop) in n._objects)
        {
            if (prop._objects.Count == 0)
            {
                // Leaf property (or comment): name, optional " = ", then the (separated) values.
                file.Write(Ind(level) + name + (prop._isComment ? "" : " = "));
                var remaining = prop.GetValueCount();
                for (var i = 0; i < prop.GetValueCount(); i++)
                {
                    var val = prop.GetString(i);
                    // Values containing the separator are quoted.
                    var quoted = val.IndexOf(listSep) >= 0 ? "\"" + val + "\"" : val;
                    file.Write(quoted + (remaining > 1 ? sep : ""));
                    remaining--;
                }
                file.Write("\n");
            }
            else
            {
                // Node: name, then a { } block containing its properties.
                file.Write("\n" + Ind(level) + name + "\n");
                file.Write(Ind(level) + "{\n");
                WriteNode(prop, file, indent, listSep, level + 1);
                file.Write(Ind(level) + "}\n\n");
            }
        }
    }

    /// <summary>Parses a text file into the given node tree; returns false if the file is missing.</summary>
    /// <param name="node">The root node populated from the file.</param>
    /// <param name="fileName">The source file path.</param>
    /// <param name="listSep">The separator character used between list values.</param>
    /// <returns><c>true</c> if the file was read; <c>false</c> if <paramref name="fileName"/> does not exist.</returns>
    /// <seealso cref="Write"/>
    public static bool Read(DataFile node, string fileName, char listSep = ',')
    {
        if (!File.Exists(fileName)) return false;

        var stack = new Stack<DataFile>();
        stack.Push(node);
        var propName = string.Empty;

        foreach (var raw in File.ReadLines(fileName))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line[0] == '#')
            {
                // Comment: preserved as a valueless, name-only child (not indexed/looked up).
                stack.Peek()._objects.Add((line, new DataFile { _isComment = true }));
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq >= 0)
            {
                propName = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();

                // Split into list values, honouring quotes around separator-containing values.
                var inQuotes = false;
                var token = new StringBuilder();
                var tokenCount = 0;
                foreach (var c in value)
                {
                    if (c == '"') { inQuotes = !inQuotes; }
                    else if (inQuotes) { token.Append(c); }
                    else if (c == listSep)
                    {
                        stack.Peek()[propName].SetString(token.ToString().Trim(), tokenCount);
                        token.Clear();
                        tokenCount++;
                    }
                    else { token.Append(c); }
                }
                if (token.Length > 0)
                    stack.Peek()[propName].SetString(token.ToString().Trim(), tokenCount);
            }
            else if (line[0] == '{')
            {
                // The previously-seen valueless line named this node.
                stack.Push(stack.Peek()[propName]);
            }
            else if (line[0] == '}')
            {
                stack.Pop();
            }
            else
            {
                // Valueless line: the name of a node defined on the next line(s).
                propName = line;
            }
        }

        return true;
    }
}
