using System;
using NUnit.Framework;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.City;
using Syms.Simulation.Jobs;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class AuthoredTownContentTests
    {
        [Test]
        public void PlacedBuildingCreatesOnlyItsAuthoredRoleSlotsAndFacilities()
        {
            AuthoredTownContent content = CreateSmithyContent(2);
            var capacity = new CapacityService();

            AuthoredTownRuntime runtime = content.BuildRuntime(capacity);

            Assert.That(runtime.RoleSlots.Count, Is.EqualTo(2));
            Assert.That(runtime.RoleSlots[0].Job, Is.EqualTo(new JobDefinitionId("jobs.trade.blacksmith")));
            Assert.That(capacity.HasAvailability(
                new CapacityKey(new PlaceId("places.smithy"), FacilityKind.Forge)), Is.True);
        }

        [Test]
        public void DefinitionWithoutPlacedBuildingCreatesNoJobs()
        {
            AuthoredTownContent content = CreateSmithyContent(1);
            content.BuildingInstances.Clear();

            AuthoredTownRuntime runtime = content.BuildRuntime(new CapacityService());

            Assert.That(runtime.RoleSlots, Is.Empty);
        }

        [Test]
        public void BuildingInstanceCanOverrideAuthoredRoleCapacity()
        {
            AuthoredTownContent content = CreateSmithyContent(2);
            content.BuildingInstances[0].RoleOverrides.Add(new CapacityOverride
            {
                LocalId = "smith",
                Capacity = 1
            });

            AuthoredTownRuntime runtime = content.BuildRuntime(new CapacityService());

            Assert.That(runtime.RoleSlots.Count, Is.EqualTo(1));
        }

        [Test]
        public void MissingJobDefinitionFailsValidation()
        {
            AuthoredTownContent content = CreateSmithyContent(1);
            content.Jobs.Clear();

            Assert.Throws<InvalidOperationException>(() => content.BuildRuntime(new CapacityService()));
        }

        [Test]
        public void CityAssignsContractFromBuildingRoleSlot()
        {
            var city = new CitySimulation("Town");
            city.InitializeAuthoredTown(CreateSmithyContent(1));
            var sim = new Sim("npc.smith", "Anne", "Smith");
            city.AddSim(sim);
            BuildingRoleSlot slot = city.TownRuntime.RoleSlots[0];

            EmploymentContract contract = city.AssignEmployment(sim.Id, slot.Id);

            Assert.That(contract.Job, Is.EqualTo(new JobDefinitionId("jobs.trade.blacksmith")));
            Assert.That(contract.Workplace, Is.EqualTo(new PlaceId("places.smithy")));
            Assert.That(slot.AssignedSimId, Is.EqualTo(sim.Id));
        }

        [Test]
        public void ContractScheduleProducesWorkCandidate()
        {
            var city = new CitySimulation("Town");
            city.InitializeAuthoredTown(CreateSmithyContent(1));
            var sim = new Sim("npc.smith", "Anne", "Smith");
            city.AddSim(sim);
            city.AssignEmployment(sim.Id, city.TownRuntime.RoleSlots[0].Id);
            double mondayAtEight = (24d + 8d) * 60d * 60d;

            var candidates = new BasicActivityCandidateSource().GetCandidates(sim, mondayAtEight);
            bool foundWork = false;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Kind == ActivityKind.WorkTask) foundWork = true;

            Assert.That(foundWork, Is.True);
        }

        private static AuthoredTownContent CreateSmithyContent(int roleCapacity)
        {
            var schedule = new WorkSchedule();
            schedule.Shifts.Add(new WorkShift(DayOfWeek.Monday, 6 * 60, 12 * 60));
            var job = new JobDefinition
            {
                Id = new JobDefinitionId("jobs.trade.blacksmith"),
                DisplayName = "Blacksmith",
                Category = JobCategory.Trade,
                BaseHourlyPay = 2f,
                DefaultSchedule = schedule
            };
            job.Duties.Add(new DutyDefinition { Id = "forge", DisplayName = "Forge goods" });

            var definition = new BuildingDefinition
            {
                Id = new BuildingDefinitionId("buildings.smithy"),
                DisplayName = "Smithy",
                Kind = BuildingKind.Workshop
            };
            definition.Roles.Add(new BuildingRoleTemplate
            {
                LocalId = "smith",
                Job = job.Id,
                Capacity = roleCapacity,
                HasFacilityOverride = true,
                RequiresFacility = true,
                WorkFacility = FacilityKind.Forge
            });
            definition.Facilities.Add(new FacilityTemplate
            {
                LocalId = "workbench",
                Kind = FacilityKind.Forge,
                Capacity = roleCapacity
            });

            var instance = new BuildingInstance
            {
                Id = new BuildingId("building.smithy.01"),
                Definition = definition.Id,
                Address = new WorldAddress(
                    new ChunkId("chunk.0"),
                    new BlockId("block.0"),
                    new PlaceId("places.smithy"),
                    new CellId("cells.smithy"),
                    new AnchorId("anchors.smithy.entry"))
            };

            var content = new AuthoredTownContent();
            content.Jobs.Add(job);
            content.BuildingDefinitions.Add(definition);
            content.BuildingInstances.Add(instance);
            return content;
        }
    }
}
