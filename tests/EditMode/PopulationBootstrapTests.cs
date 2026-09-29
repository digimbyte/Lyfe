using System;
using NUnit.Framework;
using Syms.Content;
using Syms.Population;
using Syms.Simulation.Buildings;
using Syms.Simulation.City;
using Syms.Simulation.People;
using Syms.Simulation.Objects;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class PopulationBootstrapTests
    {
        [Test]
        public void IdenticalSeedAndTownProduceIdenticalPopulation()
        {
            PopulationBootstrapDefinition definition = Definition(8, true);

            PopulationBootstrapResult first = new PopulationBootstrapper().Build(definition, Town(2, 6, 4));
            PopulationBootstrapResult second = new PopulationBootstrapper().Build(definition, Town(2, 6, 4));

            Assert.That(first.Report.HasErrors, Is.False);
            Assert.That(first.State.Sims.Count, Is.EqualTo(8));
            Assert.That(second.State.Sims.Count, Is.EqualTo(first.State.Sims.Count));
            for (int i = 0; i < first.State.Sims.Count; i++)
            {
                Assert.That(second.State.Sims[i].Id, Is.EqualTo(first.State.Sims[i].Id));
                Assert.That(second.State.Sims[i].GivenName, Is.EqualTo(first.State.Sims[i].GivenName));
                Assert.That(second.State.Sims[i].FamilyName, Is.EqualTo(first.State.Sims[i].FamilyName));
                Assert.That(second.State.Sims[i].Demographics.AgeYears,
                    Is.EqualTo(first.State.Sims[i].Demographics.AgeYears));
            }
            for (int i = 0; i < first.State.EmploymentAssignments.Count; i++)
            {
                Assert.That(second.State.EmploymentAssignments[i].SimId,
                    Is.EqualTo(first.State.EmploymentAssignments[i].SimId));
                Assert.That(second.State.EmploymentAssignments[i].RoleSlot,
                    Is.EqualTo(first.State.EmploymentAssignments[i].RoleSlot));
            }
        }

        [Test]
        public void StableNameChoiceDoesNotDependOnPoolOrdering()
        {
            PopulationBootstrapDefinition firstDefinition = Definition(6, true);
            PopulationBootstrapDefinition secondDefinition = Definition(6, true);
            secondDefinition.Names.GivenNames.Reverse();
            secondDefinition.Names.FamilyNames.Reverse();

            PopulationBootstrapResult first = new PopulationBootstrapper().Build(firstDefinition, Town(1, 6, 0));
            PopulationBootstrapResult second = new PopulationBootstrapper().Build(secondDefinition, Town(1, 6, 0));

            for (int i = 0; i < first.State.Sims.Count; i++)
            {
                Assert.That(second.State.Sims[i].GivenName, Is.EqualTo(first.State.Sims[i].GivenName));
                Assert.That(second.State.Sims[i].FamilyName, Is.EqualTo(first.State.Sims[i].FamilyName));
            }
        }

        [Test]
        public void AuthoredNpcOverrideAndQuestRoleReservationWinBeforeGeneratedFilling()
        {
            PopulationBootstrapDefinition definition = Definition(4, true);
            definition.NpcTemplates.Add(new NpcTemplate
            {
                StableKey = "quest.blacksmith",
                HouseholdKey = "quest.household",
                ReservedResidence = new BuildingId("home.0"),
                GivenName = "Authored",
                HasAge = true,
                AgeYears = 42
            });
            definition.NpcOverrides.Add(new NpcOverride
            {
                TargetStableKey = "quest.blacksmith",
                FamilyName = "Override"
            });
            definition.QuestRoleReservations.Add(new QuestRoleReservation
            {
                NpcStableKey = "quest.blacksmith",
                RoleSlot = new RoleSlotId("work/role/0")
            });

            PopulationBootstrapResult result = new PopulationBootstrapper().Build(definition, Town(1, 4, 2));
            EmploymentAssignmentRecord employment = result.State.EmploymentAssignments[0];
            Sim authored = result.State.Sims[0];

            Assert.That(result.Report.HasErrors, Is.False);
            Assert.That(authored.GivenName, Is.EqualTo("Authored"));
            Assert.That(authored.FamilyName, Is.EqualTo("Override"));
            Assert.That(authored.Demographics.AgeYears, Is.EqualTo(42));
            Assert.That(employment.SimId, Is.EqualTo(authored.Id));
            Assert.That(employment.RoleSlot, Is.EqualTo(new RoleSlotId("work/role/0")));
        }

        [Test]
        public void PopulationBeyondAuthoredHousingBecomesHomelessWithoutInventingHomes()
        {
            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(5, true), Town(1, 2, 0));

            Assert.That(result.State.Households.Count, Is.EqualTo(1));
            Assert.That(result.Report.HousedPopulation, Is.EqualTo(2));
            Assert.That(result.Report.HomelessPopulation, Is.EqualTo(3));
            Assert.That(result.State.Sims[4].Residence.IsHomeless, Is.True);
        }

        [Test]
        public void PopulationDeterministicallyAssignsAuthoredBedsWithinResidence()
        {
            AuthoredTownRuntime town = Town(1, 2, 0);
            InteractiveObjectDefinition bed = HistoricObjectCatalog.Find(
                HistoricObjectCatalog.CreateDefault(), new ObjectDefinitionId("objects.sleeping.single_bed"));
            town.Objects.Definitions.Add(bed);
            town.Objects.Objects.Add(Bed("bed.1", bed.Id));
            town.Objects.Objects.Add(Bed("bed.2", bed.Id));
            town.Objects.RebuildSlots();

            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(2, true), town);

            Assert.That(result.Report.AssignedBeds, Is.EqualTo(2));
            Assert.That(result.Report.HousedResidentsWithoutAssignedBed, Is.EqualTo(0));
            Assert.That(town.Objects.Objects[0].AssignedSimIds.Count, Is.EqualTo(1));
            Assert.That(town.Objects.Objects[1].AssignedSimIds.Count, Is.EqualTo(1));
            Assert.That(town.Objects.Objects[0].AssignedSimIds[0],
                Is.LessThan(town.Objects.Objects[1].AssignedSimIds[0]));
        }

        [Test]
        public void PartneredHouseholdCanShareAuthoredDoubleBed()
        {
            AuthoredTownRuntime town = Town(1, 2, 0);
            InteractiveObjectDefinition bed = HistoricObjectCatalog.Find(
                HistoricObjectCatalog.CreateDefault(), new ObjectDefinitionId("objects.sleeping.shared_bed"));
            town.Objects.Definitions.Add(bed);
            town.Objects.Objects.Add(Bed("bed.shared", bed.Id));
            town.Objects.RebuildSlots();

            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(2, true), town);

            Assert.That(result.State.Relationships.Find(
                result.State.Sims[0].Id, result.State.Sims[1].Id).HasKind(RelationshipKind.Partner), Is.True);
            Assert.That(town.Objects.Objects[0].AssignedSimIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void DisabledHomelessnessLeavesTargetUnfilledAndReportsIt()
        {
            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(5, false), Town(1, 2, 0));

            Assert.That(result.Report.HasErrors, Is.False);
            Assert.That(result.State.Sims.Count, Is.EqualTo(2));
            Assert.That(result.Report.HomelessPopulation, Is.EqualTo(0));
            Assert.That(result.Report.Issues.Exists(issue =>
                issue.Code == "population.target.unfilled"), Is.True);
        }

        [Test]
        public void MissingRequiredQuestRoleFailsBootstrapValidation()
        {
            PopulationBootstrapDefinition definition = Definition(2, true);
            definition.NpcTemplates.Add(new NpcTemplate
            {
                StableKey = "quest.npc",
                GivenName = "Quest",
                FamilyName = "Npc",
                HasAge = true,
                AgeYears = 30
            });
            definition.QuestRoleReservations.Add(new QuestRoleReservation
            {
                NpcStableKey = "quest.npc",
                RoleSlot = new RoleSlotId("missing.role"),
                Required = true
            });

            PopulationBootstrapResult result = new PopulationBootstrapper().Build(definition, Town(1, 2, 0));

            Assert.That(result.Report.HasErrors, Is.True);
            Assert.That(result.Report.Issues.Exists(issue =>
                issue.Code == "population.role_reservation.unsatisfied"), Is.True);
        }

        [Test]
        public void SelectorsUseNormalizedIndexesForIdentityHouseholdWorkAndRelationships()
        {
            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(6, true), Town(1, 6, 3));
            var store = new PopulationStore(result.State);
            Sim sim = result.State.Sims[0];
            HouseholdRecord household = store.Select.HouseholdForSim(sim.Id);

            Assert.That(store.Select.SimById(new SimUid(sim.Id)), Is.SameAs(sim));
            Assert.That(household, Is.Not.Null);
            Assert.That(store.Select.HouseholdMembers(household.Id).Count, Is.EqualTo(6));
            Assert.That(store.Select.RelationshipsForSim(sim.Id).Count, Is.GreaterThan(0));
            EmploymentAssignmentRecord assigned = result.State.EmploymentAssignments[0];
            EmploymentAssignmentRecord employment = store.Select.EmploymentForSim(assigned.SimId);
            Assert.That(employment, Is.SameAs(assigned));
            Assert.That(store.Select.WorkplaceMembers(employment.Workplace).Count, Is.EqualTo(3));
        }

        [Test]
        public void CityImportsValidatedPopulationAndBuildingDerivedEmployment()
        {
            AuthoredTownRuntime town = Town(1, 4, 2);
            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(4, true), town);
            var city = new CitySimulation("Town") { TownRuntime = town };

            city.InitializePopulation(result);

            Assert.That(city.Sims.Count, Is.EqualTo(4));
            Assert.That(city.PopulationQueries.SimById(city.Sims[0].Id), Is.SameAs(city.Sims[0]));
            Assert.That(city.TownRuntime.RoleSlots[0].IsVacant, Is.False);
            Assert.That(city.Relationships.Relationships.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ScriptedStructuralChangeInvalidatesPopulationSelectors()
        {
            AuthoredTownRuntime town = Town(1, 3, 0);
            PopulationBootstrapResult result = new PopulationBootstrapper()
                .Build(Definition(3, true), town);
            var city = new CitySimulation("Town") { TownRuntime = town };
            city.InitializePopulation(result);
            Sim sim = city.Sims[0];
            Assert.That(city.PopulationQueries.HouseholdForSim(sim.Id), Is.Not.Null);

            city.ApplyScriptedHomelessness(sim.Id);

            Assert.That(city.PopulationQueries.HouseholdForSim(sim.Id), Is.Null);
            Assert.That(city.PopulationQueries.BlockResidents(new BlockId("block.0")).Count,
                Is.EqualTo(2));
        }

        [Test]
        public void ReducerRejectsDuplicateStableIds()
        {
            var store = new PopulationStore(new PopulationState());
            store.Dispatch(PopulationAction.Add(new Sim("sim_uid", "A", "B")));

            Assert.Throws<InvalidOperationException>(() =>
                store.Dispatch(PopulationAction.Add(new Sim("sim_uid", "C", "D"))));
        }

        private static PopulationBootstrapDefinition Definition(int target, bool allowHomeless)
        {
            var definition = new PopulationBootstrapDefinition
            {
                Seed = 8675309,
                TargetPopulation = target,
                AllowHomeless = allowHomeless,
                MinimumAdultAge = 18,
                MaximumAdultAge = 65,
                MaximumTraitsPerSim = 3
            };
            definition.Names.GivenNames.AddRange(new[] { "Ada", "Beatrice", "Clara", "Daniel" });
            definition.Names.FamilyNames.AddRange(new[] { "Baker", "Cooper", "Miller", "Smith" });
            return definition;
        }

        private static AuthoredTownRuntime Town(int residenceCount, int residentsPerResidence, int roleCount)
        {
            var town = new AuthoredTownRuntime();
            for (int residence = 0; residence < residenceCount; residence++)
            {
                var address = new WorldAddress(
                    new ChunkId("town"),
                    new BlockId("block." + residence),
                    new PlaceId("home." + residence),
                    new CellId("home." + residence + ".cell"),
                    new AnchorId("home." + residence + ".entry"));
                town.Residences.Add(new ResidenceInstance
                {
                    Building = new BuildingId("home." + residence),
                    Place = address.Place,
                    Address = address,
                    ResidentCapacity = residentsPerResidence
                });
            }
            for (int role = 0; role < roleCount; role++)
            {
                town.RoleSlots.Add(new BuildingRoleSlot
                {
                    Id = new RoleSlotId("work/role/" + role),
                    Building = new BuildingId("work"),
                    Workplace = new PlaceId("workplace"),
                    WorkAnchor = new AnchorId("work.anchor")
                });
            }
            return town;
        }

        private static InteractiveObjectState Bed(string id, ObjectDefinitionId definition)
        {
            return new InteractiveObjectState
            {
                Id = new ObjectId(id),
                Definition = definition,
                Building = new BuildingId("home.0"),
                MaximumHealth = 100f,
                Health = 100f,
                Cleanliness = 100f,
                Enabled = true,
                Ownership = new ObjectOwnership
                {
                    Kind = OwnershipKind.Building,
                    OwnerId = "home.0",
                    Access = ObjectAccessPolicy.Residents
                }
            };
        }
    }
}
