// File: PixelSharper.Core/Types/LayerDesc.cs
using System;
using System.Collections.Generic;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Types;
using PixelSharper.Core.Components;

namespace PixelSharper.Core.Types
{
    // A reference type (not a struct): olc accesses layers by reference (`auto& layer`) and
    // mutates them in place (toggling bUpdate, clearing decal/GPU task lists). Stored in a
    // List<LayerDesc>, struct copy semantics would silently drop those mutations.
    public class LayerDesc
    {
        public Vector2d<float> VOffset { get; set; }
        public Vector2d<float> VScale { get; set; }
        public bool BShow { get; set; }
        public bool BUpdate { get; set; }
        public Renderable PDrawTarget { get; set; }
        public uint NResID { get; set; }
        // These two lists double as reusable object pools: the first N entries (N = the *Count
        // field) are live this frame; CoreUpdate resets the count instead of clearing the list,
        // so the DecalInstance/GPUTask objects (and their vertex lists) are reused next frame.
        public List<DecalInstance> VecDecalInstance { get; set; }
        public int DecalInstanceCount;
        public List<GPUTask> VecGPUTasks { get; set; }
        public int GpuTaskCount;
        public Pixel Tint { get; set; }
        public Action FuncHook { get; set; }

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