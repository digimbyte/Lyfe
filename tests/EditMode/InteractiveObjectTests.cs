using System;
using System.Collections.Generic;
using NUnit.Framework;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.Objects;
using Syms.Simulation.People;
using Syms.Simulation.Social;
using Syms.Simulation.Stats;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class InteractiveObjectTests
    {
        [Test]
        public void CouchCreatesIndependentMultiSeatAndEmergencySleepSlots()
        {
            InteractiveObjectDefinition couch = FindDefinition("objects.seating.couch");
            AuthoredTownContent content = CreateRoomContent(couch);
            content.ObjectInstances.Add(CreateObject("object.parlour.couch", couch.Id));
            var capacity = new CapacityService();

            AuthoredTownRuntime runtime = content.BuildRuntime(capacity);
            IReadOnlyList<ObjectAffordanceSlot> slots = runtime.Objects.GetSlots(new ObjectId("object.parlour.couch"));

            Assert.That(slots.Count, Is.EqualTo(4));
            Assert.That(Count(slots, "sit"), Is.EqualTo(3));
            Assert.That(Count(slots, "sleep"), Is.EqualTo(1));
            Assert.That(capacity.HasAvailability(new CapacityKey(new PlaceId("place.house.parlour"), FacilityKind.Seat)), Is.True);
            Assert.That(capacity.HasAvailability(new CapacityKey(new PlaceId("place.house.parlour"), FacilityKind.Bed)), Is.True);

            MaterializationTarget materialized;
            Assert.That(new AuthoredAnchorProvider(runtime).TryGetObjectAnchor(
                new ObjectId("object.parlour.couch"), out materialized), Is.True);
            Assert.That(materialized.Room, Is.EqualTo(new RoomId("room.house.parlour")));
            Assert.That(materialized.RoomLocalPosition, Is.EqualTo(new Float3(0.5f, 0.5f, 0.5f)));

            ObjectAffordanceSlot first;
            ObjectAffordanceSlot second;
            ObjectAffordanceSlot third;
            ObjectAffordanceSlot excess;
            Assert.That(runtime.Objects.TryReserve(new ObjectId("object.parlour.couch"), "sit", "npc.1", 0d, 10d, out first), Is.True);
            Assert.That(runtime.Objects.TryReserve(new ObjectId("object.parlour.couch"), "sit", "npc.2", 0d, 10d, out second), Is.True);
            Assert.That(runtime.Objects.TryReserve(new ObjectId("object.parlour.couch"), "sit", "npc.3", 0d, 10d, out third), Is.True);
            Assert.That(runtime.Objects.TryReserve(new ObjectId("object.parlour.couch"), "sit", "npc.4", 0d, 10d, out excess), Is.False);
            Assert.That(runtime.Objects.TryReserve(new ObjectId("object.parlour.couch"), "sleep", "npc.4", 0d, 10d, out excess), Is.True);
        }

        [Test]
        public void SleepingInAnotherHouseholdMembersBedFeelsWorseUntilDesperate()
        {
            InteractiveObjectDefinition bed = FindDefinition("objects.sleeping.single_bed");
            InteractiveObjectState state = State(bed, ObjectAccessPolicy.Household, OwnershipKind.Household, "household.1");
            state.AssignedSimIds.Add("npc.owner");
            Sim visitor = HousedSim("npc.visitor", "household.1");
            var context = new ObjectUseContext { Sim = visitor, Relationships = new RelationshipGraph() };
            var policy = new ObjectUsePolicy();

            visitor.Stats.Set(SimStat.Energy, 100f);
            ObjectUseEvaluation rested = policy.Evaluate(state, bed, bed.FindAffordance("sleep"), context);
            visitor.Stats.Set(SimStat.Energy, 0f);
            ObjectUseEvaluation desperate = policy.Evaluate(state, bed, bed.FindAffordance("sleep"), context);

            Assert.That(rested.Allowed, Is.True);
            Assert.That(desperate.Allowed, Is.True);
            Assert.That(rested.Reasons, Contains.Item("sleep.suboptimal"));
            Assert.That(desperate.Reasons, Contains.Item("sleep.desperate"));
            Assert.That(desperate.UtilityModifier, Is.GreaterThan(rested.UtilityModifier));
        }

        [Test]
        public void StrangerOnlyTrespassesIntoPrivateRestroomWhenNeedIsCritical()
        {
            InteractiveObjectDefinition privy = FindDefinition("objects.plumbing.private_privy");
            InteractiveObjectState state = State(privy, ObjectAccessPolicy.Household, OwnershipKind.Household, "household.owner");
            Sim stranger = HousedSim("npc.stranger", "household.other");
            var context = new ObjectUseContext { Sim = stranger, Relationships = new RelationshipGraph() };
            var policy = new ObjectUsePolicy();

            stranger.Stats.Set(SimStat.Bladder, 20f);
            ObjectUseEvaluation ordinary = policy.Evaluate(state, privy, privy.FindAffordance("toilet"), context);
            stranger.Stats.Set(SimStat.Bladder, 100f);
            ObjectUseEvaluation critical = policy.Evaluate(state, privy, privy.FindAffordance("toilet"), context);

            Assert.That(ordinary.Allowed, Is.False);
            Assert.That(critical.Allowed, Is.True);
            Assert.That(critical.Trespassing, Is.True);
        }

        [Test]
        public void PublicRestroomNeedsNoOwnershipClaim()
        {
            InteractiveObjectDefinition privy = FindDefinition("objects.plumbing.public_privy");
            InteractiveObjectState state = State(privy, ObjectAccessPolicy.Public, OwnershipKind.Public, string.Empty);
            Sim sim = HousedSim("npc.public", "household.1");

            ObjectUseEvaluation evaluation = new ObjectUsePolicy().Evaluate(
                state, privy, privy.FindAffordance("toilet"),
                new ObjectUseContext { Sim = sim, Relationships = new RelationshipGraph() });

            Assert.That(evaluation.Allowed, Is.True);
            Assert.That(evaluation.Trespassing, Is.False);
        }

        [Test]
        public void LockedFurnitureRequiresItsKeyOrOwner()
        {
            InteractiveObjectDefinition chest = FindDefinition("objects.storage.chest");
            InteractiveObjectState state = State(chest, ObjectAccessPolicy.Public, OwnershipKind.Sim, "npc.owner");
            state.Lock = new ObjectLock { State = LockState.Locked, KeyId = "chest.1" };
            Sim visitor = HousedSim("npc.visitor", "household.2");
            var context = new ObjectUseContext { Sim = visitor };
            var policy = new ObjectUsePolicy();

            ObjectUseEvaluation denied = policy.Evaluate(state, chest, chest.FindAffordance("store"), context);
            context.Claims.Add("key:chest.1");
            Assert.That(new ObjectLockService().TryApply(state, ObjectLockCommand.Unlock, context), Is.True);
            ObjectUseEvaluation allowed = policy.Evaluate(state, chest, chest.FindAffordance("store"), context);

            Assert.That(denied.Allowed, Is.False);
            Assert.That(allowed.Allowed, Is.True);
        }

        [Test]
        public void AssignedPartnerCanUseSharedBedWithoutStrangerPenalty()
        {
            InteractiveObjectDefinition bed = FindDefinition("objects.sleeping.shared_bed");
            InteractiveObjectState state = State(bed, ObjectAccessPolicy.HouseholdAndGuests, OwnershipKind.Sim, "npc.owner");
            state.AssignedSimIds.Add("npc.owner");
            Sim partner = HousedSim("npc.partner", "household.2");
            var relationships = new RelationshipGraph();
            relationships.SetPartners("npc.owner", partner.Id, 0d);
            var context = new ObjectUseContext { Sim = partner, Relationships = relationships };

            ObjectUseEvaluation evaluation = new ObjectUsePolicy()
                .Evaluate(state, bed, bed.FindAffordance("sleep"), context);

            Assert.That(evaluation.Allowed, Is.True);
            Assert.That(evaluation.Trespassing, Is.False);
            Assert.That(evaluation.UtilityModifier, Is.GreaterThan(10f));
        }

        [Test]
        public void JailBunkIsLessDesirableThanPaidHotelBed()
        {
            InteractiveObjectDefinition jail = FindDefinition("objects.sleeping.jail_bunk");
            InteractiveObjectDefinition hotel = FindDefinition("objects.sleeping.hotel_bed");
            Sim sim = HousedSim("npc.traveller", "household.1");
            sim.Stats.Set(SimStat.Energy, 50f);
            var context = new ObjectUseContext { Sim = sim, Relationships = new RelationshipGraph() };
            var policy = new ObjectUsePolicy();

            ObjectUseEvaluation jailUse = policy.Evaluate(
                State(jail, ObjectAccessPolicy.Public, OwnershipKind.Public, string.Empty),
                jail, jail.FindAffordance("sleep"), context);
            ObjectUseEvaluation hotelUse = policy.Evaluate(
                State(hotel, ObjectAccessPolicy.Public, OwnershipKind.Public, string.Empty),
                hotel, hotel.FindAffordance("sleep"), context);

            Assert.That(jailUse.UtilityModifier, Is.LessThan(hotelUse.UtilityModifier));
        }

        [Test]
        public void DoorStateChangesOnlyDoorOwnedTopologyBlockers()
        {
            var topology = new WorldTopology();
            var source = new CellId("cell.a");
            var destination = new CellId("cell.b");
            topology.AddOrReplaceCell(Cell(source));
            topology.AddOrReplaceCell(Cell(destination));
            var connection = new CellConnection(
                "connection.door",
                source,
                destination,
                CellExitDirection.PositiveX,
                new NormalizedCellPosition(1f, 0.5f, 0.5f),
                new NormalizedCellPosition(0f, 0.5f, 0.5f));
            connection.Portal = PortalKind.Door;
            connection.Blockers = ConnectionBlocker.Hazard;
            topology.AddConnection(connection);
            InteractiveObjectDefinition doorDefinition = FindDefinition("objects.portal.lockable_door");
            InteractiveObjectState door = State(doorDefinition, ObjectAccessPolicy.OwnerOnly, OwnershipKind.Sim, "npc.owner");
            door.Door = new DoorState();
            door.Lock = new ObjectLock { State = LockState.Locked, KeyId = "front-door" };
            door.Door.ConnectionIds.Add(connection.Id);
            Sim owner = HousedSim("npc.owner", "household.1");
            var context = new ObjectUseContext { Sim = owner };
            context.Claims.Add("key:front-door");
            var service = new DoorStateService();

            service.Synchronize(door, topology);
            Assert.That(connection.Blockers, Is.EqualTo(
                ConnectionBlocker.Hazard | ConnectionBlocker.Closed | ConnectionBlocker.Locked));
            Assert.That(service.TryApply(door, DoorCommand.Unlock, context, topology), Is.True);
            Assert.That(service.TryApply(door, DoorCommand.Open, context, topology), Is.True);

            Assert.That(connection.Blockers, Is.EqualTo(ConnectionBlocker.Hazard));
        }

        [Test]
        public void ClosedUnlockedDoorCanBeRoutedThroughButLockedDoorCannot()
        {
            var connection = new CellConnection(
                "connection.door", new CellId("cell.a"), new CellId("cell.b"), CellExitDirection.PositiveX,
                new NormalizedCellPosition(1f, 0.5f, 0.5f),
                new NormalizedCellPosition(0f, 0.5f, 0.5f));
            connection.Portal = PortalKind.Door;
            connection.Blockers = ConnectionBlocker.Closed;

            Assert.That(connection.CanTraverse(TravelMode.Walk), Is.True);

            connection.Blockers |= ConnectionBlocker.Locked;
            Assert.That(connection.CanTraverse(TravelMode.Walk), Is.False);
        }

        [Test]
        public void BrokenDoorStopsBlockingButPreservesHazard()
        {
            var topology = new WorldTopology();
            CellId source = new CellId("cell.a");
            CellId destination = new CellId("cell.b");
            topology.AddOrReplaceCell(Cell(source));
            topology.AddOrReplaceCell(Cell(destination));
            var connection = new CellConnection(
                "connection.door", source, destination, CellExitDirection.PositiveX,
                new NormalizedCellPosition(1f, 0.5f, 0.5f),
                new NormalizedCellPosition(0f, 0.5f, 0.5f));
            connection.Portal = PortalKind.Door;
            connection.Blockers = ConnectionBlocker.Hazard | ConnectionBlocker.Closed | ConnectionBlocker.Locked;
            topology.AddConnection(connection);
            InteractiveObjectDefinition definition = FindDefinition("objects.portal.lockable_door");
            InteractiveObjectState door = State(definition, ObjectAccessPolicy.Public, OwnershipKind.Public, string.Empty);
            door.Door = new DoorState();
            door.Lock = new ObjectLock { State = LockState.Locked };
            door.Door.ConnectionIds.Add(connection.Id);
            door.ApplyDamage(door.MaximumHealth);

            new DoorStateService().Synchronize(door, topology);

            Assert.That(connection.Blockers, Is.EqualTo(ConnectionBlocker.Hazard));
        }

        [Test]
        public void ObjectCatalogUsesUniqueStableIdsAndValidDefinitions()
        {
            List<InteractiveObjectDefinition> catalog = HistoricObjectCatalog.CreateDefault();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var content = new AuthoredTownContent();
            content.ObjectDefinitions.AddRange(catalog);

            for (int i = 0; i < catalog.Count; i++) Assert.That(ids.Add(catalog[i].Id.Value), Is.True);
            List<ContentValidationIssue> issues = content.Validate();
            for (int i = 0; i < issues.Count; i++)
                Assert.That(issues[i].Severity, Is.Not.EqualTo(ValidationSeverity.Error), issues[i].Message);
        }

        private static AuthoredTownContent CreateRoomContent(InteractiveObjectDefinition definition)
        {
            var buildingDefinition = new BuildingDefinition
            {
                Id = new BuildingDefinitionId("buildings.house"),
                DisplayName = "House",
                Kind = BuildingKind.Residence
            };
            var building = new BuildingInstance
            {
                Id = new BuildingId("building.house.1"),
                Definition = buildingDefinition.Id,
                Address = Address("place.house", "cell.parlour")
            };
            var roomDefinition = new RoomDefinition
            {
                Id = new RoomDefinitionId("rooms.test.parlour"),
                DisplayName = "Parlour",
                Category = RoomCategory.Residential
            };
            var room = new RoomInstance
            {
                Id = new RoomId("room.house.parlour"),
                Definition = roomDefinition.Id,
                Building = building.Id,
                Place = new PlaceId("place.house.parlour"),
                Scene = new RoomSceneBinding("scene.house", "rooms.interior", "parlour")
            };
            var content = new AuthoredTownContent();
            content.BuildingDefinitions.Add(buildingDefinition);
            content.BuildingInstances.Add(building);
            content.RoomDefinitions.Add(roomDefinition);
            content.RoomInstances.Add(room);
            content.BuildingGraphs.Add(new BuildingRoomGraph
            {
                Id = new BuildingGraphId("graph.house.1"),
                Building = building.Id,
                Rooms = { room.Id }
            });
            content.ObjectDefinitions.Add(definition);
            return content;
        }

        private static AuthoredObjectInstance CreateObject(string id, ObjectDefinitionId definition)
        {
            return new AuthoredObjectInstance
            {
                Id = new ObjectId(id),
                Definition = definition,
                Room = new RoomId("room.house.parlour"),
                Building = new BuildingId("building.house.1"),
                LocalPosition = new Float3(0.5f, 0.5f, 0.5f),
                Ownership = new ObjectOwnership
                {
                    Kind = OwnershipKind.Household,
                    OwnerId = "household.1",
                    Access = ObjectAccessPolicy.HouseholdAndGuests
                }
            };
        }

        private static InteractiveObjectState State(
            InteractiveObjectDefinition definition,
            ObjectAccessPolicy access,
            OwnershipKind ownership,
            string ownerId)
        {
            return new InteractiveObjectState
            {
                Id = new ObjectId("object.test"),
                Definition = definition.Id,
                MaximumHealth = definition.MaximumHealth,
                Health = definition.MaximumHealth,
                Cleanliness = 100f,
                Enabled = true,
                Ownership = new ObjectOwnership { Kind = ownership, OwnerId = ownerId, Access = access }
            };
        }

        private static Sim HousedSim(string id, string household)
        {
            var sim = new Sim(id, "Test", "Person");
            sim.Residence.Active = true;
            sim.Residence.HouseholdId = household;
            return sim;
        }

        private static InteractiveObjectDefinition FindDefinition(string id)
        {
            return HistoricObjectCatalog.Find(
                HistoricObjectCatalog.CreateDefault(), new ObjectDefinitionId(id));
        }

        private static int Count(IReadOnlyList<ObjectAffordanceSlot> slots, string affordance)
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].AffordanceLocalId == affordance) count++;
            return count;
        }

        private static TraversalCell Cell(CellId id)
        {
            return new TraversalCell(
                id, new ChunkId("chunk.0"), new BlockId("block.0"), new PlaceId("place.test"),
                new CellBounds(new Float3(0f, 0f, 0f), new Float3(1f, 1f, 1f)));
        }

        private static WorldAddress Address(string place, string cell)
        {
            return new WorldAddress(
                new ChunkId("chunk.0"), new BlockId("block.0"), new PlaceId(place),
                new CellId(cell), new AnchorId());
        }
    }
}
