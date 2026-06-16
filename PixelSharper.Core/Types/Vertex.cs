using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixelSharper.Core.Types;

/// <summary>Mirrors olc::GPUTask::Vertex (float p[6] + uint32 c): homogeneous position (x,y,z,w), texture coords (u,v), and a packed RGBA colour, laid out sequentially so a List of these uploads as an interleaved vertex buffer.</summary>
/// <remarks>
/// <para>The <see cref="StructLayoutAttribute"/> with <see cref="LayoutKind.Sequential"/> keeps the fields in declaration order so a contiguous list of vertices is a valid interleaved vertex buffer.</para>
/// </remarks>
/// <seealso cref="GPUTask"/>
[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    /// <summary>Position X.</summary>
    /// <value>The x coordinate (indexer slot 0).</value>
    public float X;
    /// <summary>Position Y.</summary>
    /// <value>The y coordinate (indexer slot 1).</value>
    public float Y;
    /// <summary>Position Z.</summary>
    /// <value>The z coordinate (indexer slot 2).</value>
    public float Z;
    /// <summary>Homogeneous W.</summary>
    /// <value>The homogeneous w coordinate (indexer slot 3).</value>
    public float W;
    /// <summary>Texture U coordinate.</summary>
    /// <value>The texture u coordinate (indexer slot 4).</value>
    public float U;
    /// <summary>Texture V coordinate.</summary>
    /// <value>The texture v coordinate (indexer slot 5).</value>
    public float V;
    /// <summary>Packed RGBA colour.</summary>
    /// <value>The colour packed as a 32-bit RGBA value.</value>
    public uint C;

    /// <summary>Constructs a vertex from explicit components.</summary>
    /// <param name="x">Position X.</param>
    /// <param name="y">Position Y.</param>
    /// <param name="z">Position Z.</param>
    /// <param name="w">Homogeneous W.</param>
    /// <param name="u">Texture U coordinate.</param>
    /// <param name="v">Texture V coordinate.</param>
    /// <param name="c">Packed RGBA colour.</param>
    public Vertex(float x, float y, float z, float w, float u, float v, uint c)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
        U = u;
        V = v;
        C = c;
    }

    /// <summary>Constructs a vertex from a 6-element position/UV array and a packed colour.</summary>
    /// <param name="p">A 6-element array <c>[x, y, z, w, u, v]</c>.</param>
    /// <param name="c">Packed RGBA colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="p"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="p"/> does not contain exactly 6 elements.</exception>
    public Vertex(float[] p, uint c) : this()
    {
        if (p == null)
            throw new ArgumentNullException(nameof(p));
        if (p.Length != 6)
            throw new ArgumentException("Vertex position array must contain exactly 6 elements (x, y, z, w, u, v).", nameof(p));

        X = p[0];
        Y = p[1];
        Z = p[2];
        W = p[3];
        U = p[4];
        V = p[5];
        C = c;
    }

    /// <summary>Indexer reproducing C++ vertex.p[i] access over the six float components (0..5).</summary>
    /// <param name="index">Component index: 0=X, 1=Y, 2=Z, 3=W, 4=U, 5=V.</param>
    /// <value>The component at <paramref name="index"/>.</value>
    /// <returns>The float component at <paramref name="index"/>.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the range 0..5.</exception>
    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            4 => U,
            5 => V,
            _ => throw new IndexOutOfRangeException("Vertex component index must be in the range 0..5.")
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                case 3: W = value; break;
                case 4: U = value; break;
                case 5: V = value; break;
                default: throw new IndexOutOfRangeException("Vertex component index must be in the range 0..5.");
            }
        }
    }
}
