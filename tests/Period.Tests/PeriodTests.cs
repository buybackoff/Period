using System;
using NUnit.Framework;
using Period;

namespace QuikSharp.Tests {
	[TestFixture]
	public class PeriodTests {

		public int[] unitPeriods = { -1, 1, 2, 3, 4, 5, 6 };

		[Test]
		public void CouldCreatePeriod() {
			var p = new Period.Period(UnitPeriod.Day, 1, DateTime.Today.ToUniversalTime());
			var tom = new Period.Period(UnitPeriod.Day, 1, DateTime.Today.AddDays(1));
			Assert.That(p.Add(1).Add(-1), Is.EqualTo(p));
			Assert.That(p.Add(1), Is.EqualTo(tom));
		}

		[Test]
		public void CouldAddPeriod()
		{
			Console.WriteLine(DateTimeOffset.UtcNow.Date);
			for (int step = 0; step < 1000; step++)
			{
				foreach (var i in unitPeriods) {
					var unitPeriod = (UnitPeriod)i;
					var p = new Period.Period(unitPeriod, 1, DateTimeOffset.UtcNow.Date);
					var next = p.Add(step);
					var diff = next.Diff(p);
					Assert.That(next.Previous, Is.EqualTo(p)); // failing
					Assert.That(p.Next, Is.EqualTo(next));
					Assert.That(next.Add(-step), Is.EqualTo(p));
					Assert.That(diff, Is.EqualTo(step));
				}
			}
		}
	}
}
