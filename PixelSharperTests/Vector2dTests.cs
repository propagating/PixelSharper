using System;
using System.IO;
using PixelSharper.Core.Types;
namespace PixelSharperTests;
using NUnit.Framework;

[TestFixture]
public class Vector2dTests
{
    [Test]
    public void Test_Vector2d_Constructor()
    {
        var vector = new Vector2d<int>(3, 4);
        Assert.AreEqual(3, vector.X);
        Assert.AreEqual(4, vector.Y);
    }

    [Test]
    public void Test_Vector2d_Equality()
    {
        var vector1 = new Vector2d<int>(3, 4);
        var vector2 = new Vector2d<int>(3, 4);
        Assert.IsTrue(vector1 == vector2);
    }

    [Test]
    public void Test_Vector2d_Inequality()
    {
        var vector1 = new Vector2d<int>(3, 4);
        var vector2 = new Vector2d<int>(4, 3);
        Assert.IsTrue(vector1 != vector2);
    }

    [Test]
    public void Test_Vector2d_Addition()
    {
        var vector1 = new Vector2d<int>(1, 2);
        var vector2 = new Vector2d<int>(3, 4);
        var result = vector1 + vector2;

        var expected = new Vector2d<int>(4, 6);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Test_Vector2d_Subtraction()
    {
        var vector1 = new Vector2d<int>(5, 7);
        var vector2 = new Vector2d<int>(2, 3);
        var result = vector1 - vector2;

        var expected = new Vector2d<int>(3, 4);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Test_Vector2d_DotProduct()
    {
        var vector1 = new Vector2d<int>(1, 2);
        var vector2 = new Vector2d<int>(3, 4);
        var result = vector1.DotProduct<int, int>(vector2);

        int expected = 11; // 1*3 + 2*4
        Assert.AreEqual(expected, result);
    }
    
    [Test]
    public void Test_CrossProduct()
    {
        var v1 = new Vector2d<int>(3, 4);
        var v2 = new Vector2d<int>(5, 6);
        int cross = v1.CrossProduct<int, int>(v2);
        Assert.AreEqual(-2, cross);

        var v3 = new Vector2d<double>(1.5, 2.0);
        var v4 = new Vector2d<double>(3.0, 4.0);
        double crossD = v3.CrossProduct<double, double>(v4);
        Assert.That(crossD, Is.EqualTo(0).Within(1e-10));
    }
    
    [Test]
    public void Test_Vector2d_Magnitude()
    {
        var vector = new Vector2d<double>(3.0, 4.0);
        var magnitude = vector.Magnitude();

        double expected = 5.0; // sqrt(3^2 + 4^2)
        Assert.AreEqual(expected, magnitude);
    }

    [Test]
    public void Test_Vector2d_Normalize()
    {
        var vector = new Vector2d<double>(3.0, 4.0);
        var normalized = vector.Normalize();

        var expected = new Vector2d<double>(0.6, 0.8);
        Assert.AreEqual(expected.X, normalized.X, 1e-6);
        Assert.AreEqual(expected.Y, normalized.Y, 1e-6);
    }

    [Test]
    public void Test_Vector2d_ZeroNormalization()
    {
        var vector = new Vector2d<double>(0.0, 0.0);

        Assert.Throws<InvalidOperationException>(() => vector.Normalize());
    }

    [Test]
    public void Test_Vector2d_MagnitudeSquared()
    {
        var vector = new Vector2d<double>(3.0, 4.0);
        var magnitudeSquared = vector.MagnitudeSquared();

        double expected = 25.0; // 3^2 + 4^2
        Assert.AreEqual(expected, magnitudeSquared);
    }
    
    [Test]
    public void Test_Vector2d_Lerp_ForceIntegerWithInteger()
    {
        var vector1 = new Vector2d<int>(0, 0);
        var vector2 = new Vector2d<int>(10, 10);
        var result = Vector2d<int>.Lerp(vector1, vector2, 0.5f, true);

        var expected = new Vector2d<int>(0, 0);
        Assert.AreEqual(expected, result);
    }
    
    [Test]
    public void Test_Vector2d_Lerp_NoForceIntegerWithInteger()
    {
        var vector1 = new Vector2d<int>(0, 0);
        var vector2 = new Vector2d<int>(10, 10);

        Assert.Throws<InvalidOperationException>(() => Vector2d<int>.Lerp(vector1, vector2, 0.5f, false));
    }
    
        
    [Test]
    public void Test_Vector2d_Lerp_FloatingPoint()
    {
        var vector1 = new Vector2d<float>(0, 0);
        var vector2 = new Vector2d<float>(10, 10);
        var result = Vector2d<float>.Lerp(vector1, vector2, 0.5f, true);

        var expected = new Vector2d<float>(5, 5);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Test_Vector2d_Lerp_DoubleFloatingPoint()
    {
        var vector1 = new Vector2d<double>(0, 0);
        var vector2 = new Vector2d<double>(10, 10);
        var result = Vector2d<double>.Lerp(vector1, vector2, 0.5f, true);

        var expected = new Vector2d<double>(5, 5);
        Assert.AreEqual(expected, result);
    }
    
    [Test]
    public void Reflect_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<double>(1.0, -1.0);
        var normal = new Vector2d<double>(0.0, 1.0);
        var result = vector.Reflect(normal);

        var expected = new Vector2d<double>(1.0, 1.0);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Clamp_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<int>(5, 10);
        var min = new Vector2d<int>(0, 0);
        var max = new Vector2d<int>(10, 5);
        var result = vector.Clamp(min, max);

        var expected = new Vector2d<int>(5, 5);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void FromPolar_ShouldReturnCorrectResult()
    {
        var r = 5.0;
        var theta = Math.PI / 4;
        var result = Vector2d<double>.FromPolar(r, theta);

        var expected = new Vector2d<double>(Math.Sqrt(12.5), Math.Sqrt(12.5));
        Assert.AreEqual(expected.X, result.X, 1e-6);
        Assert.AreEqual(expected.Y, result.Y, 1e-6);
    }

    [Test]
    public void ToPolar_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<double>(Math.Sqrt(12.5), Math.Sqrt(12.5));
        var (r, theta) = vector.ToPolar();

        Assert.AreEqual(5.0, r, 1e-6);
        Assert.AreEqual(Math.PI / 4, theta, 1e-6);
    }

    [Test]
    public void Min_ShouldReturnCorrectResult()
    {
        var vector1 = new Vector2d<int>(1, 4);
        var vector2 = new Vector2d<int>(3, 2);
        var result = Vector2d<int>.Min(vector1, vector2);

        var expected = new Vector2d<int>(1, 2);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Max_ShouldReturnCorrectResult()
    {
        var vector1 = new Vector2d<int>(1, 4);
        var vector2 = new Vector2d<int>(3, 2);
        var result = Vector2d<int>.Max(vector1, vector2);

        var expected = new Vector2d<int>(3, 4);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Ceiling_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<double>(1.2, 3.7);
        var result = vector.Ceiling<double>();

        var expected = new Vector2d<double>(2.0, 4.0);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Floor_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<double>(1.8, 3.2);
        var result = vector.Floor<double>();

        var expected = new Vector2d<double>(1.0, 3.0);
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void Perpendicular_ShouldReturnCorrectResult()
    {
        var vector = new Vector2d<int>(3, 4);
        var result = vector.Perpendicular();

        var expected = new Vector2d<int>(-4, 3);
        Assert.AreEqual(expected, result);
    }
    
    
    [Test]
    public void Test_AngleBetween()
    {
        var v1 = new Vector2d<double>(1, 0);
        var v2 = new Vector2d<double>(0, 1);
        double angle = Vector2d<double>.AngleBetween(v1, v2);
        Assert.That(angle, Is.EqualTo(Math.PI / 2).Within(1e-10));
    }

    [Test]
    public void Test_Distance()
    {
        var v1 = new Vector2d<double>(0, 0);
        var v2 = new Vector2d<double>(3, 4);
        double dist = Vector2d<double>.Distance(v1, v2);
        Assert.That(dist, Is.EqualTo(5.0).Within(1e-10));
    }

    [Test]
    public void Test_DistanceSquared()
    {
        var v1 = new Vector2d<int>(1, 2);
        var v2 = new Vector2d<int>(4, 6);
        int distSq = Vector2d<int>.DistanceSquared(v1, v2);
        Assert.AreEqual(25, distSq);
    }

    [Test]
    public void Test_Negate()
    {
        var v = new Vector2d<int>(3, -7);
        var neg = v.Negate();
        Assert.AreEqual(new Vector2d<int>(-3, 7), neg);
    }

    [Test]
    public void Test_Swizzle()
    {
        var v = new Vector2d<int>(1, 2);
        var swizzled = v.Swizzle(true);
        Assert.AreEqual(new Vector2d<int>(2, 1), swizzled);
        var notSwizzled = v.Swizzle(false);
        Assert.AreEqual(new Vector2d<int>(1, 2), notSwizzled);
    }
    
}