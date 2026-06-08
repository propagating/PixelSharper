<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
</Query>

// Vector2d boxing: OLD (Convert.ToDouble -> boxes a generic T) vs NEW (generic math: T.Min/T.Clamp,
// double.CreateChecked -> no boxing). Run in LINQPad 8 (targets .NET 8) — generic math (INumber<T>)
// needs .NET 7+. Watch the "Alloc/op" column go to zero and the times drop.
//
// This is a lightweight in-process harness (Stopwatch + GC.GetAllocatedBytesForCurrentThread), not
// BenchmarkDotNet — the absolute times won't match the BDN run, but the ALLOCATIONS are exact and the
// speedups tell the same story.

const int N = 10_000;   // an "op" = one pass over N elements (mirrors the BenchmarkDotNet loop)
const int Reps = 200;

double _sink;           // accumulator to stop the JIT eliding the work

void Main()
{
    var rnd = new Random(1);
    var a = new V<float>[N];
    var b = new V<float>[N];
    for (var i = 0; i < N; i++)
    {
        a[i] = new V<float>((float)(rnd.NextDouble() * 100), (float)(rnd.NextDouble() * 100));
        b[i] = new V<float>((float)(rnd.NextDouble() * 100), (float)(rnd.NextDouble() * 100));
    }
    var lo = new V<float>(10, 10);
    var hi = new V<float>(90, 90);

    var rows = new List<Row>
    {
        Compare("Magnitude", i => _sink += a[i].MagnitudeOld(),         i => _sink += a[i].MagnitudeNew()),
        Compare("Distance",  i => _sink += V<float>.DistanceOld(a[i], b[i]), i => _sink += V<float>.DistanceNew(a[i], b[i])),
        Compare("Min",       i => _sink += V<float>.MinOld(a[i], b[i]).X,    i => _sink += V<float>.MinNew(a[i], b[i]).X),
        Compare("Clamp",     i => _sink += a[i].ClampOld(lo, hi).X,      i => _sink += a[i].ClampNew(lo, hi).X),
    };

    rows.Dump("Vector2d boxing — old (Convert.ToDouble) vs new (generic math)");
    $"(sink = {_sink:F0} — ignore; just prevents dead-code elimination)".Dump();
}

Row Compare(string method, Action<int> oldBody, Action<int> newBody)
{
    var o = Bench(oldBody);
    var n = Bench(newBody);
    return new Row(
        method,
        $"{o.meanUs:F1} us", $"{n.meanUs:F1} us", $"{o.meanUs / n.meanUs:F1}x",
        $"{o.alloc:N0} B", $"{n.alloc:N0} B");
}

(double meanUs, long alloc) Bench(Action<int> body)
{
    for (var i = 0; i < N; i++) body(i);                 // warm up / JIT
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

    var before = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < Reps; r++)
        for (var i = 0; i < N; i++) body(i);
    sw.Stop();
    var after = GC.GetAllocatedBytesForCurrentThread();

    return (sw.Elapsed.TotalMicroseconds / Reps, (long)Math.Round((after - before) / (double)Reps));
}

record Row(string Method, string OldMean, string NewMean, string Speedup, string OldAllocPerOp, string NewAllocPerOp);

// A trimmed generic vector with BOTH versions of each method, so old and new sit side by side.
struct V<T> where T : INumber<T>
{
    public T X, Y;
    public V(T x, T y) { X = x; Y = y; }

    // ---------- OLD: Convert.ToDouble(T) binds to Convert.ToDouble(object) -> boxes every call ----------
    public double MagnitudeOld()                                  // 4 boxes
        => Math.Sqrt(Convert.ToDouble(X) * Convert.ToDouble(X) + Convert.ToDouble(Y) * Convert.ToDouble(Y));

    public static double DistanceOld(V<T> a, V<T> b)              // 1 box
    {
        T dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(Convert.ToDouble((dx * dx) + (dy * dy)));
    }

    public static V<T> MinOld(V<T> a, V<T> b)                     // 4 boxes
        => new V<T>(T.CreateChecked(Math.Min(Convert.ToDouble(a.X), Convert.ToDouble(b.X))),
                    T.CreateChecked(Math.Min(Convert.ToDouble(a.Y), Convert.ToDouble(b.Y))));

    public V<T> ClampOld(V<T> lo, V<T> hi)                        // 6 boxes
        => new V<T>(T.CreateChecked(Math.Min(Math.Max(Convert.ToDouble(X), Convert.ToDouble(lo.X)), Convert.ToDouble(hi.X))),
                    T.CreateChecked(Math.Min(Math.Max(Convert.ToDouble(Y), Convert.ToDouble(lo.Y)), Convert.ToDouble(hi.Y))));

    // ---------- NEW: generic math — no object, no box ----------
    public double MagnitudeNew()
    {
        double x = double.CreateChecked(X), y = double.CreateChecked(Y);   // T -> double, no box
        return Math.Sqrt(x * x + y * y);
    }

    public static double DistanceNew(V<T> a, V<T> b)
    {
        T dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(double.CreateChecked((dx * dx) + (dy * dy)));
    }

    public static V<T> MinNew(V<T> a, V<T> b) => new V<T>(T.Min(a.X, b.X), T.Min(a.Y, b.Y));            // pure T

    public V<T> ClampNew(V<T> lo, V<T> hi) => new V<T>(T.Clamp(X, lo.X, hi.X), T.Clamp(Y, lo.Y, hi.Y)); // pure T
}
