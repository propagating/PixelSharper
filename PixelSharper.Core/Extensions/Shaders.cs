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
public class EffectConfig
{
    public string VertexSource = "";
    public string PixelSource = "";
    public int Inputs = 1;
    public int Outputs = 1;
    public List<(string Name, string Type, string Default)> Attributes = new();
}

// A built-in library of effects (matching olc::fx::FX_*).
public static class Fx
{
    public const string ShaderHeader = "#version 330 core       \n";

    public const string DefaultVs =
        "void main()                                             \n" +
        "{                                                       \n" +
        "    float p = 1.0 / inPos.z;                            \n" +
        "    gl_Position = p * vec4(inPos.x, inPos.y, 0.0, 1.0); \n" +
        "    xUV1 = p * inUV1;                                   \n" +
        "    xCol = inCol;                                       \n" +
        "}                                                       \n";

    public const string DefaultPs =
        "void main()                              \n" +
        "{                                        \n" +
        "    pix_out = texture(tex1, xUV1) * xCol;\n" +
        "}                                        \n";

    public static EffectConfig Normal => new() { VertexSource = DefaultVs, PixelSource = DefaultPs };

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

public class Effect
{
    private string _status = "";
    internal int Psid, Vsid, Id;
    internal int InputSlots, TargetSlots;

    public bool IsOK() => _status.Length == 0;
    public string GetStatus() => _status;
    public int GetTargetSlots() => TargetSlots;
    public int GetInputSlots() => InputSlots;

    internal void AppendStatus(string msg) => _status += msg;
    internal void SetResourceIDs(int id, int vsid, int psid) { Id = id; Vsid = vsid; Psid = psid; }
    internal void SetSlots(int input, int target) { InputSlots = input; TargetSlots = target; }
}

public class Shade : PGEX
{
    private const int MaxVerts = 128;

    [StructLayout(LayoutKind.Sequential)]
    private struct OmniVertex
    {
        public float Px, Py, Pz;
        public uint Col;
        public float U0, V0, U1, V1, U2, V2, U3, V3, U4, V4, U5, V5, U6, V6, U7, V7;
    }

    private struct ResourceSlot
    {
        public bool InUse;
        public int TargetResId;
        public Vector2d<float> Pos, Size, InvSize;
    }

    private readonly ResourceSlot[] _slotTarget = new ResourceSlot[8];
    private readonly ResourceSlot[] _slotSource = new ResourceSlot[8];
    private readonly OmniVertex[] _vertexMem = new OmniVertex[MaxVerts];

    private int _vb, _va, _fbo;
    private readonly Renderable _dummyTex = new();

    public Shade() : base(true) { }

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

    public void SetTargetDecal(Decal decal, int slot = 0)
    {
        _slotTarget[slot].InUse = true;
        _slotTarget[slot].TargetResId = decal.Id;
        _slotTarget[slot].Size = new Vector2d<float>(decal.Sprite.Width, decal.Sprite.Height);
        _slotTarget[slot].InvSize = new Vector2d<float>(1f / decal.Sprite.Width, 1f / decal.Sprite.Height);
    }

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

    public void End()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Clear(Pixel? p = null)
    {
        var c = p ?? Pixel.BLANK;
        GL.ClearColor(c.Red / 255f, c.Green / 255f, c.Blue / 255f, c.Alpha / 255f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

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

    public void DrawPolygonDecal(Decal decal, IReadOnlyList<Vector2d<float>> pos, IReadOnlyList<Vector2d<float>> uv, IReadOnlyList<Pixel> colours)
        => RenderPolygon(decal, pos, uv, colours, DecalStructure.Fan);

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

    private void Render(DecalInstance di)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vb);
        for (var i = 0; i < di.Points; i++)
            SetVertex(i, di.Pos[i].X, di.Pos[i].Y, di.W[i], di.Uv[i].X, di.Uv[i].Y, di.Tint[i]);
        GL.BufferData(BufferTarget.ArrayBuffer, Marshal.SizeOf<OmniVertex>() * (int)di.Points, _vertexMem, BufferUsageHint.StreamDraw);
        GL.DrawArrays(PrimitiveType.TriangleFan, 0, (int)di.Points);
    }

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

    private void SetVertex(int i, float x, float y, float z, float u, float v, Pixel col)
    {
        _vertexMem[i].Px = x; _vertexMem[i].Py = y; _vertexMem[i].Pz = z;
        _vertexMem[i].U0 = u; _vertexMem[i].V0 = v;
        _vertexMem[i].Col = col.N;
    }

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
