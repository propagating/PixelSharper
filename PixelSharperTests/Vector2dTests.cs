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
}