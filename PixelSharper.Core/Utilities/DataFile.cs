using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PixelSharper.Core.Utilities;

// Port of olcUTIL_DataFile — a hierarchical key/value store with a simple text serialisation
// (nodes in { } blocks, properties as "name = a, b, c", '#' comments). Each property holds an
// ordered list of string values, accessed as string/real/int; child nodes are accessed by name.
public class DataFile
{
    // The list of string values that make up this property's value.
    private readonly List<string> _content = new();
    // Ordered child nodes (an "ordered map": the list preserves insertion order, the dictionary
    // maps name -> index for fast lookup).
    private readonly List<(string Name, DataFile Node)> _objects = new();
    private readonly Dictionary<string, int> _mapObjects = new();
    // Comments are preserved for round-tripping but have no assignment.
    private bool _isComment;

    public DataFile() { }

    // O--- Value access ---O
    public void SetString(string value, int item = 0)
    {
        while (item >= _content.Count) _content.Add(string.Empty);
        _content[item] = value;
    }

    public string GetString(int item = 0) => item >= 0 && item < _content.Count ? _content[item] : string.Empty;

    public double GetReal(int item = 0)
        => double.TryParse(GetString(item), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    public void SetReal(double value, int item = 0) => SetString(value.ToString(CultureInfo.InvariantCulture), item);

    public int GetInt(int item = 0)
        => int.TryParse(GetString(item), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    public void SetInt(int value, int item = 0) => SetString(value.ToString(CultureInfo.InvariantCulture), item);

    public int GetValueCount() => _content.Count;

    // O--- Child access ---O
    public bool HasProperty(string name) => _mapObjects.ContainsKey(name);

    // Access (creating if absent) a child node by name.
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

    // Access via a dotted path - "root.node.something.property".
    public DataFile GetProperty(string name)
    {
        var x = name.IndexOf('.');
        if (x < 0) return this[name];
        var head = name.Substring(0, x);
        return HasProperty(head) ? this[head].GetProperty(name.Substring(x + 1)) : this[head];
    }

    public DataFile GetIndexedProperty(string name, int index) => GetProperty($"{name}[{index}]");

    // O--- Serialisation ---O
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
