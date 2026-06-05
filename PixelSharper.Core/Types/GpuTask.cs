// File: PixelSharper.Core/Types/GPUTask.cs
using System.Collections.Generic;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Types
{
    // A reference type so it can be pooled and reused frame-to-frame (see PixelGameEngine's per-layer
    // GPU-task pools). Mvp is an OWNED 16-float array — callers (HW3D) copy into it, never replace it,
    // so Reset() can safely restore identity in place.
    public class GPUTask
    {
        public List<Vertex> Vb { get; set; } = new();
        public Decal Decal { get; set; } // olc defaults decal to nullptr; assigned when a task is queued
        public DecalStructure Structure { get; set; } = DecalStructure.Fan;
        public DecalMode Mode { get; set; } = DecalMode.Normal;
        public bool Depth { get; set; }
        public float[] Mvp { get; set; } = Identity();
        public CullMode Cull { get; set; } = CullMode.NONE;
        public Pixel Tint { get; set; } = Pixel.WHITE;

        public void Reset()
        {
            Vb.Clear();
            Decal = null;
            Structure = DecalStructure.Fan;
            Mode = DecalMode.Normal;
            Depth = false;
            Cull = CullMode.NONE;
            Tint = Pixel.WHITE;
            var m = Mvp;
            System.Array.Clear(m, 0, 16);
            m[0] = m[5] = m[10] = m[15] = 1.0f;
        }

        private static float[] Identity() => new float[16]
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        };
    }
}
