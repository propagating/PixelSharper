using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixelSharper.Core.Types;

// Mirrors olc::GPUTask::Vertex: `struct Vertex { float p[6]; uint32_t c; };`
// p holds the homogeneous position (x, y, z, w) followed by texture coords (u, v),
// and c is the packed RGBA colour. Laid out sequentially and kept allocation-free so a
// List<Vertex> can be uploaded to the GPU as an interleaved vertex buffer as-is.
[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public float X;
    public float Y;
    public float Z;
    public float W;
    public float U;
    public float V;
    public uint C;

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

    // Indexer reproducing C++ `vertex.p[i]` access over the six float components.
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
