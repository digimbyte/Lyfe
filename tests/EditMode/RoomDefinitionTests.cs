using System;
using System.Collections.Generic;
using NUnit.Framework;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.Jobs;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class RoomDefinitionTests
    {
        [Test]
        public void HistoricCatalogContainsBroadRequestedTownSpaces()
        {
            List<RoomDefinition> catalog = HistoricRoomCatalog.CreateDefault();
            var required = new[]
            {
                "rooms.public.playground",
                "rooms.public.town_square",
                "rooms.education.school_classroom",
                "rooms.orphanage.dormitory",
                "rooms.lodging.hotel_room",
                "rooms.lodging.motel_room",
                "rooms.food.tavern_bar",
                "rooms.police.front_desk",
                "rooms.funerary.graveyard",
                "rooms.medical.morgue",
                "rooms.medical.hospital_ward",
                "rooms.care.retirement_common_room",
                "rooms.residential.mansion_great_hall"
            };

            Assert.That(catalog.Count, Is.GreaterThan(80));
            for (int i = 0; i < required.Length; i++)
                Assert.That(HistoricRoomCatalog.Find(catalog, new RoomDefinitionId(required[i])), Is.Not.Null, required[i]);
        }

        [Test]
        public void HistoricCatalogUsesUniqueStableIds()
        {
            List<RoomDefinition> catalog = HistoricRoomCatalog.CreateDefault();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < catalog.Count; i++)
                Assert.That(ids.Add(catalog[i].Id.Value), Is.True, catalog[i].Id.Value);
        }

        [Test]
        public void HistoricCatalogPassesContentValidationWithoutCreatingRooms()
        {
            var content = new AuthoredTownContent();
            content.RoomDefinitions.AddRange(HistoricRoomCatalog.CreateDefault());

            List<ContentValidationIssue> issues = content.Validate();

            for (int i = 0; i < issues.Count; i++)
                Assert.That(issues[i].Severity, Is.Not.EqualTo(ValidationSeverity.Error), issues[i].Message);
            Assert.That(content.BuildRuntime(new CapacityService()).Rooms, Is.Empty);
        }

        [Test]
        public void PlacedRoomRegistersAnchorsCapacityAndEmploymentPosition()
        {
            AuthoredTownContent content = CreatePoliceOffice();
            var capacity = new CapacityService();

            AuthoredTownRuntime runtime = content.BuildRuntime(capacity);

            var roomId = new RoomId("room.watchhouse.front_desk");
            RoomRuntimeInstance room = runtime.FindRoom(roomId);
            Assert.That(room, Is.Not.Null);
            Assert.That(room.FindAnchor(new AnchorId("anchor.watchhouse.desk")).LocalPosition,
                Is.EqualTo(new Float3(0.25f, 0.5f, 0.75f)));
            Assert.That(capacity.HasAvailability(
                new CapacityKey(new PlaceId("place.watchhouse.front_desk"), FacilityKind.ServiceCounter)), Is.True);
            Assert.That(runtime.RoleSlots.Count, Is.EqualTo(1));
            Assert.That(runtime.RoleSlots[0].Room, Is.EqualTo(roomId));
            Assert.That(runtime.RoleSlots[0].WorkAnchor, Is.EqualTo(new AnchorId("anchor.watchhouse.desk")));
        }

        [Test]
        public void OutdoorPublicRoomUsesAuthoredSiteGraph()
        {
            var definition = FindCatalog("rooms.public.town_square");
            var siteDefinition = new BuildingDefinition
            {
                Id = new BuildingDefinitionId("sites.public.square"),
                DisplayName = "Public Square",
                Kind = BuildingKind.PublicGrounds
            };
            var site = new BuildingInstance
            {
                Id = new BuildingId("site.square.central"),
                Definition = siteDefinition.Id
            };
            var room = new RoomInstance
            {
                Id = new RoomId("room.square.central"),
                Definition = definition.Id,
                Building = site.Id,
                Place = new PlaceId("place.square"),
                Scene = new RoomSceneBinding("scene.town", "rooms.outdoor", "central-square"),
                Access = RoomAccess.Public
            };
            var content = new AuthoredTownContent();
            content.BuildingDefinitions.Add(siteDefinition);
            content.BuildingInstances.Add(site);
            content.RoomDefinitions.Add(definition);
            content.RoomInstances.Add(room);
            content.BuildingGraphs.Add(new BuildingRoomGraph
            {
                Id = new BuildingGraphId("graph.square.central"),
                Building = site.Id,
                Rooms = { room.Id }
            });

            AuthoredTownRuntime runtime = content.BuildRuntime(new CapacityService());

            Assert.That(runtime.FindRoom(room.Id), Is.Not.Null);
            Assert.That(runtime.RoleSlots, Is.Empty);
        }

        [Test]
        public void MissingWorkAnchorFailsValidation()
        {
            AuthoredTownContent content = CreatePoliceOffice();
            content.RoomInstances[0].Positions[0].WorkAnchor = new AnchorId("anchor.missing");

            Assert.Throws<InvalidOperationException>(() => content.BuildRuntime(new CapacityService()));
        }

        [Test]
        public void LogicalRoomChangeInvalidatesObservedTransformCache()
        {
            var sim = new Sim("npc.constable", "Ada", "Brown");
            sim.Spatial.Room = new RoomId("room.watchhouse.front_desk");
            sim.Spatial.LastObservedPose = new LastObservedPose();

            sim.StartActivity(new ActivitySegment
            {
                Kind = ActivityKind.WorkTask,
                Room = new RoomId("room.watchhouse.office")
            }, 10d);

            Assert.That(sim.Spatial.LastObservedPose, Is.Null);
        }

        private static AuthoredTownContent CreatePoliceOffice()
        {
            var job = new JobDefinition
            {
                Id = new JobDefinitionId("jobs.police.clerk"),
                DisplayName = "Police Clerk",
                Category = JobCategory.Civic,
                BaseHourlyPay = 1.5f,
                RequiresFacility = true,
                WorkFacility = FacilityKind.ServiceCounter
            };
            var buildingDefinition = new BuildingDefinition
            {
                Id = new BuildingDefinitionId("buildings.watchhouse"),
                DisplayName = "Watch House",
                Kind = BuildingKind.Civic
            };
            var building = new BuildingInstance
            {
                Id = new BuildingId("building.watchhouse.01"),
                Definition = buildingDefinition.Id,
                Address = Address("place.watchhouse", "cell.watchhouse.entry", "anchor.watchhouse.entry")
            };
            var roomDefinition = FindCatalog("rooms.police.front_desk");
            var room = new RoomInstance
            {
                Id = new RoomId("room.watchhouse.front_desk"),
                Definition = roomDefinition.Id,
                Building = building.Id,
                Place = new PlaceId("place.watchhouse.front_desk"),
                Scene = new RoomSceneBinding("scene.watchhouse", "rooms.interior", "front-desk"),
                DefaultAnchor = new AnchorId("anchor.watchhouse.desk"),
                HasAccessOverride = true,
                Access = RoomAccess.Public
            };
            room.Anchors.Add(new RoomAnchorPlacement
            {
                Id = new AnchorId("anchor.watchhouse.desk"),
                Kind = RoomAnchorKind.Work,
                LocalPosition = new Float3(0.25f, 0.5f, 0.75f),
                Activity = ActivityKind.WorkTask
            });
            room.Facilities.Add(new RoomFacilityPlacement
            {
                LocalId = "public-counter",
                Kind = FacilityKind.ServiceCounter,
                Capacity = 1,
                Anchor = new AnchorId("anchor.watchhouse.desk")
            });
            room.Positions.Add(new BuildingRoleTemplate
            {
                LocalId = "clerk",
                Job = job.Id,
                Capacity = 1,
                WorkAnchor = new AnchorId("anchor.watchhouse.desk")
            });

            var content = new AuthoredTownContent();
            content.Jobs.Add(job);
            content.BuildingDefinitions.Add(buildingDefinition);
            content.BuildingInstances.Add(building);
            content.RoomDefinitions.Add(roomDefinition);
            content.RoomInstances.Add(room);
            content.BuildingGraphs.Add(new BuildingRoomGraph
            {
                Id = new BuildingGraphId("graph.watchhouse.01"),
                Building = building.Id,
                Rooms = { room.Id }
            });
            return content;
        }

        private static RoomDefinition FindCatalog(string id)
        {
            return HistoricRoomCatalog.Find(HistoricRoomCatalog.CreateDefault(), new RoomDefinitionId(id));
        }

        private static WorldAddress Address(string place, string cell, string anchor)
        {
            return new WorldAddress(
                new ChunkId("chunk.0"),
                new BlockId("block.0"),
                new PlaceId(place),
                new CellId(cell),
                new AnchorId(anchor));
        }
    }
}
