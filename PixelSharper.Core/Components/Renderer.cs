// File: PixelSharper.Core/Components/Renderer.cs
using System;
using System.Collections.Generic;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;
using PixelSharper.Core.Resources;

namespace PixelSharper.Core.Components
{
    public abstract class Renderer
    {
        public static PixelGameEngine PtrPGE;

        // Mirrors olc's namespace-global `renderer`: the single active rendering device,
        // set by the platform layer once a concrete Renderer is constructed.
        public static Renderer Active;

        public abstract void PrepareDevice();
        public abstract FileReadCode CreateDevice(List<object> parameters, bool fullScreen, bool vsync);
        public abstract FileReadCode DestroyDevice();
        public abstract void DisplayFrame();
        public abstract void PrepareDrawing();
        public abstract void SetDecalMode(DecalMode mode);
        public abstract void DrawLayerQuad(Vector2d<float> offset, Vector2d<float> scale, Pixel tint);
        public abstract void DrawDecal(DecalInstance decal);
        public abstract void DoGPUTask(GPUTask task);
        public abstract void Set3DProjection(float[] mat); // Use float[16]
        public abstract int CreateTexture(Vector2d<int> size, bool filtered = false, bool clamp = true);
        public abstract void UpdateTexture(uint id, Sprite sprite);
        public abstract void ReadTexture(uint id, Sprite sprite);
        public abstract uint DeleteTexture(uint id);
        public abstract void ApplyTexture(uint id);
        public abstract void UpdateViewport(Vector2d<int> pos, Vector2d<int> size);
        public abstract void ClearBuffer(Pixel p, bool depth);
    }
}