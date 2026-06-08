using System.Reflection;
using BenchmarkDotNet.Running;

// Run all benchmarks (or filter): dotnet run -c Release --project PixelSharper.Benchmarks
// e.g. `... -- --filter *Vector2d*`
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
