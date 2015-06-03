using System;
using NUnit.Framework;
using Spreads;
using System.Diagnostics;

namespace QuikSharp.Tests {
    [TestFixture]
    public class PeriodTests {

        public int[] unitPeriods = { -1, 1, 2, 3, 4, 5, 6 };

        [Test]
        public void CouldCreatePeriod() {
            var p = new Period(UnitPeriod.Day, 1, DateTime.Today.ToUniversalTime());
            var tom = new Period(UnitPeriod.Day, 1, DateTime.Today.AddDays(1));
            Assert.That(p.Add(1).Add(-1), Is.EqualTo(p));
            Assert.That(p.Add(1), Is.EqualTo(tom));
        }

        [Test]
        public void CouldAddPeriod() {
            Console.WriteLine(DateTimeOffset.UtcNow.Date);
            for (int step = 0; step < 10000; step++) {
                foreach (var i in unitPeriods) {
                    var unitPeriod = (UnitPeriod)i;
                    var p = new Period(unitPeriod, 1, DateTimeOffset.UtcNow.Date);
                    var next = p.Next;
                    var steppedForward = p.Add(step);
                    var diff = steppedForward.Diff(p);

                    //Console.WriteLine("UP: " + (UnitPeriod)i);
                    //Console.WriteLine("p: " + p);
                    //Console.WriteLine("next.Previous: " + next.Previous);
                    Assert.That(next.Previous, Is.EqualTo(p), "previous " + (UnitPeriod)i); // failing
                    Assert.That(p.Next, Is.EqualTo(next), "next " + (UnitPeriod)i);
                    Assert.That(steppedForward.Add(-step), Is.EqualTo(p), "steppedForward - step " + (UnitPeriod)i);
                    Assert.That(diff, Is.EqualTo(step), "diff " + (UnitPeriod)i);
                }
            }
        }

        [Test]
        public void ComparePerformance()
        {
            var count = 10000;
            Period[] periods = new Period[count];
            Period[] periods2 = new Period[count];
            DateTime[] dts = new DateTime[count];
            var p = new Period(UnitPeriod.Second, 1, DateTimeOffset.UtcNow.Date);
            var p2 = new Period(UnitPeriod.Tick, 1, DateTimeOffset.UtcNow.Date);

            for (int i = 0; i < count; i++) {
                periods[i] = p.Add(i);
                periods2[i] = p2.Add(i);
                dts[i] = DateTime.Today.AddTicks(i);
            }
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 1; i < count; i++) {
                for (int ii = 0; ii < count; ii++)
                {
                    var isSmaller = periods[i - 1] < periods[i];
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed for Period " + sw.ElapsedMilliseconds);

            sw = new Stopwatch();
            sw.Start();
            for (int i = 1; i < count; i++) {
                for (int ii = 0; ii < count; ii++) {
                    var isSmaller = periods[i - 1] < periods2[i];
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed for Period 2 " + sw.ElapsedMilliseconds);

            sw = new Stopwatch();
            sw.Start();
            for (int i = 1; i < count; i++) {
                for (int ii = 0; ii < count; ii++)
                {
                    var isSmaller = (i - 1).CompareTo(i);
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed for int " + sw.ElapsedMilliseconds);

            sw = new Stopwatch();
            sw.Start();
            for (int i = 1; i < count; i++) {
                for (int ii = 0; ii < count; ii++) {
                    var isSmaller = dts[i - 1] < DateTime.SpecifyKind(dts[i], DateTimeKind.Utc);
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed for DateTime " + sw.ElapsedMilliseconds);

        }
    }
}
