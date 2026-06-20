using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Renderers;

/// <summary>
/// Faithful port of olc::Renderer_OGL10 — fixed-function, immediate-mode OpenGL 1.0.
/// Context creation/swapping is delegated to the OpenTK windowing layer (the Platform sibling)
/// rather than raw WGL/GLX as in olc.
/// </summary>
/// <remarks>
/// <para>
/// Texture uploads (<see cref="UpdateTexture(uint, Sprite)"/>) and read-backs
/// (<see cref="ReadTexture(uint, Sprite)"/>) go directly to and from the blittable
/// <c>List{Pixel}</c> backing array via <see cref="System.Runtime.InteropServices.CollectionsMarshal"/>,
/// with no per-frame copy.
/// </para>
/// </remarks>
/// <seealso cref="Renderer"/>
public class RendererOgl10 : Renderer
{
    /// <summary>The OpenGL context owned by the platform window; supplied via CreateDevice.</summary>
    private IGraphicsContext? _context;

    /// <summary>
    /// Current decal mode, so SetDecalMode only touches GL state on transitions (null mirrors
    /// olc seeding nDecalMode with (DecalMode)(-1)). olc's nDecalStructure is dead state in OGL10.
    /// </summary>
    private DecalMode? _decalMode;

    /// <summary>The 3D projection matrix loaded for depth-enabled GPU tasks (column-major 4x4).</summary>
    private float[] _matProjection =
    {
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    };

    /// <summary>No-op here; OpenTK creates the context lazily in CreateDevice (olc only worked for GLUT).</summary>
    public override void PrepareDevice()
    {
        // olc only does work here for the GLUT platform; WGL/GLX (and OpenTK) create the
        // context lazily in CreateDevice, so nothing is required.
    }

    /// <summary>Takes the platform's OpenTK context (parameters[0]), makes it current, sets vsync and base GL state.</summary>
    /// <param name="parameters">Platform-supplied arguments; <c>parameters[0]</c> must be the OpenTK <c>IGraphicsContext</c>.</param>
    /// <param name="fullScreen">Whether the device is created for a full-screen window (unused in this OGL10 port).</param>
    /// <param name="vsync">When <c>true</c> the swap interval is set to 1 (vsync on); otherwise 0.</param>
    /// <returns><see cref="FileReadCode.Ok"/> on success, or <see cref="FileReadCode.Fail"/> if no valid context was supplied.</returns>
    public override FileReadCode CreateDevice(List<object> parameters, bool fullScreen, bool vsync)
    {
        // The platform hands us its OpenTK graphics context as parameters[0].
        if (parameters == null || parameters.Count == 0 || parameters[0] is not IGraphicsContext context)
            return FileReadCode.Fail;

        _context = context;
        _context.MakeCurrent();
        _context.SwapInterval = vsync ? 1 : 0;

        GL.Enable(EnableCap.Texture2D);
        GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
        return FileReadCode.Ok;
    }

    /// <summary>Drops the context reference; its lifetime is owned by the platform window.</summary>
    /// <returns>Always <see cref="FileReadCode.Ok"/>.</returns>
    public override FileReadCode DestroyDevice()
    {
        // The context's lifetime is owned by the platform window, not the renderer.
        _context = null;
        return FileReadCode.Ok;
    }

    /// <summary>Presents the rendered frame by swapping the back buffer.</summary>
    public override void DisplayFrame()
    {
        _context?.SwapBuffers();
    }

    /// <summary>Sets up per-frame GL state: alpha blending on, normal blend func, culling off.</summary>
    public override void PrepareDrawing()
    {
        GL.Enable(EnableCap.Blend);
        _decalMode = DecalMode.Normal;
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
    }

