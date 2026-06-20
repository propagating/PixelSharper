using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Renderers;

/// <summary>
/// Faithful port of olc::Renderer_OGL33 — the modern, shader-based OpenGL 3.3 backend
/// (programmable pipeline: one VAO/VBO streamed through a single quad shader). Sits alongside
/// <see cref="RendererOgl10"/> behind the abstract <see cref="Renderer"/> seam.
/// </summary>
/// <remarks>
/// <para>
/// Where olc manually <c>GetProcAddress</c>-loads ~30 GL3.3 entry points, this port calls OpenTK's
/// <c>GL.*</c> bindings directly. The platform's compatibility context (requested for the OGL10
/// immediate-mode path) also exposes GL3.3 on real hardware, so this renderer needs no separate
/// context — the same arrangement the <c>Shaders</c> extension relies on.
/// </para>
/// <para>
/// The vertex format is <see cref="Vertex"/>, whose sequential layout (four position floats, two
/// texture floats, one packed RGBA <see cref="uint"/>) is byte-for-byte olc's internal
/// <c>locVertex</c>, so vertex buffers upload with no repacking. Texture uploads/read-backs are the
/// same zero-copy path as <see cref="RendererOgl10"/> (straight from the blittable
/// <c>List{Pixel}</c> backing array).
/// </para>
/// </remarks>
/// <seealso cref="Renderer"/>
/// <seealso cref="RendererOgl10"/>
public class RendererOgl33 : Renderer
{
    /// <summary>Upper bound on vertices streamed per decal draw (olc's <c>OLC_MAX_VERTS</c>). GPU tasks upload their own buffer and are not bounded by this.</summary>
    private const int MaxVerts = 128;

    /// <summary>Byte stride of one <see cref="Vertex"/> (== olc's <c>sizeof(locVertex)</c> == 28): 4 pos floats + 2 tex floats + 4-byte packed colour.</summary>
    private static readonly int VertexStride = Marshal.SizeOf<Vertex>();

    /// <summary>The OpenGL context owned by the platform window; supplied via <see cref="CreateDevice"/>.</summary>
    private IGraphicsContext? _context;

    /// <summary>
    /// Current decal mode, so <see cref="SetDecalMode"/> only touches GL state on transitions
    /// (null mirrors olc seeding <c>nDecalMode</c> with <c>DecalMode(-1)</c>).
    /// </summary>
    private DecalMode? _decalMode;

    /// <summary>The 3D projection matrix applied to depth-enabled GPU tasks (column-major 4x4).</summary>
    private float[] _matProjection =
    {
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    };

    // GL object handles.
    private int _fragmentShader;
    private int _vertexShader;
    private int _quadShader;
    private int _vbQuad;
    private int _vaQuad;

    // Uniform locations within the quad shader.
    private int _uniMvp;
    private int _uniIs3D;
    private int _uniTint;

    /// <summary>Scratch vertex buffer for decal draws (olc's <c>pVertexMem</c>); reused each call.</summary>
    private readonly Vertex[] _vertexMem = new Vertex[MaxVerts];

    /// <summary>A 1x1 white texture bound for decals/tasks that carry no decal of their own (olc's <c>rendBlankQuad</c>).</summary>
    private Renderable _blankQuad = null!;

    /// <summary>Fragment shader: sample the sprite texture and modulate by the interpolated vertex colour.</summary>
    private const string FragmentSource =
        "#version 330 core\n" +
        "out vec4 pixel;\n" +
        "in vec2 oTex;\n" +
        "in vec4 oCol;\n" +
        "uniform sampler2D sprTex;\n" +
        "void main(){pixel = texture(sprTex, oTex) * oCol;}";

    /// <summary>
    /// Vertex shader: a 3D path (multiply by MVP) and a 2D path that divides by the homogeneous
    /// component for projective ("warped") decals; colour is the vertex colour times the tint uniform.
    /// </summary>
    private const string VertexSource =
        "#version 330 core\n" +
        "layout(location = 0) in vec4 aPos;\n" +
        "layout(location = 1) in vec2 aTex;\n" +
        "layout(location = 2) in vec4 aCol;\n" +
        "uniform mat4 mvp;\n" +
        "uniform int is3d;\n" +
        "uniform vec4 tint;\n" +
        "out vec2 oTex;\n" +
        "out vec4 oCol;\n" +
        "void main(){ if(is3d!=0) {gl_Position = mvp * vec4(aPos.x, aPos.y, aPos.z, 1.0); oTex = aTex;} " +
        "else {float p = 1.0 / aPos.z; gl_Position = p * vec4(aPos.x, aPos.y, 0.0, 1.0); oTex = p * aTex;} " +
        "oCol = aCol * tint;}";

