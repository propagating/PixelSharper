using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using PixelSharper.Core.Actions;
using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Extensions.Fx;

// Port of olcPGEX_Shaders (olc::Shade) — a GLSL fragment-shader effects layer for decals. olc
// manually GetProcAddress-loads the GL3.3 functions; here we call OpenTK's GL bindings directly
// (the same context the OGL1.0 renderer uses exposes them on real hardware). Compiles user GLSL
// "effects", binds decal textures to samplers, and renders quads/polygons either to the screen or
// to a target decal (render-to-texture via a framebuffer). Runs only with a live GL context.

/// <summary>GLSL source + input/output slot counts + tunable uniform attributes describing one effect.</summary>
public class EffectConfig
{
    /// <summary>Vertex shader body source.</summary>
    public string VertexSource = "";
    /// <summary>Fragment (pixel) shader body source.</summary>
    public string PixelSource = "";
    /// <summary>Number of input texture samplers.</summary>
    public int Inputs = 1;
    /// <summary>Number of output render targets.</summary>
    public int Outputs = 1;
    /// <summary>Tunable uniform attributes (name, GLSL type, default value).</summary>
    public List<(string Name, string Type, string Default)> Attributes = new();
}

/// <summary>A built-in library of effects (matching olc::fx::FX_*).</summary>
/// <remarks>
/// <para>Each property returns a fresh <see cref="EffectConfig"/>; compile one with
/// <see cref="Shade.MakeEffect"/>. The built-in effects are:</para>
/// <list type="bullet">
/// <item><description><see cref="Normal"/> — pass-through (sampled texture × vertex colour).</description></item>
/// <item><description><see cref="Greyscale"/> — luminance-weighted greyscale.</description></item>
/// <item><description><see cref="BoxBlur"/> — box blur with a tunable <c>box_width</c>.</description></item>
/// <item><description><see cref="Threshold"/> — luminance threshold to black/white.</description></item>
/// <item><description><see cref="Scanline"/> — CRT-style scanline overlay.</description></item>
/// </list>
/// <para>Requires a live GL3.3 context at draw time.</para>
/// </remarks>
public static class Fx
{
    /// <summary>GLSL version preamble prepended to every shader.</summary>
    public const string ShaderHeader = "#version 330 core       \n";

    /// <summary>Default pass-through vertex shader body.</summary>
    public const string DefaultVs =
        "void main()                                             \n" +
        "{                                                       \n" +
        "    float p = 1.0 / inPos.z;                            \n" +
        "    gl_Position = p * vec4(inPos.x, inPos.y, 0.0, 1.0); \n" +
        "    xUV1 = p * inUV1;                                   \n" +
        "    xCol = inCol;                                       \n" +
        "}                                                       \n";

    /// <summary>Default pixel shader body (sampled texture × vertex colour).</summary>
    public const string DefaultPs =
        "void main()                              \n" +
        "{                                        \n" +
        "    pix_out = texture(tex1, xUV1) * xCol;\n" +
        "}                                        \n";

    /// <summary>Pass-through effect (no colour change).</summary>
    /// <value>A new <see cref="EffectConfig"/> sampling the texture times the vertex colour.</value>
    public static EffectConfig Normal => new() { VertexSource = DefaultVs, PixelSource = DefaultPs };

    /// <summary>Luminance-weighted greyscale effect.</summary>
    /// <value>A new <see cref="EffectConfig"/> that converts each texel to luminance-weighted grey.</value>
    public static EffectConfig Greyscale => new()
    {
        VertexSource = DefaultVs,
        PixelSource =
            "void main()                                                \n" +
            "{                                                          \n" +
            "    vec4 o = texture(tex1, xUV1) * xCol;                   \n" +
            "    float lum = o.r * 0.2126 + o.g * 0.7152 + o.b * 0.0722;\n" +
            "    pix_out = vec4(lum, lum, lum, o.a);                    \n" +
            "}                                                          \n"
    };

