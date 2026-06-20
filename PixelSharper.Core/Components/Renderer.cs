// File: PixelSharper.Core/Components/Renderer.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;
using PixelSharper.Core.Resources;

namespace PixelSharper.Core.Components
{
    /// <summary>
    /// Abstract GL rendering device, the sibling of <see cref="Platform"/>. Port of olc::Renderer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Concrete backends (e.g. <c>RendererOgl10</c>, a faithful port of olc::Renderer_OGL10's
    /// fixed-function immediate mode) implement these primitives against a real graphics API. The
    /// abstract base is the seam that lets a future shader/VBO renderer sit alongside the legacy one.
    /// </para>
    /// <para>
    /// The static globals <see cref="Active"/> and <see cref="PtrPGE"/> mirror olc's namespace-global
    /// <c>renderer</c> / <c>ptrPGE</c>: there is one live device, set by the platform layer once a
    /// concrete <see cref="Renderer"/> is constructed.
    /// </para>
    /// </remarks>
    public abstract class Renderer
    {
        /// <summary>
        /// The engine this renderer serves. Mirrors olc's namespace-global <c>ptrPGE</c>; set by the
        /// platform layer during device creation.
        /// </summary>
        public static PixelGameEngine PtrPGE = null!;

        /// <summary>
        /// The single active rendering device. Mirrors olc's namespace-global <c>renderer</c>; set by
        /// the platform layer once a concrete <see cref="Renderer"/> is constructed.
        /// </summary>
        public static Renderer Active = null!;

        /// <summary>
        /// Texture ids awaiting deletion on the GL thread. GL calls are only valid on the thread that
        /// owns the context, but <c>~Decal()</c> runs on the GC finalizer thread — calling
        /// <see cref="DeleteTexture"/> from there dereferences the driver with no current context and
        /// throws <see cref="AccessViolationException"/>. So finalizers enqueue here instead, and the
        /// engine drains the queue via <see cref="ProcessPendingTextureDeletes"/> each frame.
        /// </summary>
        private static readonly ConcurrentQueue<uint> PendingTextureDeletes = new();

        /// <summary>Queues a GL texture id for deletion on the GL thread. Safe to call from any thread
        /// (notably a GC finalizer), as it performs no GL work — it only enqueues.</summary>
        /// <param name="id">The GL texture id to delete later.</param>
        public static void ScheduleTextureDelete(uint id) => PendingTextureDeletes.Enqueue(id);

        /// <summary>Deletes all queued textures. MUST be called on the thread that owns the GL context
        /// (the engine frame loop), where <see cref="DeleteTexture"/> is valid.</summary>
        public void ProcessPendingTextureDeletes()
        {
            while (PendingTextureDeletes.TryDequeue(out var id))
                DeleteTexture(id);
        }

        /// <summary>Performs one-time device preparation before a graphics context exists.</summary>
        public abstract void PrepareDevice();

        /// <summary>Creates the rendering device against an existing graphics context.</summary>
        /// <param name="parameters">Backend-specific construction parameters (e.g. the platform handles).</param>
        /// <param name="fullScreen">Whether the device targets a full-screen surface.</param>
        /// <param name="vsync">Whether vertical sync is enabled.</param>
        /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
        public abstract FileReadCode CreateDevice(List<object> parameters, bool fullScreen, bool vsync);

        /// <summary>Destroys the rendering device and releases its GL resources.</summary>
        /// <returns>A <see cref="FileReadCode"/> indicating success or failure.</returns>
        public abstract FileReadCode DestroyDevice();

        /// <summary>Presents the completed frame to the screen (swaps buffers).</summary>
        public abstract void DisplayFrame();

        /// <summary>Sets up render state at the start of a frame's drawing.</summary>
        public abstract void PrepareDrawing();

        /// <summary>Sets the active decal blend / draw mode for subsequent draws.</summary>
        /// <param name="mode">The <see cref="DecalMode"/> to apply.</param>
        public abstract void SetDecalMode(DecalMode mode);

        /// <summary>Draws a full-screen layer quad with the given offset, scale and tint.</summary>
        /// <param name="offset">The layer offset, in normalised coordinates.</param>
        /// <param name="scale">The layer scale.</param>
        /// <param name="tint">The tint <see cref="Pixel"/> applied to the layer.</param>
        public abstract void DrawLayerQuad(Vector2d<float> offset, Vector2d<float> scale, Pixel tint);

        /// <summary>Draws a queued decal instance.</summary>
        /// <param name="decal">The <see cref="DecalInstance"/> to render.</param>
        public abstract void DrawDecal(DecalInstance decal);

        /// <summary>Executes a queued GPU task (e.g. rotated decals, HW3D objects).</summary>
        /// <param name="task">The <see cref="GPUTask"/> to render.</param>
        public abstract void DoGPUTask(GPUTask task);

        /// <summary>Sets the 3D projection matrix used by HW3D draws.</summary>
        /// <param name="mat">A 16-element row/column float array (a 4x4 matrix).</param>
        public abstract void Set3DProjection(float[] mat); // Use float[16]

        /// <summary>Creates a GPU texture of the given size.</summary>
        /// <param name="size">The texture dimensions, in texels.</param>
        /// <param name="filtered">Whether to use linear filtering (otherwise nearest).</param>
        /// <param name="clamp">Whether to clamp texture coordinates (otherwise repeat).</param>
        /// <returns>The created texture's GL id.</returns>
        public abstract int CreateTexture(Vector2d<int> size, bool filtered = false, bool clamp = true);

        /// <summary>Uploads a sprite's pixel data into an existing texture.</summary>
        /// <param name="id">The target texture's GL id.</param>
        /// <param name="sprite">The <see cref="Sprite"/> whose pixels are uploaded.</param>
        public abstract void UpdateTexture(uint id, Sprite sprite);

        /// <summary>Reads a texture's pixel data back into a sprite.</summary>
        /// <param name="id">The source texture's GL id.</param>
        /// <param name="sprite">The <see cref="Sprite"/> that receives the pixels.</param>
        public abstract void ReadTexture(uint id, Sprite sprite);

        /// <summary>Deletes a GPU texture.</summary>
        /// <param name="id">The texture's GL id.</param>
        /// <returns>The deleted texture id.</returns>
        public abstract uint DeleteTexture(uint id);

        /// <summary>Binds a texture as the active one for subsequent draws.</summary>
        /// <param name="id">The texture's GL id.</param>
        public abstract void ApplyTexture(uint id);

        /// <summary>Updates the GL viewport (letterbox region) within the window.</summary>
        /// <param name="pos">The viewport top-left position, in window pixels.</param>
        /// <param name="size">The viewport size, in window pixels.</param>
        public abstract void UpdateViewport(Vector2d<int> pos, Vector2d<int> size);

        /// <summary>Clears the colour buffer (and optionally the depth buffer).</summary>
        /// <param name="p">The clear colour <see cref="Pixel"/>.</param>
        /// <param name="depth">When <c>true</c> the depth buffer is also cleared.</param>
        public abstract void ClearBuffer(Pixel p, bool depth);
    }
}
