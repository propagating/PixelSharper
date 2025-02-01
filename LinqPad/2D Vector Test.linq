<Query Kind="Program">
  <Namespace>System.Numerics</Namespace>
</Query>

void Main()
{

	var v1 = new Vector2d<int>(1, 2);
	var v2 = new Vector2d<int>(1, 2);
	var s = 2;
	var result = v1/s;
	Console.WriteLine($"{result.X} : {result.Y}");
}

// You can define other methods, fields, classes and namespaces here

public struct Vector2d<T> where T : INumber<T>
{
	public T X;
	public T Y;

	public Vector2d(T x, T y)
	{
		X = x;
		Y = y;
	}

	public static Vector2d<T> operator /(Vector2d<T> left, T scalar)
	{
		return new Vector2d<T>(left.X / scalar, left.Y / scalar);
	}

}