    /// <summary>Box-blur effect with a tunable box_width radius.</summary>
    /// <value>A new <see cref="EffectConfig"/> with a tunable <c>box_width</c> uniform (default <c>4</c>).</value>
    public static EffectConfig BoxBlur => new()
    {
        VertexSource = DefaultVs,
        PixelSource =
            "void main()                                                              \n" +
            "{                                                                        \n" +
            "    vec2 texelSize = 1.0 / vec2(textureSize(tex1, 0));                    \n" +
            "    vec4 result;                                                          \n" +
            "    for (int x = -box_width; x < +box_width; ++x)                         \n" +
            "        for (int y = -box_width; y < +box_width; ++y)                     \n" +
            "            result += texture(tex1, xUV1 + vec2(float(x), float(y)) * texelSize);\n" +
            "    pix_out = (result / (4.0 * float(box_width * box_width))) * xCol;     \n" +
            "}                                                                        \n",
        Attributes = { ("box_width", "int", "4") }
    };

    /// <summary>Luminance threshold (black/white) effect with a tunable threshold.</summary>
    /// <value>A new <see cref="EffectConfig"/> with a tunable <c>threshold</c> uniform (default <c>0.5</c>).</value>
    public static EffectConfig Threshold => new()
    {
        VertexSource = DefaultVs,
        PixelSource =
            "void main()                                                       \n" +
            "{                                                                 \n" +
            "    vec4 o = texture(tex1, xUV1) * xCol;                          \n" +
            "    float lum = o.r * 0.2126 + o.g * 0.7152 + o.b * 0.0722;       \n" +
            "    pix_out = (lum > threshold) ? vec4(1,1,1,1) : vec4(0,0,0,1);  \n" +
            "}                                                                 \n",
        Attributes = { ("threshold", "float", "0.5") }
    };

    /// <summary>CRT-style scanline overlay effect (tunable frequency/intensity/phase).</summary>
    /// <value>A new <see cref="EffectConfig"/> with tunable <c>frequency</c>, <c>intensity</c>, and <c>phase</c> uniforms.</value>
    public static EffectConfig Scanline => new()
    {
        VertexSource = DefaultVs,
        PixelSource =
            "void main()                                                                       \n" +
            "{                                                                                 \n" +
            "    float scanline = sin(xUV1.y * frequency + phase) * intensity;                 \n" +
            "    pix_out = (texture(tex1, xUV1) + vec4(scanline, scanline, scanline, 0.0)) * xCol;\n" +
            "}                                                                                 \n",
        Attributes = { ("frequency", "float", "400.0"), ("intensity", "float", "0.4"), ("phase", "float", "0.0") }
    };
}

/// <summary>A compiled GLSL effect program plus its resource ids and slot counts.</summary>
public class Effect
{
    /// <summary>Accumulated compile/link log; empty means success.</summary>
    private string _status = "";
    /// <summary>GL pixel/vertex shader and program object ids.</summary>
    internal int Psid, Vsid, Id;
    /// <summary>Number of input sampler slots and output target slots.</summary>
    internal int InputSlots, TargetSlots;

    /// <summary>True when the effect compiled and linked without errors.</summary>
    /// <returns><c>true</c> if the status log is empty (compile and link succeeded); otherwise <c>false</c>.</returns>
    public bool IsOK() => _status.Length == 0;
    /// <summary>Returns the compile/link status log.</summary>
    /// <returns>The accumulated compile/link log; empty on success.</returns>
    public string GetStatus() => _status;
    /// <summary>Number of output render-target slots.</summary>
    /// <returns>The count of output render-target slots.</returns>
    public int GetTargetSlots() => TargetSlots;
    /// <summary>Number of input sampler slots.</summary>
    /// <returns>The count of input sampler slots.</returns>
    public int GetInputSlots() => InputSlots;

