using System.Numerics;

namespace PixelSharper.Core.Extensions;


/// <summary>
/// C# provides a built in Vector2 class that we can utilize to a similar effect to the class provided in the olcPGE.
/// Unfortunately, the same level of generic Maths support is much more difficult to achieve in C# without a large amount of work.
/// For the time being, we can achieve similar functionality with extension methods to the base Vector2 class and go from there.   
/// </summary>
public static class Vector2Extensions
{
    
    /// <summary>
    /// Just wraps the Length() function for sake of consistency
    /// </summary>
    /// <param name="vec"></param>
    /// <returns></returns>
    public static double Magnitude(Vector2 vec)
    {
        return vec.Length();
    }

    /// <summary>
    /// Just wraps the LengthSquared() Function for sake of consistency
    /// </summary>
    /// <param name="vec"></param>
    /// <returns></returns>
    public static double MagnitudeSquared(Vector2 vec)
    {
       return vec.LengthSquared();
    }

    public static Vector2 Perpendicular(Vector2 vec)
    { 
        return new Vector2(-vec.Y, vec.X);
    }

    public static Vector2 Floor(Vector2 vec)
    {
        return new Vector2((float)Math.Floor(vec.X), (float)Math.Floor(vec.Y));
    }
    
    public static Vector2 Ceiling(Vector2 vec)
    {
        return new Vector2((float)Math.Ceiling(vec.X), (float)Math.Ceiling(vec.Y));
    }

    //TODO: Add indicator to prevent double converting to cartesian/polar coordinates
    public static Vector2 ConvertToCartesian(Vector2 vec)
    {
        return new Vector2((float)(Math.Cos(vec.Y) * vec.X), (float)(Math.Sin(vec.Y) * vec.X));
    }

    public static Vector2 ConvertToPolar(Vector2 vec)
    {
        return new Vector2(vec.Length(), (float)Math.Atan2(vec.Y, vec.X));
    }

    public static double Cross(Vector2 left, Vector2 right)
    {
        return left.X * right.Y - left.Y * right.X;
    }

    /// <summary>
    /// Is the Left Vector less than the Right Vector within specified tolerance 
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <param name="tolerance">Defaults to 1e-10</param>
    /// <returns></returns>
    public static bool LessThan(Vector2 left, Vector2 right, double tolerance = 1e-10)
    {
        return left.Y <right.Y || (Math.Abs(left.Y - right.Y) < tolerance && left.X < right.X);

    }
    
    /// <summary>
    /// Is the Left Vector greater than the Right Vector within specified tolerance 
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <param name="tolerance">Defaults to 1e-10</param>
    /// <returns></returns>
    public static bool GreaterThan(Vector2 left, Vector2 right, double tolerance = 1e-10)
    {
        return left.Y > right.Y || (Math.Abs(left.Y - right.Y) < tolerance && left.X > right.X);
    }
    
}
