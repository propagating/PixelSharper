<Query Kind="Statements" />

var stringTest = "testing some strings";

Console.WriteLine($"String Length : {stringTest.Length}");
Console.WriteLine($"String Size UTF8: {Encoding.UTF8.GetByteCount(stringTest)}");
Console.WriteLine($"String Size ASCII: {Encoding.ASCII.GetByteCount(stringTest)}"); 
Console.WriteLine($"String Size UTF16 : {Encoding.Unicode.GetByteCount(stringTest)}");