    /// <summary>Appends a message to the status log.</summary>
    /// <param name="msg">The text to append to the compile/link log.</param>
    internal void AppendStatus(string msg) => _status += msg;
    /// <summary>Records the GL program/vertex/pixel shader ids.</summary>
    /// <param name="id">GL program object id.</param>
    /// <param name="vsid">GL vertex-shader object id.</param>
    /// <param name="psid">GL fragment (pixel) shader object id.</param>
    internal void SetResourceIDs(int id, int vsid, int psid) { Id = id; Vsid = vsid; Psid = psid; }
    /// <summary>Records the input/output slot counts.</summary>
    /// <param name="input">Number of input sampler slots.</param>
    /// <param name="target">Number of output render-target slots.</param>
    internal void SetSlots(int input, int target) { InputSlots = input; TargetSlots = target; }
}

/// <summary>The effects-layer PGEX: compiles effects and renders decals/quads/polygons through a shader to the screen or a target decal.</summary>
/// <remarks>
/// <para>Port of olc::Shade. Requires a live GL3.3 context (provided by the OGL1.0 renderer's compatibility
/// context on real hardware); the GL resources are created in <see cref="OnBeforeUserCreate"/>.</para>
/// <para>Typical use: build an effect with <see cref="MakeEffect"/>, then per frame call <see cref="Start"/>,
/// the <c>Draw*</c> methods, and <see cref="End"/>.</para>
/// <para>See also <see cref="Fx"/> for the built-in effect library.</para>
/// </remarks>
public class Shade : PGEX
{
    /// <summary>Maximum vertices buffered per draw call.</summary>
    private const int MaxVerts = 128;

    /// <summary>The 80-byte interleaved vertex format (position + packed colour + 8 UV sets).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct OmniVertex
    {
        /// <summary>Position (x, y, perspective z).</summary>
        public float Px, Py, Pz;
        /// <summary>Packed RGBA colour.</summary>
        public uint Col;
        /// <summary>Eight UV coordinate pairs.</summary>
        public float U0, V0, U1, V1, U2, V2, U3, V3, U4, V4, U5, V5, U6, V6, U7, V7;
    }

    /// <summary>One bound texture/render-target slot.</summary>
    private struct ResourceSlot
    {
        /// <summary>Whether the slot is bound to a user decal.</summary>
        public bool InUse;
        /// <summary>GL texture id of the bound decal.</summary>
        public int TargetResId;
        /// <summary>Source sub-region origin/size and inverse target size.</summary>
        public Vector2d<float> Pos, Size, InvSize;
    }

    /// <summary>The eight output render-target slots.</summary>
    private readonly ResourceSlot[] _slotTarget = new ResourceSlot[8];
    /// <summary>The eight input source slots.</summary>
    private readonly ResourceSlot[] _slotSource = new ResourceSlot[8];
    /// <summary>CPU-side vertex staging buffer uploaded each draw.</summary>
    private readonly OmniVertex[] _vertexMem = new OmniVertex[MaxVerts];

    /// <summary>GL vertex buffer, vertex array, and framebuffer object ids.</summary>
    private int _vb, _va, _fbo;
    /// <summary>1x1 white texture used as a safe default source.</summary>
    private readonly Renderable _dummyTex = new();

    /// <summary>Constructs the extension and auto-registers its lifecycle hooks.</summary>
    public Shade() : base(true) { }

