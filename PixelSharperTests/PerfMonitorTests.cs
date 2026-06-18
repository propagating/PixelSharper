using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using PixelSharper.Core.Utilities;

namespace PixelSharperTests
{
    // Exercises the PerfMonitor utility: the using-scope path, the Wrap<T> DispatchProxy path (return-value
    // forwarding + exception unwrapping + interface-only guard), the Enabled gate, and thread safety.
    [TestFixture]
    public class PerfMonitorTests
    {
        public interface ICalc
        {
            int Add(int a, int b);
            void Boom();
        }

        private sealed class Calc : ICalc
        {
            public int Calls;
            public int Add(int a, int b) { Calls++; return a + b; }
            public void Boom() => throw new InvalidOperationException("boom");
        }

        [Test]
        public void Measure_RecordsCountAndOrdersMinMeanMax()
        {
            var pm = new PerfMonitor();
            for (var i = 0; i < 5; i++) using (pm.Measure("Work")) { Spin(); }

            var stat = pm.Snapshot().Single(s => s.Name == "Work");
            Assert.AreEqual(5, stat.Count);
            Assert.That(stat.MinMs, Is.LessThanOrEqualTo(stat.MeanMs));
            Assert.That(stat.MeanMs, Is.LessThanOrEqualTo(stat.MaxMs));
            Assert.That(stat.TotalMs, Is.GreaterThanOrEqualTo(stat.MaxMs));
        }

        [Test]
        public void Measure_AutoNamesFromCaller()
        {
            var pm = new PerfMonitor();
            using (pm.Measure()) { } // no explicit name -> [CallerMemberName]
            Assert.IsTrue(pm.Snapshot().Any(s => s.Name == nameof(Measure_AutoNamesFromCaller)));
        }

        [Test]
        public void Disabled_RecordsNothing()
        {
            var pm = new PerfMonitor { Enabled = false };
            using (pm.Measure("Ignored")) { Spin(); }
            Assert.IsEmpty(pm.Snapshot());
        }

        [Test]
        public void Reset_ClearsStats()
        {
            var pm = new PerfMonitor();
            using (pm.Measure("X")) { }
            pm.Reset();
            Assert.IsEmpty(pm.Snapshot());
        }

        [Test]
        public void Wrap_TimesCalls_AndForwardsReturnValue()
        {
            var pm = new PerfMonitor();
            var impl = new Calc();
            var proxy = PerfMonitor.Wrap<ICalc>(impl, pm);

            Assert.AreEqual(7, proxy.Add(3, 4), "return value forwarded");
            Assert.AreEqual(7, proxy.Add(2, 5));
            Assert.AreEqual(2, impl.Calls, "calls reached the real implementation");

            var stat = pm.Snapshot().Single(s => s.Name == "ICalc.Add");
            Assert.AreEqual(2, stat.Count);
        }

        [Test]
        public void Wrap_PropagatesUnwrappedException_AndStillRecords()
        {
            var pm = new PerfMonitor();
            var proxy = PerfMonitor.Wrap<ICalc>(new Calc(), pm);

            // The caller must see the real exception, NOT reflection's TargetInvocationException wrapper.
            var ex = Assert.Throws<InvalidOperationException>(() => proxy.Boom());
            Assert.AreEqual("boom", ex!.Message);
            Assert.IsNotInstanceOf<TargetInvocationException>(ex);

            // And the failing call is still timed (recorded in the finally block).
            Assert.AreEqual(1, pm.Snapshot().Single(s => s.Name == "ICalc.Boom").Count);
        }

        [Test]
        public void Wrap_RequiresInterface()
        {
            var pm = new PerfMonitor();
            Assert.Throws<ArgumentException>(() => PerfMonitor.Wrap<Calc>(new Calc(), pm));
        }

        [Test]
        public void Record_IsThreadSafe()
        {
            var pm = new PerfMonitor();
            Parallel.For(0, 2000, _ => { using var s = pm.Measure("Concurrent"); });
            Assert.AreEqual(2000, pm.Snapshot().Single(s => s.Name == "Concurrent").Count);
        }

        [Test]
        public void Report_ContainsTrackedMethods()
        {
            var pm = new PerfMonitor();
            using (pm.Measure("Alpha")) { }
            using (pm.Measure("Beta")) { }
            var report = pm.Report();
            StringAssert.Contains("Alpha", report);
            StringAssert.Contains("Beta", report);
            StringAssert.Contains("Method", report);
        }

        private static void Spin()
        {
            var x = 0.0;
            for (var i = 0; i < 500; i++) x += Math.Sqrt(i + 1);
            if (double.IsNaN(x)) throw new InvalidOperationException();
        }
    }
}
