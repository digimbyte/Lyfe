using NUnit.Framework;
using Syms.Simulation.Activities;
using Syms.Simulation.City;
using Syms.Simulation.People;
using Syms.Simulation.Travel;
using System.Collections.Generic;
using Syms.Simulation.Stats;
using Syms.Content;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class CitySimulationTests
    {
        [Test]
        public void LongGapResolvesDueSimOnceAtDestinationTime()
        {
            var city = new CitySimulation("Test");
            var sim = new Sim("sim", "A", "B");
            city.AddSim(sim);

            int processed = city.AdvanceTo(7d * 24d * 60d * 60d);

            Assert.That(processed, Is.EqualTo(1));
            Assert.That(sim.LastSimulatedAt, Is.EqualTo(city.CityTime));
            Assert.That(sim.Activity.Status, Is.EqualTo(ActivityStatus.Active));
            Assert.That(sim.NextWakeTime, Is.GreaterThan(city.CityTime));
        }

        [Test]
        public void DuplicateSimIdsAreRejected()
        {
            var city = new CitySimulation("Test");
            city.AddSim(new Sim("same", "A", "B"));

            Assert.Throws<System.InvalidOperationException>(() => city.AddSim(new Sim("same", "C", "D")));
        }

        [Test]
        public void CompletedActivityAppliesItsNeedEffect()
        {
            var city = new CitySimulation("Test");
            city.ConfigureRuntime(new DrinkSource(), new DeterministicActivityPlanner(),
                new TravelTimeCalculator(), new RouteStore(), new CapacityService());
            var sim = new Sim("sim", "A", "B");
            sim.Stats.Set(SimStat.Thirst, 100f);
            city.AddSim(sim);

            city.AdvanceTo(0d);
            city.AdvanceTo(300d);

            Assert.That(sim.Stats.Get(SimStat.Thirst), Is.LessThan(50f));
        }

        [Test]
        public void UnavailableFacilityIsFilteredBeforeUtilitySelection()
        {
            var city = new CitySimulation("Test");
            var sim = new Sim("sim", "A", "B");
            sim.Stats.Set(SimStat.Bladder, 100f);
            city.AddSim(sim);

            city.AdvanceTo(0d);

            Assert.That(sim.Activity.Kind, Is.Not.EqualTo(ActivityKind.Toilet));
        }

        [Test]
        public void OrdinaryAdvancementDoesNotChangeStablePopulationFacts()
        {
            var city = new CitySimulation("Test");
            var first = new Sim("first", "A", "B");
            first.Demographics.SetAge(30, 0.4d);
            first.Stats.Set(SimStat.Health, 0f);
            var second = new Sim("second", "C", "D");
            city.AddSim(first);
            city.AddSim(second);
            var home = new WorldAddress(
                new ChunkId("town"), new BlockId("block"), new PlaceId("home"),
                new CellId("home.cell"), new AnchorId("home.bed"));
            city.InitializeResidence(first.Id, "household", new BuildingId("home"), home);
            city.ApplyScriptedMarriage(first.Id, second.Id);

            city.AdvanceTo(50d * 365d * 86400d);

            Assert.That(first.Demographics.AgeYears, Is.EqualTo(30));
            Assert.That(first.Demographics.AgeProgress, Is.EqualTo(0.4d));
            Assert.That(first.Residence.HouseholdId, Is.EqualTo("household"));
            Assert.That(first.LifeStatus, Is.EqualTo(LifeStatus.Alive));
            Assert.That(second.LifeStatus, Is.EqualTo(LifeStatus.Alive));
            Assert.That(city.Sims.Count, Is.EqualTo(2));
            Assert.That(city.GetPartner(first.Id).Id, Is.EqualTo(second.Id));
            Assert.That(city.Relationships.Find(first.Id, second.Id)
                .GetFamilyRole(first.Id), Is.EqualTo(FamilyRole.Spouse));
        }

        [Test]
        public void ScriptedOrchestratorCanChangePopulationFactsExplicitly()
        {
            var city = new CitySimulation("Test");
            var first = new Sim("first", "A", "B");
            var second = new Sim("second", "C", "D");
            city.AddSim(first);
            city.AddSim(second);
            city.ApplyScriptedAgeChange(first.Id, 40);
            city.ApplyScriptedMarriage(first.Id, second.Id);

            bool divorced = city.ApplyScriptedDivorce(first.Id, second.Id);
            city.ApplyScriptedDeath(second.Id);
            city.ApplyScriptedBirth(new Sim("newborn", "E", "D"));

            Assert.That(first.Demographics.AgeYears, Is.EqualTo(40));
            Assert.That(divorced, Is.True);
            Assert.That(city.GetPartner(first.Id), Is.Null);
            Assert.That(city.Relationships.Find(first.Id, second.Id)
                .HasKind(RelationshipKind.ExPartner), Is.True);
            Assert.That(second.LifeStatus, Is.EqualTo(LifeStatus.Dead));
            Assert.That(city.FindSim("newborn"), Is.Not.Null);
        }

        private sealed class DrinkSource : IActivityCandidateSource
        {
            public IReadOnlyList<ActivityCandidate> GetCandidates(Sim sim, double cityTime)
            {
                return new[] { new ActivityCandidate { Kind = ActivityKind.Drink, DurationSeconds = 300d } };
            }
        }
    }
}
