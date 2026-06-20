// File: PixelSharper.Core/Types/LayerDesc.cs
using System;
using System.Collections.Generic;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Types;
using PixelSharper.Core.Components;

namespace PixelSharper.Core.Types
{
    /// <summary>A render layer (draw target + decal/GPU-task queues). Reference type because olc accesses layers by reference and mutates them in place; struct copy semantics would drop those mutations in the List storage.</summary>
    /// <remarks>
    /// <para><see cref="VecDecalInstance"/> and <see cref="VecGPUTasks"/> double as reusable object pools: only the first <see cref="DecalInstanceCount"/> / <see cref="GpuTaskCount"/> entries are live each frame, and the per-frame reset zeroes those counts instead of clearing the lists.</para>
    /// </remarks>
    public class LayerDesc
    {
        /// <summary>Layer offset applied when drawing its quad.</summary>
        /// <value>The offset (in screen space) added when the layer quad is drawn.</value>
        public Vector2d<float> VOffset { get; set; }
        /// <summary>Layer scale applied when drawing its quad.</summary>
        /// <value>The per-axis scale applied when the layer quad is drawn.</value>
        public Vector2d<float> VScale { get; set; }
        /// <summary>Whether the layer is shown.</summary>
        /// <value><c>true</c> if the layer is rendered; otherwise <c>false</c>.</value>
        public bool BShow { get; set; }
        /// <summary>Whether the layer's draw-target texture needs re-uploading this frame.</summary>
        /// <value><c>true</c> if the draw-target texture is dirty and must be re-uploaded this frame.</value>
        public bool BUpdate { get; set; }
        /// <summary>The layer's draw target (sprite + decal).</summary>
        /// <value>The renderable backing this layer (its sprite and decal).</value>
        public Renderable PDrawTarget { get; set; }
        /// <summary>Resource id of the draw target's decal.</summary>
        /// <value>The renderer resource id of the draw target's decal texture.</value>
        public uint NResID { get; set; }
        // These two lists double as reusable object pools: the first N entries (N = the *Count
        // field) are live this frame; CoreUpdate resets the count instead of clearing the list,
        // so the DecalInstance/GPUTask objects (and their vertex lists) are reused next frame.
        /// <summary>Decal-instance draw queue; doubles as a pool whose first DecalInstanceCount entries are live this frame.</summary>
        /// <value>The decal draw queue and pool; only the first <see cref="DecalInstanceCount"/> entries are live.</value>
        public List<DecalInstance> VecDecalInstance { get; set; }
        /// <summary>Count of live decal instances in the pool this frame.</summary>
        /// <value>The number of live entries at the front of <see cref="VecDecalInstance"/>.</value>
        public int DecalInstanceCount;
        /// <summary>GPU-task draw queue; doubles as a pool whose first GpuTaskCount entries are live this frame.</summary>
        /// <value>The GPU-task draw queue and pool; only the first <see cref="GpuTaskCount"/> entries are live.</value>
        public List<GPUTask> VecGPUTasks { get; set; }
        /// <summary>Count of live GPU tasks in the pool this frame.</summary>
        /// <value>The number of live entries at the front of <see cref="VecGPUTasks"/>.</value>
        public int GpuTaskCount;
        /// <summary>Tint colour applied to the layer.</summary>
        /// <value>The tint colour multiplied into the layer when drawn.</value>
        public Pixel Tint { get; set; }
        /// <summary>Optional per-layer update hook invoked each frame.</summary>
        /// <value>An optional callback run each frame for this layer; <c>null</c> if none.</value>
        public Action? FuncHook { get; set; }

        /// <summary>Initialises a hidden, unit-scaled layer with an empty draw target and fresh draw queues.</summary>
        public LayerDesc()
        {
            VOffset = new Vector2d<float>(0, 0);
            VScale = new Vector2d<float>(1, 1);
            BShow = false;
            BUpdate = false;
            PDrawTarget = new Renderable();
            NResID = 0;
            VecDecalInstance = new List<DecalInstance>();
            VecGPUTasks = new List<GPUTask>();
            Tint = Pixel.WHITE;
            FuncHook = null;
        }
    }
}