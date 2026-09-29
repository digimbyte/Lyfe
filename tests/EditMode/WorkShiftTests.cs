using System;
using NUnit.Framework;
using Syms.Simulation.Jobs;

namespace Syms.Tests.EditMode
{
    public sealed class WorkShiftTests
    {
        [Test]
        public void OvernightShiftContinuesIntoNextDay()
        {
            var shift = new WorkShift(DayOfWeek.Monday, 22 * 60, 8 * 60);

            Assert.That(shift.Contains(DayOfWeek.Tuesday, 2 * 60), Is.True);
            Assert.That(shift.Contains(DayOfWeek.Tuesday, 7 * 60), Is.False);
        }
    }
}