    /// <summary>Assembles full vertex/pixel shader source from a config and compiles it into an effect.</summary>
    /// <param name="premade">The effect description (GLSL bodies, slot counts, tunable attributes).</param>
    /// <returns>The compiled <see cref="Effect"/>; check <see cref="Effect.IsOK"/> / <see cref="Effect.GetStatus"/> for errors.</returns>
    public Effect MakeEffect(EffectConfig premade)
    {
        // Build the vertex shader (inputs + varyings + the user body).
        var vs = Fx.ShaderHeader + "layout(location = 0) in vec3 inPos; \nlayout(location = 1) in vec4 inCol; \n";
        for (var i = 0; i < premade.Inputs; i++) vs += $"layout(location = {2 + i}) in vec2 inUV{1 + i}; \n";
        for (var i = 0; i < premade.Inputs; i++) vs += $"out vec2 xUV{1 + i}; \n";
        vs += "out vec4 xCol; \n" + premade.VertexSource;

        // Build the pixel shader (varyings + samplers + uniforms + the user body).
        var ps = Fx.ShaderHeader + "in vec4 xCol; \n";
        for (var i = 0; i < premade.Inputs; i++) ps += $"in vec2 xUV{1 + i}; \n";
        for (var i = 0; i < premade.Inputs; i++) ps += $"uniform sampler2D tex{1 + i};\n";
        foreach (var (name, type, def) in premade.Attributes) ps += $"uniform {type} {name} = {def};\n";
        ps += "out vec4 pix_out;\n" + premade.PixelSource;

        var effect = ConstructShader(vs, ps);
        effect.SetSlots(premade.Inputs, premade.Outputs);
        return effect;
    }

    /// <summary>Compiles and links the vertex/pixel sources into a GL program, capturing any log to the effect.</summary>
    /// <param name="vs">Full vertex-shader GLSL source.</param>
    /// <param name="ps">Full fragment (pixel) shader GLSL source.</param>
    /// <returns>An <see cref="Effect"/> holding the GL ids and any compile/link log.</returns>
    private Effect ConstructShader(string vs, string ps)
    {
        var effect = new Effect();

        var vsId = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vsId, vs);
        GL.CompileShader(vsId);
        var log = GL.GetShaderInfoLog(vsId);
        if (!string.IsNullOrEmpty(log)) effect.AppendStatus(log);

        var psId = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(psId, ps);
        GL.CompileShader(psId);
        log = GL.GetShaderInfoLog(psId);
        if (!string.IsNullOrEmpty(log)) effect.AppendStatus(log);

        var shId = GL.CreateProgram();
        GL.AttachShader(shId, psId);
        GL.AttachShader(shId, vsId);
        GL.LinkProgram(shId);
        log = GL.GetProgramInfoLog(shId);
        if (!string.IsNullOrEmpty(log)) effect.AppendStatus(log);

