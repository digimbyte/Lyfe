using NUnit.Framework;
using Syms.Content;
using Syms.Population;
using Syms.Simulation.Buildings;
using Syms.Simulation.City;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class CityScaleBenchmark
    {
        [Test, Explicit("Performance smoke test for development machines and CI benchmark jobs.")]
        public void AdvancesTenThousandDueSimsWithoutPerFrameUpdates()
        {
            var city = new CitySimulation("Scale Test");
            for (int i = 0; i < 10000; i++) city.AddSim(new Sim("sim." + i, "Sim", i.ToString()));

            int processed = city.AdvanceTo(3600d);

            Assert.That(processed, Is.EqualTo(10000));
        }

        [Test, Explicit("Performance smoke test for deterministic population bootstrap.")]
        public void BootstrapsTenThousandSimsWithSparseLocalRelationships()
        {
            var town = new AuthoredTownRuntime();
            for (int residence = 0; residence < 1000; residence++)
            {
                var address = new WorldAddress(
                    new ChunkId("town"), new BlockId("block." + residence),
                    new PlaceId("home." + residence), new CellId("cell." + residence),
                    new AnchorId("entry." + residence));
                town.Residences.Add(new ResidenceInstance
                {
                    Building = new BuildingId("home." + residence),
                    Place = address.Place,
                    Address = address,
                    ResidentCapacity = 10
                });
            }
            var definition = new PopulationBootstrapDefinition
            {
                Seed = 42,
                TargetPopulation = 10000,
                MaximumTraitsPerSim = 2
            };
            definition.Names.GivenNames.AddRange(new[] { "Anne", "Beth", "Charles", "David" });
            definition.Names.FamilyNames.AddRange(new[] { "Baker", "Miller", "Smith", "Taylor" });

            PopulationBootstrapResult result = new PopulationBootstrapper().Build(definition, town);

            Assert.That(result.Report.HasErrors, Is.False);
            Assert.That(result.State.Sims.Count, Is.EqualTo(10000));
            Assert.That(result.State.Relationships.Relationships.Count, Is.LessThan(100000));
        }
    }
}
