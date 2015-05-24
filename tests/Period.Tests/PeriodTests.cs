using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Period;

namespace QuikSharp.Tests {
    [TestFixture]
    public class PeriodTests {

        [Test]
        public void CouldCreatePeriod()
        {
            var p = new Period.Period(UnitPeriod.Day, 1, DateTime.Today.ToUniversalTime());
            var tom = new Period.Period(UnitPeriod.Day, 1, DateTime.Today.AddDays(1));
            Assert.That(p.Add(1).Add(-1), Is.EqualTo(p));
            Assert.That(p.Add(1), Is.EqualTo(tom));
        }

    }
}