        effect.SetResourceIDs(shId, vsId, psId);
        return effect;
    }

    /// <summary>One-time GL setup: creates the FBO/VBO/VAO, sets vertex attribute pointers, and builds the dummy texture.</summary>
    /// <remarks>
    /// <para>Invoked by the engine before the user's <c>OnCreate</c>; requires a live GL3.3 context.</para>
    /// </remarks>
    protected internal override void OnBeforeUserCreate()
    {
        _fbo = GL.GenFramebuffer();
        _vb = GL.GenBuffer();
        _va = GL.GenVertexArray();
        GL.BindVertexArray(_va);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vb);
        GL.BufferData(BufferTarget.ArrayBuffer, MaxVerts * Marshal.SizeOf<OmniVertex>(), _vertexMem, BufferUsageHint.StreamDraw);

        var stride = Marshal.SizeOf<OmniVertex>();
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, stride, 12);
        for (var i = 0; i < 8; i++)
            GL.VertexAttribPointer(2 + i, 2, VertexAttribPointerType.Float, false, stride, 16 + i * 8);
        for (var i = 0; i < 10; i++) GL.EnableVertexAttribArray(i);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);

        // A 1x1 white texture used as a safe default source.
        _dummyTex.Create(1, 1, false, true);
        _dummyTex.Sprite.PixelData[0] = Pixel.WHITE;
        _dummyTex.Decal.Update();
    }

    /// <summary>Binds a decal's texture (or the dummy texture if null) to an input sampler slot.</summary>
    /// <param name="decal">The decal whose texture to bind; <c>null</c> binds the 1x1 white dummy texture.</param>
    /// <param name="slot">Input sampler slot index (0..7).</param>
    /// <param name="sourcePos">Source sub-region origin in normalised texture coordinates.</param>
    /// <param name="sourceSize">Source sub-region size in normalised texture coordinates; <c>(0,0)</c> defaults to the full <c>(1,1)</c>.</param>
    public void SetSourceDecal(Decal decal, int slot = 0, Vector2d<float> sourcePos = default, Vector2d<float> sourceSize = default)
    {
        if (decal == null)
        {
            _slotSource[slot] = new ResourceSlot { InUse = false, TargetResId = _dummyTex.Decal.Id, Size = new Vector2d<float>(1, 1) };
        }
        else
        {
            _slotSource[slot].InUse = true;
            _slotSource[slot].TargetResId = decal.Id;
            _slotSource[slot].Pos = sourcePos;
            _slotSource[slot].Size = sourceSize.X == 0 && sourceSize.Y == 0 ? new Vector2d<float>(1, 1) : sourceSize;
        }
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _slotSource[slot].TargetResId);
    }

    /// <summary>Binds a decal as an output render target (render-to-texture) for a slot.</summary>
    /// <param name="decal">The decal whose texture receives the rendered output.</param>
    /// <param name="slot">Output render-target slot index (0..7).</param>
    public void SetTargetDecal(Decal decal, int slot = 0)
    {
        _slotTarget[slot].InUse = true;
        _slotTarget[slot].TargetResId = decal.Id;
        _slotTarget[slot].Size = new Vector2d<float>(decal.Sprite.Width, decal.Sprite.Height);
        _slotTarget[slot].InvSize = new Vector2d<float>(1f / decal.Sprite.Width, 1f / decal.Sprite.Height);
    }

    /// <summary>Begins rendering with an effect: binds the FBO/targets, activates the program and VAO, and sets the viewport.</summary>
    /// <param name="effect">The compiled effect to render through; its target-slot count drives the FBO attachments.</param>
    /// <remarks>
    /// <para>Pair every <see cref="Start"/> with an <see cref="End"/>. Bind targets with
    /// <see cref="SetTargetDecal"/> beforehand to render to a decal, or none to render to the screen.</para>
    /// </remarks>
    public void Start(Effect effect)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

        var attachments = new[]
        {
            FramebufferAttachment.ColorAttachment0, FramebufferAttachment.ColorAttachment1,
            FramebufferAttachment.ColorAttachment2, FramebufferAttachment.ColorAttachment3,
            FramebufferAttachment.ColorAttachment4, FramebufferAttachment.ColorAttachment5,
            FramebufferAttachment.ColorAttachment6, FramebufferAttachment.ColorAttachment7
        };
        foreach (var a in attachments) GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, a, TextureTarget.Texture2D, 0, 0);

        var targetCount = 0;
        while (targetCount < 8 && _slotTarget[targetCount].TargetResId > 0) targetCount++;
        var drawBuffers = new DrawBuffersEnum[targetCount];
        for (var i = 0; i < targetCount; i++) drawBuffers[i] = (DrawBuffersEnum)attachments[i];
        if (targetCount > 0) GL.DrawBuffers(targetCount, drawBuffers);

        for (var i = 0; i < effect.GetTargetSlots(); i++)
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachments[i], TextureTarget.Texture2D, _slotTarget[i].TargetResId, 0);

        GL.UseProgram(effect.Id);
        GL.BindVertexArray(_va);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Viewport(0, 0, (int)_slotTarget[0].Size.X, (int)_slotTarget[0].Size.Y);
    }

    /// <summary>Ends rendering: unbinds the buffer/VAO/program and restores the default framebuffer.</summary>
    /// <remarks>
    /// <para>Closes a render pass opened by <see cref="Start"/>.</para>
    /// </remarks>
    public void End()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Clears the current target's colour buffer to the given pixel (transparent by default).</summary>
    /// <param name="p">Clear colour; defaults to <see cref="Pixel.BLANK"/> (transparent) when <c>null</c>.</param>
    public void Clear(Pixel? p = null)
    {
        var c = p ?? Pixel.BLANK;
        GL.ClearColor(c.Red / 255f, c.Green / 255f, c.Blue / 255f, c.Alpha / 255f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>Draws a decal as a screen-space quad through the active effect, with optional scale and tint.</summary>
    /// <param name="pos">Top-left position of the quad in screen pixels.</param>
    /// <param name="decal">The decal to draw.</param>
    /// <param name="scale">Per-axis scale; <c>(0,0)</c> defaults to <c>(1,1)</c>.</param>
    /// <param name="tint">Tint multiplied into the vertex colour; defaults to <see cref="Pixel.WHITE"/> when <c>null</c>.</param>
    public void DrawDecal(Vector2d<float> pos, Decal decal, Vector2d<float> scale = default, Pixel? tint = null)
    {
        if (scale.X == 0 && scale.Y == 0) scale = new Vector2d<float>(1, 1);
        var t = tint ?? Pixel.WHITE;
        var inv = _slotTarget[0].InvSize;
        var spX = System.MathF.Floor(pos.X) * inv.X * 2f - 1f;
        var spY = System.MathF.Floor(pos.Y) * inv.Y * 2f - 1f;
        var sdX = spX + 2f * (decal.Sprite.Width * inv.X) * scale.X;
        var sdY = spY + 2f * (decal.Sprite.Height * inv.Y) * scale.Y;

        SetSourceDecal(decal, 0);
        var di = BuildQuad(decal, spX, spY, sdX, sdY,
            new Vector2d<float>(0, 0), new Vector2d<float>(0, 1), new Vector2d<float>(1, 1), new Vector2d<float>(1, 0), t);
        Render(di);
    }

    /// <summary>Draws an arbitrary triangle-fan polygon decal through the active effect.</summary>
    /// <param name="decal">The decal sampled for the polygon.</param>
    /// <param name="pos">Vertex positions in screen pixels.</param>
    /// <param name="uv">Per-vertex texture coordinates, parallel to <paramref name="pos"/>.</param>
    /// <param name="colours">Per-vertex colours, parallel to <paramref name="pos"/>.</param>
    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> colours)
        => RenderPolygon(decal, pos, uv, colours, DecalStructure.Fan);

    /// <summary>Draws an untextured white quad at pos with the given size.</summary>
    /// <param name="pos">Top-left position of the quad in screen pixels.</param>
    /// <param name="size">Quad width and height in screen pixels.</param>
    public void DrawQuad(Vector2d<float> pos, Vector2d<float> size)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vb);
        SetVertex(0, pos.X, pos.Y, 1, 0, 0, Pixel.WHITE);
        SetVertex(1, pos.X, pos.Y + size.Y, 1, 0, 1, Pixel.WHITE);
        SetVertex(2, pos.X + size.X, pos.Y + size.Y, 1, 1, 1, Pixel.WHITE);
        SetVertex(3, pos.X + size.X, pos.Y, 1, 1, 0, Pixel.WHITE);
        GL.BufferData(BufferTarget.ArrayBuffer, Marshal.SizeOf<OmniVertex>() * 4, _vertexMem, BufferUsageHint.StreamDraw);
        GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
    }

    /// <summary>Uploads a decal instance's vertices and issues a triangle-fan draw call.</summary>
    /// <param name="di">The decal instance whose positions/UVs/tints/W are uploaded and drawn.</param>
    private void Render(DecalInstance di)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vb);
        for (var i = 0; i < di.Points; i++)
            SetVertex(i, di.Pos[i].X, di.Pos[i].Y, di.W[i], di.Uv[i].X, di.Uv[i].Y, di.Tint[i]);
        GL.BufferData(BufferTarget.ArrayBuffer, Marshal.SizeOf<OmniVertex>() * (int)di.Points, _vertexMem, BufferUsageHint.StreamDraw);
        GL.DrawArrays(PrimitiveType.TriangleFan, 0, (int)di.Points);
    }

    /// <summary>Uploads polygon vertices and draws them with the primitive type matching the given decal structure.</summary>
    /// <param name="decal">The decal bound as the source texture.</param>
    /// <param name="pos">Vertex positions in screen pixels.</param>
    /// <param name="uv">Per-vertex texture coordinates, parallel to <paramref name="pos"/>.</param>
    /// <param name="colours">Per-vertex colours, parallel to <paramref name="pos"/>.</param>
    /// <param name="structure">The primitive layout (List → triangles, Strip → triangle strip, otherwise triangle fan).</param>
    private void RenderPolygon(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> colours, DecalStructure structure)
    {
        SetSourceDecal(decal, 0);
        for (var i = 0; i < pos.Count; i++) SetVertex(i, pos[i].X, pos[i].Y, 1, uv[i].X, uv[i].Y, colours[i]);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vb);
        GL.BufferData(BufferTarget.ArrayBuffer, Marshal.SizeOf<OmniVertex>() * pos.Count, _vertexMem, BufferUsageHint.StreamDraw);
        var mode = structure switch
        {
            DecalStructure.List => PrimitiveType.Triangles,
            DecalStructure.Strip => PrimitiveType.TriangleStrip,
            _ => PrimitiveType.TriangleFan
        };
        GL.DrawArrays(mode, 0, pos.Count);
    }

    /// <summary>Writes position/UV0/colour into the staging vertex at index i.</summary>
    /// <param name="i">Staging-buffer vertex index to write.</param>
    /// <param name="x">Vertex X position.</param>
    /// <param name="y">Vertex Y position.</param>
    /// <param name="z">Vertex perspective term (z).</param>
    /// <param name="u">UV0 U coordinate.</param>
    /// <param name="v">UV0 V coordinate.</param>
    /// <param name="col">Vertex colour (packed into the vertex's RGBA).</param>
    private void SetVertex(int i, float x, float y, float z, float u, float v, Pixel col)
    {
        _vertexMem[i].Px = x; _vertexMem[i].Py = y; _vertexMem[i].Pz = z;
        _vertexMem[i].U0 = u; _vertexMem[i].V0 = v;
        _vertexMem[i].Col = col.N;
    }

    /// <summary>Builds a 4-vertex decal-instance quad from the given screen corners, UVs, and tint.</summary>
    /// <param name="decal">The decal assigned to the instance.</param>
    /// <param name="spX">Quad start (top-left) X in clip space.</param>
    /// <param name="spY">Quad start (top-left) Y in clip space.</param>
    /// <param name="sdX">Quad end (bottom-right) X in clip space.</param>
    /// <param name="sdY">Quad end (bottom-right) Y in clip space.</param>
    /// <param name="uv0">Texture coordinate for the top-left corner.</param>
    /// <param name="uv1">Texture coordinate for the bottom-left corner.</param>
    /// <param name="uv2">Texture coordinate for the bottom-right corner.</param>
    /// <param name="uv3">Texture coordinate for the top-right corner.</param>
    /// <param name="tint">Tint applied to all four vertices.</param>
    /// <returns>A 4-point <see cref="DecalInstance"/> describing the quad.</returns>
    private static DecalInstance BuildQuad(Decal decal, float spX, float spY, float sdX, float sdY,
        Vector2d<float> uv0, Vector2d<float> uv1, Vector2d<float> uv2, Vector2d<float> uv3, Pixel tint)
    {
        var di = new DecalInstance { Decal = decal, Points = 4 };
        di.Pos.Add(new Vector2d<float>(spX, spY)); di.Pos.Add(new Vector2d<float>(spX, sdY));
        di.Pos.Add(new Vector2d<float>(sdX, sdY)); di.Pos.Add(new Vector2d<float>(sdX, spY));
        di.Uv.Add(uv0); di.Uv.Add(uv1); di.Uv.Add(uv2); di.Uv.Add(uv3);
        for (var i = 0; i < 4; i++) { di.W.Add(1f); di.Tint.Add(tint); }
        return di;
    }
}
