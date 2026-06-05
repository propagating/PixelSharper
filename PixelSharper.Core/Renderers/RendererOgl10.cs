using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Renderers;

// Faithful port of olc::Renderer_OGL10 — fixed-function, immediate-mode OpenGL 1.0
// (olcPixelGameEngine.h ~line 5302). Context creation/swapping is delegated to the
// OpenTK windowing layer (the Platform sibling) rather than raw WGL/GLX as in olc.
public class RendererOgl10 : Renderer
{
    // The OpenGL context owned by the platform window; supplied via CreateDevice.
    private IGraphicsContext? _context;

    // Track current decal mode so SetDecalMode only changes GL state on transitions
    // (olc seeds nDecalMode with (DecalMode)(-1); null serves the same role). olc's
    // nDecalStructure is dead state in OGL10 — the primitive comes straight from the decal/task.
    private DecalMode? _decalMode;

    private float[] _matProjection =
    {
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    };

    public override void PrepareDevice()
    {
        // olc only does work here for the GLUT platform; WGL/GLX (and OpenTK) create the
        // context lazily in CreateDevice, so nothing is required.
    }

    public override FileReadCode CreateDevice(List<object> parameters, bool fullScreen, bool vsync)
    {
        // The platform hands us its OpenTK graphics context as parameters[0].
        if (parameters == null || parameters.Count == 0 || parameters[0] is not IGraphicsContext context)
            return FileReadCode.FAIL;

        _context = context;
        _context.MakeCurrent();
        _context.SwapInterval = vsync ? 1 : 0;

        GL.Enable(EnableCap.Texture2D);
        GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
        return FileReadCode.OK;
    }

    public override FileReadCode DestroyDevice()
    {
        // The context's lifetime is owned by the platform window, not the renderer.
        _context = null;
        return FileReadCode.OK;
    }

    public override void DisplayFrame()
    {
        _context?.SwapBuffers();
    }

    public override void PrepareDrawing()
    {
        GL.Enable(EnableCap.Blend);
        _decalMode = DecalMode.Normal;
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
    }

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
            case CullMode.NONE:
                GL.CullFace(TriangleFace.Front);
                GL.Disable(EnableCap.CullFace);
                break;
            case CullMode.CW:
                GL.CullFace(TriangleFace.Front);
                GL.Enable(EnableCap.CullFace);
                break;
            case CullMode.CCW:
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

    public override void Set3DProjection(float[] mat)
    {
        _matProjection = mat;
    }

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

    public override void UpdateTexture(uint id, Sprite sprite)
    {
        // id is already bound via ApplyTexture; olc ignores it here too. Pixel is a 4-byte
        // blittable struct, so we upload straight from the List<Pixel> backing array — no copy.
        if (sprite.Width <= 0 || sprite.Height <= 0) return;
        var span = CollectionsMarshal.AsSpan(sprite.PixelData);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, sprite.Width, sprite.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, ref MemoryMarshal.GetReference(span));
    }

    public override void ReadTexture(uint id, Sprite sprite)
    {
        if (sprite.Width <= 0 || sprite.Height <= 0) return;
        var span = CollectionsMarshal.AsSpan(sprite.PixelData);
        GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(span));
    }

    public override uint DeleteTexture(uint id)
    {
        GL.DeleteTexture((int)id);
        return id;
    }

    public override void ApplyTexture(uint id)
    {
        GL.BindTexture(TextureTarget.Texture2D, (int)id);
    }

    public override void ClearBuffer(Pixel p, bool depth)
    {
        GL.ClearColor(p.Red / 255.0f, p.Green / 255.0f, p.Blue / 255.0f, p.Alpha / 255.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        if (depth)
            GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    public override void UpdateViewport(Vector2d<int> pos, Vector2d<int> size)
    {
        GL.Viewport(pos.X, pos.Y, size.X, size.Y);
    }
}
