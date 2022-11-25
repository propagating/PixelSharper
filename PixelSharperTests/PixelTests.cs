using NUnit.Framework;
using PixelSharper.Core.Resources;


namespace PixelSharperTests;

//TODO: Replace with Test Fixture(s)
public class PixelTests
{

    [Test]
    public void UnionFromBytes()
    {
        var pixel = new Pixel(100, 100, 100, 255);
        Assert.AreEqual(pixel.N, 4284769380);
    }
    
    [Test]
    public void UnionFromInt()
    {
        var pixel = new Pixel(4284769380);
        Assert.Multiple(()=> {
            Assert.AreEqual(pixel.Red, 100);
            Assert.AreEqual(pixel.Green, 100);
            Assert.AreEqual(pixel.Blue, 100);
            Assert.AreEqual(pixel.Alpha, 255);
        });
    }
    
    [Test]
    public void Multiply()
    {
        var pixel = new Pixel(4284769380);
        pixel = pixel * 2;
        Assert.Multiple(()=> {
            Assert.AreEqual(pixel.Red, 200);
            Assert.AreEqual(pixel.Green, 200);
            Assert.AreEqual(pixel.Blue, 200);
            Assert.AreEqual(pixel.Alpha, 255);
        });
    }
    
    [Test]
    public void Divide()
    {
        var pixel = new Pixel(4284769380);
        pixel = pixel / 2;
        Assert.Multiple(()=> {
            Assert.AreEqual(pixel.Red, 50);
            Assert.AreEqual(pixel.Green, 50);
            Assert.AreEqual(pixel.Blue, 50);
            Assert.AreEqual(pixel.Alpha, 255);
        });
    }
    
        
    [Test]
    public void Add()
    {
        var p1 = new Pixel(4284769380);
        var p2 = new Pixel(20,255,20,255);
        var p3 = p1 + p2;
        Assert.Multiple(()=> {
            Assert.AreEqual(p3.Red, 120);
            Assert.AreEqual(p3.Green, 255);
            Assert.AreEqual(p3.Blue, 120);
            Assert.AreEqual(p3.Alpha, 255);
        });
    }
    
            
    [Test]
    public void Subtract()
    {
        var p1 = new Pixel(4284769380);
        var p2 = new Pixel(20,255,20,255);
        var p3 = p1 - p2;
        Assert.Multiple(()=> {
            Assert.AreEqual(p3.Red, 80);
            Assert.AreEqual(p3.Green, 0);
            Assert.AreEqual(p3.Blue, 80);
            Assert.AreEqual(p3.Alpha, 255);
        });
    }

    [Test]
    public void Inverse()
    {
        var p1 = new Pixel(100, 255, 100, 255);
        p1 = p1.Inverse();
        Assert.Multiple(()=> {
            Assert.AreEqual(p1.Red, 155);
            Assert.AreEqual(p1.Green, 0);
            Assert.AreEqual(p1.Blue, 155);
            Assert.AreEqual(p1.Alpha, 255);
        });
    }
    
    [Test]
    public void EqualsFunction()
    {
        var p1 = new Pixel(100, 100, 100, 255);
        var p2 = new Pixel(4284769380);
        Assert.IsTrue(p1.Equals(p2));
    }
    
    [Test]
    public void EqualsOperator()
    {
        var p1 = new Pixel(100, 100, 100, 255);
        var p2 = new Pixel(4284769380);
        Assert.IsFalse(p1 != p2);
    }
}
