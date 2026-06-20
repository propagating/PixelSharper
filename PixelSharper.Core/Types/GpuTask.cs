// File: PixelSharper.Core/Types/GPUTask.cs

using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;

namespace PixelSharper.Core.Types
{
    /// <summary>A queued GPU draw task (geometry + decal + transform). Reference type so it can be pooled and reused frame-to-frame; Mvp is an owned 16-float array that callers copy into, never replace.</summary>
    /// <remarks>
    /// <para>Instances are pooled and reused across frames via <see cref="Reset"/>; do not assume a fresh instance per submission.</para>
    /// <para>The <see cref="Mvp"/> array is owned by the task — callers must <c>Array.Copy</c> their matrix into it and never replace the reference, so <see cref="Reset"/> can restore identity in place.</para>
    /// </remarks>
    /// <seealso cref="LayerDesc.VecGPUTasks"/>
    public class GPUTask
    {
        /// <summary>Vertex buffer for this task.</summary>
        /// <value>The interleaved vertices to draw; cleared (capacity kept) by <see cref="Reset"/>.</value>
        public List<Vertex> Vb { get; set; } = new();
        /// <summary>Decal (texture) to draw with; null until assigned when queued.</summary>
        /// <value>The texture decal; <c>null</c> until a task is queued.</value>
        public Decal Decal { get; set; } = null!; // olc defaults decal to nullptr; assigned when a task is queued
        /// <summary>Primitive layout of the vertex buffer (Fan/Strip/List).</summary>
        /// <value>How <see cref="Vb"/> is assembled into primitives; defaults to <see cref="DecalStructure.Fan"/>.</value>
        public DecalStructure Structure { get; set; } = DecalStructure.Fan;
        /// <summary>Decal blend mode.</summary>
        /// <value>The blend mode; defaults to <see cref="DecalMode.Normal"/>.</value>
        public DecalMode Mode { get; set; } = DecalMode.Normal;
        /// <summary>Whether depth testing is enabled for this task.</summary>
        /// <value><c>true</c> to depth-test this task; otherwise <c>false</c>.</value>
        public bool Depth { get; set; }
        /// <summary>Owned model-view-projection matrix as a flat 16-float array (defaults to identity).</summary>
        /// <value>The owned flat 16-float MVP matrix; copy into it, never replace the reference.</value>
        /// <remarks>
        /// <para>This array is owned by the task. Callers must <c>Array.Copy</c> their matrix into it rather than assigning a new reference, so pooling and in-place identity restore in <see cref="Reset"/> stay correct.</para>
        /// </remarks>
        public float[] Mvp { get; set; } = Identity();
        /// <summary>Back-face cull mode.</summary>
        /// <value>The face-culling mode; defaults to <see cref="CullMode.None"/>.</value>
        public CullMode Cull { get; set; } = CullMode.None;
        /// <summary>Tint colour applied to the geometry.</summary>
        /// <value>The tint colour; defaults to <see cref="Pixel.WHITE"/>.</value>
        public Pixel Tint { get; set; } = Pixel.WHITE;

        /// <summary>Resets the task to defaults for pool reuse, restoring the owned Mvp to identity in place.</summary>
        /// <remarks>
        /// <para>Clears <see cref="Vb"/> (keeping capacity) and zeroes then re-diagonalises <see cref="Mvp"/> in place rather than allocating a new array.</para>
        /// </remarks>
        public void Reset()
        {
            Vb.Clear();
            Decal = null!;
            Structure = DecalStructure.Fan;
            Mode = DecalMode.Normal;
            Depth = false;
            Cull = CullMode.None;
            Tint = Pixel.WHITE;
            var m = Mvp;
            System.Array.Clear(m, 0, 16);
            m[0] = m[5] = m[10] = m[15] = 1.0f;
        }

        /// <summary>Allocates a fresh identity matrix as a flat 16-float array.</summary>
        /// <returns>A new 16-element array holding the 4x4 identity matrix in flat order.</returns>
        private static float[] Identity() => new float[16]
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        };
    }
}
