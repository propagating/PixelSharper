using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PixelSharper.Core.Utilities;

/// <summary>An immutable snapshot of one method's accumulated timing statistics (all times in milliseconds).</summary>
/// <param name="Name">The method label (the <c>[CallerMemberName]</c> for scopes, or <c>Interface.Method</c> for proxied calls).</param>
/// <param name="Count">Number of recorded invocations.</param>
/// <param name="TotalMs">Total time spent across all invocations.</param>
/// <param name="MeanMs">Mean time per invocation.</param>
/// <param name="MinMs">Fastest recorded invocation.</param>
/// <param name="MaxMs">Slowest recorded invocation.</param>
/// <param name="LastMs">The most recent invocation's time.</param>
public readonly record struct MethodStat(
    string Name, long Count, double TotalMs, double MeanMs, double MinMs, double MaxMs, double LastMs);

/// <summary>
/// A lightweight, thread-safe per-method timing collector that can be injected into any class. Two ways to
/// attach it: a no-alloc <see cref="Measure"/> scope opened at the top of a method (works on any class), or
/// <see cref="Wrap{T}"/>, which returns a transparent proxy that times every call to an interface.
/// </summary>
/// <remarks>
/// <para>Timing uses <see cref="Stopwatch.GetTimestamp"/> (a few ns) and is reported in milliseconds. Set
/// <see cref="Enabled"/> to <c>false</c> to short-circuit recording to near-zero cost.</para>
/// <para>Limitation: a <c>using</c> scope and the proxy both measure wall-clock time, so for <c>async</c>
/// methods they include awaited time and the proxy cannot unwrap the returned <c>Task</c>. Time the
/// synchronous portion, or scope the awaited work explicitly.</para>
/// <para>Display the results with <see cref="Report"/> (a text table) or <see cref="PerfOverlay"/> (an
/// on-screen overlay).</para>
/// </remarks>
public sealed class PerfMonitor
{
    /// <summary>Stopwatch-tick-to-millisecond conversion factor for this machine.</summary>
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    /// <summary>Per-method accumulators, keyed by method label.</summary>
    private readonly ConcurrentDictionary<string, Accumulator> _stats = new();

    /// <summary>When <c>false</c>, <see cref="Measure"/> and <see cref="Wrap{T}"/> record nothing (near-zero overhead).</summary>
    /// <value><c>true</c> (the default) to collect timings; <c>false</c> to disable collection.</value>
    public bool Enabled { get; set; } = true;

    /// <summary>Opens a timing scope for the calling method; dispose it (via <c>using</c>) to record the elapsed time.</summary>
    /// <param name="method">The method label; defaults to the caller's name via <see cref="CallerMemberNameAttribute"/>.</param>
    /// <returns>A no-allocation <see cref="Scope"/> that records on <see cref="Scope.Dispose"/>.</returns>
    public Scope Measure([CallerMemberName] string method = "") => new(this, method);

    /// <summary>Records an elapsed-tick sample for a method label. No-op when <see cref="Enabled"/> is <c>false</c>.</summary>
    /// <param name="method">The method label.</param>
    /// <param name="elapsedTicks">Elapsed <see cref="Stopwatch"/> ticks for one invocation.</param>
    internal void Record(string method, long elapsedTicks)
    {
        if (!Enabled) return;
        _stats.GetOrAdd(method, static _ => new Accumulator()).Add(elapsedTicks);
    }

    /// <summary>Takes an immutable snapshot of every tracked method's stats, sorted slowest-mean first.</summary>
    /// <returns>A list of <see cref="MethodStat"/> ordered by descending <see cref="MethodStat.MeanMs"/>.</returns>
    public IReadOnlyList<MethodStat> Snapshot()
    {
        var list = new List<MethodStat>(_stats.Count);
        foreach (var kvp in _stats) list.Add(kvp.Value.ToStat(kvp.Key, TicksToMs));
        list.Sort(static (x, y) => y.MeanMs.CompareTo(x.MeanMs));
        return list;
    }

    /// <summary>Formats the current stats as an aligned text table (for console/log output or tests).</summary>
    /// <returns>A multi-line table with one row per tracked method, slowest first.</returns>
    public string Report()
    {
        var snap = Snapshot();
        var sb = new StringBuilder();
        sb.Append($"{"Method",-28} {"Count",8} {"Mean",9} {"Min",9} {"Max",9} {"Last",9} {"Total",10}   (ms)\n");
        foreach (var s in snap)
            sb.Append($"{Truncate(s.Name, 28),-28} {s.Count,8} {s.MeanMs,9:F3} {s.MinMs,9:F3} {s.MaxMs,9:F3} {s.LastMs,9:F3} {s.TotalMs,10:F2}\n");
        return sb.ToString();
    }

    /// <summary>Clears all accumulated stats.</summary>
    public void Reset() => _stats.Clear();