    /// <summary>No-op here; OpenTK creates the context lazily in <see cref="CreateDevice"/> (olc only worked for GLUT).</summary>
    public override void PrepareDevice()
    {
        // olc only does work here for GLUT; WGL/GLX (and OpenTK) create the context in CreateDevice.
    }

    /// <summary>Takes the platform's OpenTK context (parameters[0]), makes it current, then compiles the quad shader and builds the VAO/VBO and blank texture.</summary>
    /// <param name="parameters">Platform-supplied arguments; <c>parameters[0]</c> must be the OpenTK <c>IGraphicsContext</c>.</param>
    /// <param name="fullScreen">Whether the device is created for a full-screen window (unused in this port).</param>
    /// <param name="vsync">When <c>true</c> the swap interval is set to 1 (vsync on); otherwise 0.</param>
    /// <returns><see cref="FileReadCode.Ok"/> on success, or <see cref="FileReadCode.Fail"/> if no valid context was supplied.</returns>
    public override FileReadCode CreateDevice(List<object> parameters, bool fullScreen, bool vsync)
    {
        if (parameters == null || parameters.Count == 0 || parameters[0] is not IGraphicsContext context)
            return FileReadCode.Fail;

        _context = context;
        _context.MakeCurrent();
        _context.SwapInterval = vsync ? 1 : 0;

        GL.Enable(EnableCap.Texture2D);
        GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

        // Compile + link the quad shader (assumed valid; a non-empty info log is surfaced as a throw).
        _fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentSource);
        _vertexShader = CompileShader(ShaderType.VertexShader, VertexSource);

        _quadShader = GL.CreateProgram();
        GL.AttachShader(_quadShader, _fragmentShader);
        GL.AttachShader(_quadShader, _vertexShader);
        GL.LinkProgram(_quadShader);
        var linkLog = GL.GetProgramInfoLog(_quadShader);
        if (!string.IsNullOrWhiteSpace(linkLog))
            throw new InvalidOperationException($"OGL33 quad shader link failed: {linkLog}");

        _uniMvp = GL.GetUniformLocation(_quadShader, "mvp");
        _uniIs3D = GL.GetUniformLocation(_quadShader, "is3d");
        _uniTint = GL.GetUniformLocation(_quadShader, "tint");

        GL.UseProgram(_quadShader);
        GL.Uniform1(_uniIs3D, 0);
        GL.UniformMatrix4(_uniMvp, 1, false, _matProjection);
        GL.Uniform4(_uniTint, 1.0f, 1.0f, 1.0f, 1.0f);

