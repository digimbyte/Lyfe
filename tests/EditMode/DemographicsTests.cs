using NUnit.Framework;
using Syms.Simulation.People;

namespace Syms.Tests.EditMode
{
    public sealed class DemographicsTests
    {
        [Test]
        public void AuthoredAgeNormalizesFractionalProgress()
        {
            var demographics = new Demographics();

            demographics.SetAge(17, 1.25d);

            Assert.That(demographics.AgeYears, Is.EqualTo(18));
            Assert.That(demographics.AgeProgress, Is.EqualTo(0.25d).Within(0.000001d));
        }

        [Test]
        public void LifeStageThresholdsAreConfigurable()
        {
            var demographics = new Demographics { AgeYears = 60 };
            var thresholds = new LifeStageThresholds { ElderAge = 55 };

            Assert.That(demographics.GetLifeStage(thresholds), Is.EqualTo(LifeStage.Elder));
        }
    }
}
