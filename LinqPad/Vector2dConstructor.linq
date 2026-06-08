<Query Kind="Program">
  <Namespace>System.Diagnostics</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
</Query>

// ===========================================================================================
// Vector2d constructor: ORIGINAL (validates with a try/catch) vs CHANGED (plain field assignment).
// Run in LINQPad 8 (.NET 8).
//
// WHY THE CHANGE WAS MADE
//   The original constructor "validated" that T was numeric:
//       if (!IsValidNumeric(x) || !IsValidNumeric(y)) throw ...;
//   where IsValidNumeric pokes T.One inside a try/catch. But `where T : INumber<T>` already guarantees,
//   at COMPILE time, that T is a number — so this check can NEVER fail and the throw is unreachable.
//   It was a no-op that ran on every vector you construct... and every operator (+ - * /) constructs a
//   result, so it ran constantly.
//
// WHY IT MATTERED
//   1) Two extra method calls per construction.
//   2) A try/catch, which the JIT generally WON'T inline and which blocks optimizations — so the
//      constructor stayed a real call instead of collapsing to two field stores.
//   The benchmark below isolates exactly that: the only difference between the two structs is the ctor.
//
// THE FIX
//   Delete the check; the constructor is just `X = x; Y = y;` and now inlines away. The engine's
//   benchmark showed the constructing paths (Add/Min/Clamp) get 2.8x-4.5x faster.
// ===========================================================================================

const int N = 10_000;
const int Reps = 500;

void Main()
{
    var a = new VOld<float>[N];
    var b = new VOld<float>[N];
    var an = new VNew<float>[N];
    var bn = new VNew<float>[N];
    var rnd = new Random(1);
    for (var i = 0; i < N; i++)
    {
        float x = (float)rnd.NextDouble(), y = (float)rnd.NextDouble(), x2 = (float)rnd.NextDouble(), y2 = (float)rnd.NextDouble();
        a[i] = new VOld<float>(x, y); b[i] = new VOld<float>(x2, y2);
        an[i] = new VNew<float>(x, y); bn[i] = new VNew<float>(x2, y2);
    }

    var rows = new List<Row>
    {
        Bench("ORIGINAL (ctor validates)", i => _sink += (a[i] + b[i]).X, N, Reps),
        Bench("CHANGED  (ctor plain)",     i => _sink += (an[i] + bn[i]).X, N, Reps),
    };
    rows.Dump("Vector2d construction (operator+) — original vs changed ctor");
    $"(sink={_sink:F0})".Dump();
}

double _sink;

Row Bench(string label, Action<int> body, int n, int reps)
{
    for (var i = 0; i < n; i++) body(i);
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var sw = Stopwatch.StartNew();
    for (var r = 0; r < reps; r++) for (var i = 0; i < n; i++) body(i);
    sw.Stop();
    return new Row(label, $"{sw.Elapsed.TotalMicroseconds / reps:F1} us / {n}");
}

record Row(string Variant, string Mean);

// ORIGINAL: the redundant validation in the constructor.
struct VOld<T> where T : struct, INumber<T>
{
    public T X, Y;
    public VOld(T x, T y)
    {
        if (!Valid(x) || !Valid(y)) throw new ArgumentException("must be numeric"); // can never fire
        X = x; Y = y;
    }
    private static bool Valid<U>(U v) where U : struct, INumber<U>
    {
        try { var _ = U.One; return true; } catch { return false; }   // try/catch blocks inlining
    }
    public static VOld<T> operator +(VOld<T> a, VOld<T> b) => new VOld<T>(a.X + b.X, a.Y + b.Y);
}

// CHANGED: plain assignment -> the constructor inlines.
struct VNew<T> where T : struct, INumber<T>
{
    public T X, Y;
    public VNew(T x, T y) { X = x; Y = y; }
    public static VNew<T> operator +(VNew<T> a, VNew<T> b) => new VNew<T>(a.X + b.X, a.Y + b.Y);
}
