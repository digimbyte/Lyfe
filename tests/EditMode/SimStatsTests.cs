using NUnit.Framework;
using Syms.Content;
using Syms.Simulation.Jobs;
using Syms.Simulation.People;
using Syms.Simulation.Stats;

namespace Syms.Tests.EditMode
{
    public sealed class SimStatsTests
    {
        [Test]
        public void BoundedStatIsClamped()
        {
            var stats = new SimStats();

            stats.Set(SimStat.Hunger, 120f);

            Assert.That(stats.Get(SimStat.Hunger), Is.EqualTo(100f));
        }

        [Test]
        public void MoneyCanBeNegative()
        {
            var stats = new SimStats();

            stats.Set(SimStat.Money, -50f);

            Assert.That(stats.Get(SimStat.Money), Is.EqualTo(-50f));
        }

        [Test]
        public void MissingResidenceIsAuthoritativeHomelessStatus()
        {
            var sim = new Sim("sim", "A", "B");

            Assert.That(sim.Residence.Status, Is.EqualTo(HousingStatus.Homeless));

            sim.Residence.Active = true;

            Assert.That(sim.Residence.Status, Is.EqualTo(HousingStatus.Housed));
        }

        [Test]
        public void HomelessUnemployedSimAccumulatesTheMostPovertyPressure()
        {
            var settings = new EconomicPressureSettings();
            var homelessUnemployed = new Sim("homeless.unemployed", "A", "B");
            var housedUnemployed = new Sim("housed.unemployed", "C", "D");
            housedUnemployed.Residence.Active = true;
            var homelessEmployed = new Sim("homeless.employed", "E", "F");
            homelessEmployed.Employment.Contract = new EmploymentContract
            {
                Id = "employment.test",
                Job = new JobDefinitionId("jobs.retail.clerk"),
                Active = true
            };

            EconomicPressureModel.Advance(homelessUnemployed, 86400d, settings);
            EconomicPressureModel.Advance(housedUnemployed, 86400d, settings);
            EconomicPressureModel.Advance(homelessEmployed, 86400d, settings);

            Assert.That(homelessUnemployed.Stats.Get(SimStat.Poverty),
                Is.GreaterThan(housedUnemployed.Stats.Get(SimStat.Poverty)));
            Assert.That(homelessUnemployed.Stats.Get(SimStat.Poverty),
                Is.GreaterThan(homelessEmployed.Stats.Get(SimStat.Poverty)));
            Assert.That(homelessUnemployed.Stats.Get(SimStat.Stress), Is.GreaterThan(0f));
            Assert.That(homelessUnemployed.Stats.Get(SimStat.Health), Is.LessThan(100f));
            Assert.That(homelessUnemployed.Stats.Get(SimStat.Money), Is.EqualTo(0f));
        }

        [Test]
        public void EconomicPressureIsInvariantToCatchUpStepSize()
        {
            var settings = new EconomicPressureSettings();
            var longStep = new Sim("long", "A", "B");
            var hourlySteps = new Sim("hourly", "C", "D");

            EconomicPressureModel.Advance(longStep, 86400d, settings);
            for (int hour = 0; hour < 24; hour++)
                EconomicPressureModel.Advance(hourlySteps, 3600d, settings);

            Assert.That(hourlySteps.Stats.Get(SimStat.Poverty),
                Is.EqualTo(longStep.Stats.Get(SimStat.Poverty)).Within(0.001f));
            Assert.That(hourlySteps.Stats.Get(SimStat.Stress),
                Is.EqualTo(longStep.Stats.Get(SimStat.Stress)).Within(0.001f));
            Assert.That(hourlySteps.Stats.Get(SimStat.Health),
                Is.EqualTo(longStep.Stats.Get(SimStat.Health)).Within(0.001f));
        }
    }
}