    /// <summary>Switches the GL blend function for the given decal blend mode, skipping no-op transitions.</summary>
    /// <param name="mode">The target blend mode; ignored if it already matches the current mode.</param>
    public override void SetDecalMode(DecalMode mode)
    {
        if (mode == _decalMode) return;

        switch (mode)
        {
            case DecalMode.Normal:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
            case DecalMode.Additive:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
            case DecalMode.Multiplicative:
                GL.BlendFunc(BlendingFactor.DstColor, BlendingFactor.OneMinusSrcAlpha);
                break;
            case DecalMode.Stencil:
                GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.SrcAlpha);
                break;
            case DecalMode.Illuminate:
                GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha);
                break;
            case DecalMode.Wireframe:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
        }

        _decalMode = mode;
    }

    /// <summary>Draws a full-screen textured quad for a layer, applying its UV offset/scale and tint.</summary>
    /// <param name="offset">UV offset added to each corner texture coordinate.</param>
    /// <param name="scale">UV scale applied to each corner texture coordinate.</param>
    /// <param name="tint">Colour multiplied into the quad.</param>
    public override void DrawLayerQuad(Vector2d<float> offset, Vector2d<float> scale, Pixel tint)
    {
        GL.Disable(EnableCap.CullFace);
        GL.Begin(PrimitiveType.Quads);
        GL.Color4(tint.Red, tint.Green, tint.Blue, tint.Alpha);
        GL.TexCoord2(0.0f * scale.X + offset.X, 1.0f * scale.Y + offset.Y);
        GL.Vertex3(-1.0f, -1.0f, 0.0f);
        GL.TexCoord2(0.0f * scale.X + offset.X, 0.0f * scale.Y + offset.Y);
        GL.Vertex3(-1.0f, 1.0f, 0.0f);
        GL.TexCoord2(1.0f * scale.X + offset.X, 0.0f * scale.Y + offset.Y);
        GL.Vertex3(1.0f, 1.0f, 0.0f);
        GL.TexCoord2(1.0f * scale.X + offset.X, 1.0f * scale.Y + offset.Y);
        GL.Vertex3(1.0f, -1.0f, 0.0f);
        GL.End();
    }

    /// <summary>Renders a queued decal instance as a 2D or depth-tested 3D textured primitive.</summary>
    /// <param name="decal">The queued decal instance carrying texture, blend mode, structure, and per-vertex data.</param>
    /// <seealso cref="DoGPUTask(GPUTask)"/>
    public override void DrawDecal(DecalInstance decal)
    {
        SetDecalMode(decal.Mode);

        GL.BindTexture(TextureTarget.Texture2D, decal.Decal?.Id ?? 0);
        GL.Disable(EnableCap.CullFace);

        if (decal.Depth)
            GL.Enable(EnableCap.DepthTest);

        if (_decalMode == DecalMode.Wireframe)
        {
            GL.Begin(PrimitiveType.LineLoop);
        }
        else
        {
            GL.Begin(decal.Structure switch
            {
                DecalStructure.Fan => PrimitiveType.TriangleFan,
                DecalStructure.Strip => PrimitiveType.TriangleStrip,
                DecalStructure.List => PrimitiveType.Triangles,
                DecalStructure.Line => PrimitiveType.LineStrip,
                _ => PrimitiveType.TriangleFan
            });
        }

        var points = (int)decal.Points;
        if (decal.Depth)
        {
            // Render as 3D spatial entity
            for (var n = 0; n < points; n++)
            {
                var tint = decal.Tint[n];
                GL.Color4(tint.Red, tint.Green, tint.Blue, tint.Alpha);
                GL.TexCoord4(decal.Uv[n].X, decal.Uv[n].Y, 0.0f, decal.W[n]);
                GL.Vertex3(decal.Pos[n].X, decal.Pos[n].Y, decal.Z[n]);
            }
        }
        else
        {
            // Render as 2D spatial entity
            for (var n = 0; n < points; n++)
            {
                var tint = decal.Tint[n];
                GL.Color4(tint.Red, tint.Green, tint.Blue, tint.Alpha);
                GL.TexCoord4(decal.Uv[n].X, decal.Uv[n].Y, 0.0f, decal.W[n]);
                GL.Vertex2(decal.Pos[n].X, decal.Pos[n].Y);
            }
        }

        GL.End();

        if (decal.Depth)
            GL.Disable(EnableCap.DepthTest);
    }

    /// <summary>Renders a GPU task: applies cull/depth state and MVP matrices, then draws its tinted vertex buffer.</summary>
    /// <param name="task">The GPU task carrying texture, cull/depth flags, MVP matrix, tint, and vertex buffer.</param>
    /// <seealso cref="DrawDecal(DecalInstance)"/>
    public override void DoGPUTask(GPUTask task)
    {
        SetDecalMode(task.Mode);

        GL.BindTexture(TextureTarget.Texture2D, task.Decal?.Id ?? 0);

        GL.MatrixMode(MatrixMode.Projection);
        GL.PushMatrix();
        GL.MatrixMode(MatrixMode.Modelview);
        GL.PushMatrix();

        switch (task.Cull)
        {
            case CullMode.None:
                GL.CullFace(TriangleFace.Front);
                GL.Disable(EnableCap.CullFace);
                break;
            case CullMode.ClockWise:
                GL.CullFace(TriangleFace.Front);
                GL.Enable(EnableCap.CullFace);
                break;
            case CullMode.CounterClockWise:
                GL.CullFace(TriangleFace.Back);
                GL.Enable(EnableCap.CullFace);
                break;
        }

        if (task.Depth)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(_matProjection);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(task.Mvp);
        }
        else
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        if (_decalMode == DecalMode.Wireframe)
        {
            GL.Begin(PrimitiveType.LineLoop);
        }
        else
        {
            GL.Begin(task.Structure switch
            {
                DecalStructure.Fan => PrimitiveType.TriangleFan,
                DecalStructure.Strip => PrimitiveType.TriangleStrip,
                DecalStructure.List => PrimitiveType.Triangles,
                DecalStructure.Line => PrimitiveType.Lines,
                _ => PrimitiveType.TriangleFan
            });
        }

        // Per-vertex colour is the vertex colour modulated by the task tint (0..1 per channel).
        var f0 = task.Tint.Red / 255.0f;
        var f1 = task.Tint.Green / 255.0f;
        var f2 = task.Tint.Blue / 255.0f;
        var f3 = task.Tint.Alpha / 255.0f;

        foreach (var v in task.Vb)
        {
            var p = new Pixel(v.C);
            GL.Color4((byte)(p.Red * f0), (byte)(p.Green * f1), (byte)(p.Blue * f2), (byte)(p.Alpha * f3));
            GL.TexCoord2(v.U, v.V);
            GL.Vertex4(v.X, v.Y, task.Depth ? v.Z : 0.0f, v.W);
        }

        GL.End();

        if (task.Depth)
            GL.Disable(EnableCap.DepthTest);

        GL.MatrixMode(MatrixMode.Projection);
        GL.PopMatrix();
        GL.MatrixMode(MatrixMode.Modelview);
        GL.PopMatrix();
    }

    /// <summary>Sets the projection matrix used by depth-enabled GPU tasks.</summary>
    /// <param name="mat">A column-major 4x4 projection matrix (16 floats).</param>
    public override void Set3DProjection(float[] mat)
    {
        _matProjection = mat;
    }

    /// <summary>Allocates a GL texture, setting its min/mag filter and wrap modes; returns the texture id.</summary>
    /// <param name="size">The texture dimensions (currently used by callers; sampling state is set here).</param>
    /// <param name="filtered">When <c>true</c> the texture uses linear filtering; otherwise nearest-neighbour.</param>
    /// <param name="clamp">When <c>true</c> the texture clamps to its edge; otherwise it repeats.</param>
    /// <returns>The newly generated GL texture id.</returns>
    public override int CreateTexture(Vector2d<int> size, bool filtered = false, bool clamp = true)
    {
        var id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);

        if (filtered)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        }
        else
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        }

        if (clamp)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
        }
        else
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Modulate);
        return id;
    }

    /// <summary>Uploads the sprite's pixels into the bound texture, zero-copy from the blittable List{Pixel}.</summary>
    /// <param name="id">The texture id (already bound via <see cref="ApplyTexture(uint)"/>; ignored here, as in olc).</param>
    /// <param name="sprite">The sprite whose pixels are uploaded; no-op if its width or height is non-positive.</param>
    /// <remarks>
    /// <para>
    /// <see cref="Pixel"/> is a 4-byte blittable struct, so the upload reads straight from the
    /// <c>List{Pixel}</c> backing array with no intermediate copy.
    /// </para>
    /// </remarks>
    /// <seealso cref="ReadTexture(uint, Sprite)"/>
    public override void UpdateTexture(uint id, Sprite sprite)
    {
        // id is already bound via ApplyTexture; olc ignores it here too. Pixel is a 4-byte
        // blittable struct, so we upload straight from the List<Pixel> backing array — no copy.
        if (sprite.Width <= 0 || sprite.Height <= 0) return;
        var span = CollectionsMarshal.AsSpan(sprite.PixelData);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, sprite.Width, sprite.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, ref MemoryMarshal.GetReference(span));
    }

    /// <summary>Reads the bound texture's pixels back into the sprite's backing array.</summary>
    /// <param name="id">The texture id (already bound via <see cref="ApplyTexture(uint)"/>; ignored here).</param>
    /// <param name="sprite">The sprite whose backing array receives the pixels; no-op if its width or height is non-positive.</param>
    /// <seealso cref="UpdateTexture(uint, Sprite)"/>
    public override void ReadTexture(uint id, Sprite sprite)
    {
        if (sprite.Width <= 0 || sprite.Height <= 0) return;
        var span = CollectionsMarshal.AsSpan(sprite.PixelData);
        GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(span));
    }

    /// <summary>Deletes the GL texture; returns the id passed in.</summary>
    /// <param name="id">The GL texture id to delete.</param>
    /// <returns>The same <paramref name="id"/> that was passed in.</returns>
    public override uint DeleteTexture(uint id)
    {
        GL.DeleteTexture((int)id);
        return id;
    }

    /// <summary>Binds the texture as the active 2D texture.</summary>
    /// <param name="id">The GL texture id to bind.</param>
    public override void ApplyTexture(uint id)
    {
        GL.BindTexture(TextureTarget.Texture2D, (int)id);
    }

    /// <summary>Clears the colour buffer to the given pixel, and the depth buffer when requested.</summary>
    /// <param name="p">The colour to clear the colour buffer to.</param>
    /// <param name="depth">When <c>true</c> the depth buffer is also cleared.</param>
    public override void ClearBuffer(Pixel p, bool depth)
    {
        GL.ClearColor(p.Red / 255.0f, p.Green / 255.0f, p.Blue / 255.0f, p.Alpha / 255.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        if (depth)
            GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    /// <summary>Sets the GL viewport rectangle (letterbox position and size).</summary>
    /// <param name="pos">Bottom-left position of the viewport in window pixels.</param>
    /// <param name="size">Width and height of the viewport in window pixels.</param>
    public override void UpdateViewport(Vector2d<int> pos, Vector2d<int> size)
    {
        GL.Viewport(pos.X, pos.Y, size.X, size.Y);
    }
}