        // Create the quad VAO/VBO and describe the interleaved Vertex layout once.
        _vbQuad = GL.GenBuffer();
        _vaQuad = GL.GenVertexArray();
        GL.BindVertexArray(_vaQuad);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbQuad);
        GL.BufferData(BufferTarget.ArrayBuffer, VertexStride * MaxVerts, _vertexMem, BufferUsageHint.StreamDraw);

        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, VertexStride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, VertexStride, 4 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, VertexStride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);

        // Blank 1x1 white texture for spriteless decals/tasks.
        _blankQuad = new Renderable();
        _blankQuad.Create(1, 1, false, true);
        _blankQuad.Sprite.PixelData[0] = Pixel.WHITE;
        _blankQuad.Decal.Update();

        return FileReadCode.Ok;
    }

    /// <summary>Compiles a shader stage from GLSL source, surfacing any compiler info log as an exception.</summary>
    /// <param name="type">The shader stage to compile.</param>
    /// <param name="source">The GLSL source string.</param>
    /// <returns>The compiled GL shader id.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the driver reports a non-empty compile log.</exception>
    private static int CompileShader(ShaderType type, string source)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, source);
        GL.CompileShader(id);
        var log = GL.GetShaderInfoLog(id);
        if (!string.IsNullOrWhiteSpace(log))
            throw new InvalidOperationException($"OGL33 {type} compile failed: {log}");
        return id;
    }

    /// <summary>Tears down the GL program/buffers (while the context is still current) and drops the context reference.</summary>
    /// <returns>Always <see cref="FileReadCode.Ok"/>.</returns>
    public override FileReadCode DestroyDevice()
    {
        if (_context != null)
        {
            if (_quadShader != 0) GL.DeleteProgram(_quadShader);
            if (_vertexShader != 0) GL.DeleteShader(_vertexShader);
            if (_fragmentShader != 0) GL.DeleteShader(_fragmentShader);
            if (_vbQuad != 0) GL.DeleteBuffer(_vbQuad);
            if (_vaQuad != 0) GL.DeleteVertexArray(_vaQuad);
        }

        // The context's lifetime is owned by the platform window, not the renderer.
        _context = null;
        return FileReadCode.Ok;
    }

    /// <summary>Presents the rendered frame by swapping the back buffer.</summary>
    public override void DisplayFrame()
    {
        _context?.SwapBuffers();
    }

    /// <summary>Sets up per-frame GL state: blending on, the quad shader/VAO bound, white tint, 2D mode, culling off.</summary>
    public override void PrepareDrawing()
    {
        GL.Enable(EnableCap.Blend);
        _decalMode = DecalMode.Normal;
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.UseProgram(_quadShader);
        GL.BindVertexArray(_vaQuad);
        GL.Uniform4(_uniTint, 1.0f, 1.0f, 1.0f, 1.0f);
        GL.Uniform1(_uniIs3D, 0);
        GL.UniformMatrix4(_uniMvp, 1, false, _matProjection);
        GL.Disable(EnableCap.CullFace);
        GL.DepthFunc(DepthFunction.Less);
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
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbQuad);

        // pos = (x, y, z=1, w=0); the 2D shader path divides by z (== 1) so these map straight to NDC.
        _vertexMem[0] = new Vertex(-1.0f, -1.0f, 1.0f, 0.0f, 0.0f * scale.X + offset.X, 1.0f * scale.Y + offset.Y, tint.N);
        _vertexMem[1] = new Vertex(+1.0f, -1.0f, 1.0f, 0.0f, 1.0f * scale.X + offset.X, 1.0f * scale.Y + offset.Y, tint.N);
        _vertexMem[2] = new Vertex(-1.0f, +1.0f, 1.0f, 0.0f, 0.0f * scale.X + offset.X, 0.0f * scale.Y + offset.Y, tint.N);
        _vertexMem[3] = new Vertex(+1.0f, +1.0f, 1.0f, 0.0f, 1.0f * scale.X + offset.X, 0.0f * scale.Y + offset.Y, tint.N);

        GL.BufferData(BufferTarget.ArrayBuffer, VertexStride * 4, _vertexMem, BufferUsageHint.StreamDraw);

        GL.Uniform1(_uniIs3D, 0);
        GL.Uniform4(_uniTint, 1.0f, 1.0f, 1.0f, 1.0f);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    /// <summary>Renders a queued decal instance as a 2D (or projectively-warped) textured primitive.</summary>
    /// <param name="decal">The queued decal instance carrying texture, blend mode, structure, and per-vertex data.</param>
    /// <seealso cref="DoGPUTask(GPUTask)"/>
    public override void DrawDecal(DecalInstance decal)
    {
        GL.Disable(EnableCap.CullFace);
        SetDecalMode(decal.Mode);
        GL.BindTexture(TextureTarget.Texture2D, decal.Decal?.Id ?? _blankQuad.Decal.Id);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbQuad);

        var points = (int)decal.Points;
        for (var i = 0; i < points; i++)
        {
            // pos = (x, y, z=w[i], w=0): the 2D shader path divides by z, giving the projective warp.
            _vertexMem[i] = new Vertex(decal.Pos[i].X, decal.Pos[i].Y, decal.W[i], 0.0f,
                decal.Uv[i].X, decal.Uv[i].Y, decal.Tint[i].N);
        }

        GL.BufferData(BufferTarget.ArrayBuffer, VertexStride * points, _vertexMem, BufferUsageHint.StreamDraw);
        GL.Uniform1(_uniIs3D, 0);
        GL.Uniform4(_uniTint, 1.0f, 1.0f, 1.0f, 1.0f);

        if (_decalMode == DecalMode.Wireframe)
            GL.DrawArrays(PrimitiveType.LineLoop, 0, points);
        else
            GL.DrawArrays(PrimitiveFor(decal.Structure), 0, points);
    }

    /// <summary>Renders a GPU task: uploads its vertex buffer, composes projection×MVP, applies cull/depth/tint, then draws.</summary>
    /// <param name="task">The GPU task carrying texture, cull/depth flags, MVP matrix, tint, and vertex buffer.</param>
    /// <seealso cref="DrawDecal(DecalInstance)"/>
    public override void DoGPUTask(GPUTask task)
    {
        SetDecalMode(task.Mode);
        GL.BindTexture(TextureTarget.Texture2D, task.Decal?.Id ?? _blankQuad.Decal.Id);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbQuad);

        // Upload the task's vertices straight from the List<Vertex> backing array (no copy).
        var count = task.Vb.Count;
        var span = CollectionsMarshal.AsSpan(task.Vb);
        GL.BufferData(BufferTarget.ArrayBuffer, VertexStride * count,
            ref MemoryMarshal.GetReference(span), BufferUsageHint.StreamDraw);

        GL.Uniform1(_uniIs3D, 1);

        var mvp = MultiplyProjection(_matProjection, task.Mvp);
        GL.UniformMatrix4(_uniMvp, 1, false, mvp);

        GL.Uniform4(_uniTint, task.Tint.Red / 255.0f, task.Tint.Green / 255.0f,
            task.Tint.Blue / 255.0f, task.Tint.Alpha / 255.0f);

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
            GL.Enable(EnableCap.DepthTest);

        if (_decalMode == DecalMode.Wireframe)
            GL.DrawArrays(PrimitiveType.LineLoop, 0, count);
        else
            GL.DrawArrays(PrimitiveFor(task.Structure), 0, count);

        if (task.Depth)
            GL.Disable(EnableCap.DepthTest);
    }

    /// <summary>Maps a decal structure to its GL primitive type (fan/strip/list/line).</summary>
    /// <param name="structure">The vertex-assembly structure.</param>
    /// <returns>The matching <see cref="PrimitiveType"/> (triangle fan when unspecified).</returns>
    private static PrimitiveType PrimitiveFor(DecalStructure structure) => structure switch
    {
        DecalStructure.Fan => PrimitiveType.TriangleFan,
        DecalStructure.Strip => PrimitiveType.TriangleStrip,
        DecalStructure.List => PrimitiveType.Triangles,
        DecalStructure.Line => PrimitiveType.Lines,
        _ => PrimitiveType.TriangleFan
    };

    /// <summary>Composes the column-major product <c>projection × mvp</c> for a GPU task's final transform.</summary>
    /// <param name="projection">The 16-element column-major projection matrix.</param>
    /// <param name="mvp">The 16-element column-major model-view matrix.</param>
    /// <returns>A new 16-element column-major matrix equal to <paramref name="projection"/> × <paramref name="mvp"/>.</returns>
    public static float[] MultiplyProjection(float[] projection, float[] mvp)
    {
        var result = new float[16];
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                result[c * 4 + r] =
                    projection[0 * 4 + r] * mvp[c * 4 + 0]
                    + projection[1 * 4 + r] * mvp[c * 4 + 1]
                    + projection[2 * 4 + r] * mvp[c * 4 + 2]
                    + projection[3 * 4 + r] * mvp[c * 4 + 3];
        return result;
    }

    /// <summary>Sets the projection matrix used by depth-enabled GPU tasks.</summary>
    /// <param name="mat">A column-major 4x4 projection matrix (16 floats).</param>
    public override void Set3DProjection(float[] mat)
    {
        _matProjection = mat;
    }

    /// <summary>Allocates a GL texture, setting its min/mag filter and wrap modes; returns the texture id.</summary>
    /// <param name="size">The texture dimensions (sampling state is set here; the size is supplied at upload time).</param>
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
    /// <seealso cref="ReadTexture(uint, Sprite)"/>
    public override void UpdateTexture(uint id, Sprite sprite)
    {
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
