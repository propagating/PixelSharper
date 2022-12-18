<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

void Main()
{
	var p = new Pixel(64,128,32,16);
	var q = new Pixel(270565440);
	Console.WriteLine(Marshal.SizeOf(typeof(Pixel)));
}

[StructLayout(LayoutKind.Explicit, Size = 5, Pack = 1 )]
public struct Pixel
{

	[FieldOffset(0)] public uint N;
	[FieldOffset(0)] public byte Red;
	[FieldOffset(1)] public byte Green;
	[FieldOffset(2)] public byte Blue;
	[FieldOffset(3)] public byte Alpha;
	[FieldOffset(4)] public byte DisplayMode;

	public Pixel(uint pixel) : this()
	{
		N = pixel;
		DisplayMode = 0x01;
	}

	public Pixel(float r, float g, float b, float a) : this()
	{
		Red = (byte)(r * 255.0f);
		Green = (byte)(g * 255.0f);
		Blue = (byte)(b * 255.0f);
		Alpha = (byte)(a * 255.0f);
		DisplayMode = 0x01;
	}

	//TODO: update alpha to use default from CoreConfig Settings
	public Pixel(byte red, byte green, byte blue, byte alpha = 0XFF) : this()
	{
		N = (uint)(red | green << 8 | blue << 16 | alpha << 24);
		DisplayMode = 0x01;
	}
}