    /// <summary>
    /// Wraps an interface implementation in a proxy that times every method call transparently, with no
    /// changes to the implementation. Calls are recorded as <c>InterfaceName.MethodName</c>.
    /// </summary>
    /// <typeparam name="T">The interface type to proxy. Must be an interface (a <see cref="DispatchProxy"/> constraint).</typeparam>
    /// <param name="implementation">The concrete implementation whose calls are forwarded and timed.</param>
    /// <param name="monitor">The monitor that receives the timings.</param>
    /// <returns>A <typeparamref name="T"/> proxy that forwards to <paramref name="implementation"/> while timing each call.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="implementation"/> or <paramref name="monitor"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">If <typeparamref name="T"/> is not an interface.</exception>
    public static T Wrap<T>(T implementation, PerfMonitor monitor) where T : class
    {
        if (implementation is null) throw new ArgumentNullException(nameof(implementation));
        if (monitor is null) throw new ArgumentNullException(nameof(monitor));
        if (!typeof(T).IsInterface)
            throw new ArgumentException(
                $"PerfMonitor.Wrap<T> requires T to be an interface (DispatchProxy limitation); '{typeof(T)}' is not. " +
                "Use the Measure() scope for concrete classes.", nameof(implementation));

        var proxy = DispatchProxy.Create<T, PerfDispatchProxy>()!;
        var p = (PerfDispatchProxy)(object)proxy;
        p.Target = implementation;
        p.Monitor = monitor;
        p.Prefix = typeof(T).Name + ".";
        return proxy;
    }

    /// <summary>Truncates a string to a maximum length (for table alignment).</summary>
    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];

    /// <summary>A no-allocation, disposable timing scope: it captures a start timestamp on creation and records the elapsed time on <see cref="Dispose"/>.</summary>
    public readonly struct Scope : IDisposable
    {
        /// <summary>The owning monitor, or <c>null</c> when collection was disabled at scope creation (so Dispose is a no-op).</summary>
        private readonly PerfMonitor? _monitor;
        /// <summary>The method label to record under.</summary>
        private readonly string _method;
        /// <summary>The start timestamp captured at scope creation.</summary>
        private readonly long _start;

        /// <summary>Creates a scope; captures the start timestamp only when the monitor is enabled.</summary>
        /// <param name="monitor">The owning monitor.</param>
        /// <param name="method">The method label to record under.</param>
        internal Scope(PerfMonitor monitor, string method)
        {
            _method = method;
            if (monitor.Enabled) { _monitor = monitor; _start = Stopwatch.GetTimestamp(); }
            else { _monitor = null; _start = 0; }
        }

        /// <summary>Records the elapsed time since creation, unless collection was disabled.</summary>
        public void Dispose()
        {
            if (_monitor is not null) _monitor.Record(_method, Stopwatch.GetTimestamp() - _start);
        }
    }

    /// <summary>A single method's running totals, updated under a lock so it is safe to record from multiple threads.</summary>
    private sealed class Accumulator
    {
        // System.Threading.Lock (net9+) — the compiler lowers `lock` on it to Lock.EnterScope, which is
        // cheaper than the Monitor-on-object pattern under contention. No Wait/Pulse here, so it fits.
        private readonly System.Threading.Lock _lock = new();
        private long _count, _totalTicks, _minTicks = long.MaxValue, _maxTicks, _lastTicks;

        /// <summary>Adds one timing sample (clamped to be non-negative).</summary>
        public void Add(long ticks)
        {
            if (ticks < 0) ticks = 0;
            lock (_lock)
            {
                _count++; _totalTicks += ticks; _lastTicks = ticks;
                if (ticks < _minTicks) _minTicks = ticks;
                if (ticks > _maxTicks) _maxTicks = ticks;
            }
        }

        /// <summary>Produces an immutable millisecond snapshot under the lock.</summary>
        public MethodStat ToStat(string name, double ticksToMs)
        {
            lock (_lock)
            {
                var mean = _count == 0 ? 0.0 : (double)_totalTicks / _count;
                var min = _count == 0 ? 0 : _minTicks;
                return new MethodStat(name, _count, _totalTicks * ticksToMs, mean * ticksToMs,
                    min * ticksToMs, _maxTicks * ticksToMs, _lastTicks * ticksToMs);
            }
        }
    }
}

/// <summary>
/// The <see cref="DispatchProxy"/> backing <see cref="PerfMonitor.Wrap{T}"/>: it forwards each interface
/// call to the wrapped target while timing it and recording under <c>InterfaceName.MethodName</c>.
/// </summary>
public class PerfDispatchProxy : DispatchProxy
{
    /// <summary>The wrapped implementation that calls are forwarded to.</summary>
    internal object Target = null!;
    /// <summary>The monitor that receives the timings.</summary>
    internal PerfMonitor Monitor = null!;
    /// <summary>The label prefix (the interface name plus a dot).</summary>
    internal string Prefix = "";

    /// <summary>Forwards a call to <see cref="Target"/>, times it, and records the elapsed time.</summary>
    /// <param name="targetMethod">The interface method being invoked.</param>
    /// <param name="args">The call arguments.</param>
    /// <returns>The wrapped method's return value.</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        var start = Stopwatch.GetTimestamp();
        try
        {
            return targetMethod.Invoke(Target, args);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            // Surface the real exception (with its stack), not reflection's TargetInvocationException wrapper.
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable, satisfies the compiler
        }
        finally
        {
            Monitor.Record(Prefix + targetMethod.Name, Stopwatch.GetTimestamp() - start);
        }
    }
}
