using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Jobs;
using Syms.Simulation.Objects;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    public enum ValidationSeverity { Info, Warning, Error }

    [Serializable]
    public sealed class ContentValidationIssue
    {
        public ValidationSeverity Severity;
        public string Code;
        public string EntityId;
        public string Message;
    }

    [Serializable]
    public sealed class AuthoredTownRuntime
    {
        public List<BuildingRoleSlot> RoleSlots = new List<BuildingRoleSlot>();
        public List<ResidenceInstance> Residences = new List<ResidenceInstance>();
        public OccupancyLeaseService OccupancyLeases = new OccupancyLeaseService();
        public DetentionService Detention = new DetentionService();
        public List<RoomRuntimeInstance> Rooms = new List<RoomRuntimeInstance>();
        public ObjectStateStore Objects = new ObjectStateStore();
        public BuildingGraphTopology BuildingGraphs = new BuildingGraphTopology();
        public GatewayStateService GatewayStates = new GatewayStateService();
        [NonSerialized] private Dictionary<RoomId, RoomRuntimeInstance> _roomsById;
        [NonSerialized] private Dictionary<string, RoomRuntimeInstance> _roomsBySceneObject;
        [NonSerialized] private Dictionary<string, List<RoomRuntimeInstance>> _roomsBySceneLayer;
        [NonSerialized] private int _indexedRoomCount = -1;

        public BuildingRoleSlot FindRoleSlot(RoleSlotId id)
        {
            for (int i = 0; i < RoleSlots.Count; i++)
                if (RoleSlots[i] != null && RoleSlots[i].Id == id) return RoleSlots[i];
            return null;
        }

        public RoomRuntimeInstance FindRoom(RoomId id)
        {
            EnsureRoomIndexes();
            RoomRuntimeInstance room;
            return _roomsById.TryGetValue(id, out room) ? room : null;
        }

        public RoomRuntimeInstance FindRoom(string sceneId, string sceneObjectId)
        {
            EnsureRoomIndexes();
            RoomRuntimeInstance room;
            return _roomsBySceneObject.TryGetValue(SceneObjectKey(sceneId, sceneObjectId), out room) ? room : null;
        }

        public IReadOnlyList<RoomRuntimeInstance> FindRoomsInScene(string sceneId, string cullingLayer)
        {
            EnsureRoomIndexes();
            List<RoomRuntimeInstance> rooms;
            return _roomsBySceneLayer.TryGetValue(SceneLayerKey(sceneId, cullingLayer), out rooms)
                ? rooms : Array.Empty<RoomRuntimeInstance>();
        }

        private void EnsureRoomIndexes()
        {
            if (_roomsById != null && _roomsBySceneObject != null && _roomsBySceneLayer != null
                && _indexedRoomCount == Rooms.Count) return;
            _roomsById = new Dictionary<RoomId, RoomRuntimeInstance>();
            _roomsBySceneObject = new Dictionary<string, RoomRuntimeInstance>(StringComparer.Ordinal);
            _roomsBySceneLayer = new Dictionary<string, List<RoomRuntimeInstance>>(StringComparer.Ordinal);
            for (int i = 0; i < Rooms.Count; i++)
            {
                RoomRuntimeInstance room = Rooms[i];
                if (room == null) continue;
                _roomsById[room.Id] = room;
                if (!room.Scene.IsDefined) continue;
                _roomsBySceneObject[SceneObjectKey(room.Scene.SceneId, room.Scene.SceneObjectId)] = room;
                string layerKey = SceneLayerKey(room.Scene.SceneId, room.Scene.CullingLayer);
                List<RoomRuntimeInstance> layerRooms;
                if (!_roomsBySceneLayer.TryGetValue(layerKey, out layerRooms))
                { layerRooms = new List<RoomRuntimeInstance>(); _roomsBySceneLayer.Add(layerKey, layerRooms); }
                layerRooms.Add(room);
            }
            _indexedRoomCount = Rooms.Count;
        }

        private static string SceneObjectKey(string sceneId, string sceneObjectId)
        { return (sceneId ?? string.Empty) + "\n" + (sceneObjectId ?? string.Empty); }

        private static string SceneLayerKey(string sceneId, string layer)
        { return (sceneId ?? string.Empty) + "\n" + (layer ?? string.Empty); }
    }

    /// <summary>
    /// Authored content only. Building instances come from the fixed map; this
    /// catalog never creates buildings or employment demand procedurally.
    /// </summary>
    [Serializable]
    public sealed class AuthoredTownContent
    {
        public List<JobDefinition> Jobs = new List<JobDefinition>();
        public List<BuildingDefinition> BuildingDefinitions = new List<BuildingDefinition>();
        public List<BuildingInstance> BuildingInstances = new List<BuildingInstance>();
        public List<RoomDefinition> RoomDefinitions = new List<RoomDefinition>();
        public List<RoomInstance> RoomInstances = new List<RoomInstance>();
        public List<BuildingRoomGraph> BuildingGraphs = new List<BuildingRoomGraph>();
        public List<InteractiveObjectDefinition> ObjectDefinitions = new List<InteractiveObjectDefinition>();
        public List<AuthoredObjectInstance> ObjectInstances = new List<AuthoredObjectInstance>();

        public JobDefinition FindJob(JobDefinitionId id)
        {
            for (int i = 0; i < Jobs.Count; i++)
                if (Jobs[i] != null && Jobs[i].Id == id) return Jobs[i];
            return null;
        }

        public BuildingDefinition FindBuildingDefinition(BuildingDefinitionId id)
        {
            for (int i = 0; i < BuildingDefinitions.Count; i++)
                if (BuildingDefinitions[i] != null && BuildingDefinitions[i].Id == id) return BuildingDefinitions[i];
            return null;
        }

        public BuildingInstance FindBuildingInstance(BuildingId id)
        {
            for (int i = 0; i < BuildingInstances.Count; i++)
                if (BuildingInstances[i] != null && BuildingInstances[i].Id == id) return BuildingInstances[i];
            return null;
        }

        public RoomDefinition FindRoomDefinition(RoomDefinitionId id)
        {
            for (int i = 0; i < RoomDefinitions.Count; i++)
                if (RoomDefinitions[i] != null && RoomDefinitions[i].Id == id) return RoomDefinitions[i];
            return null;
        }

        public InteractiveObjectDefinition FindObjectDefinition(ObjectDefinitionId id)
        {
            for (int i = 0; i < ObjectDefinitions.Count; i++)
                if (ObjectDefinitions[i] != null && ObjectDefinitions[i].Id == id) return ObjectDefinitions[i];
            return null;
        }

        public RoomInstance FindRoomInstance(RoomId id)
        {
            for (int i = 0; i < RoomInstances.Count; i++)
                if (RoomInstances[i] != null && RoomInstances[i].Id == id) return RoomInstances[i];
            return null;
        }

        public List<ContentValidationIssue> Validate()
        {
            var issues = new List<ContentValidationIssue>();
            ValidateUniqueJobIds(issues);
            ValidateUniqueBuildingDefinitionIds(issues);
            ValidateUniqueBuildingInstanceIds(issues);
            ValidateUniqueRoomDefinitionIds(issues);
            ValidateUniqueRoomInstanceIds(issues);
            ValidateUniqueRoomAnchorIds(issues);
            ValidateBuildingHierarchy(issues);
            ValidateUniqueObjectDefinitionIds(issues);
            ValidateUniqueObjectInstanceIds(issues);
            ValidateBuildingGraphs(issues);

            for (int i = 0; i < BuildingDefinitions.Count; i++)
                if (BuildingDefinitions[i] != null) ValidateBuildingDefinition(BuildingDefinitions[i], issues);

            for (int i = 0; i < BuildingInstances.Count; i++)
            {
                BuildingInstance instance = BuildingInstances[i];
                if (instance == null) continue;
                BuildingDefinition definition = FindBuildingDefinition(instance.Definition);
                if (definition == null)
                {
                    AddIssue(issues, ValidationSeverity.Error, "building.definition.missing", instance.Id.Value,
                        "Building instance references a missing building definition.");
                    continue;
                }

                ValidateBuildingInstance(instance, issues);
            }

            for (int i = 0; i < RoomDefinitions.Count; i++)
                if (RoomDefinitions[i] != null) ValidateRoomDefinition(RoomDefinitions[i], issues);

            for (int i = 0; i < RoomInstances.Count; i++)
                if (RoomInstances[i] != null) ValidateRoomInstance(RoomInstances[i], issues);

            for (int i = 0; i < ObjectDefinitions.Count; i++)
                if (ObjectDefinitions[i] != null) ValidateObjectDefinition(ObjectDefinitions[i], issues);

            for (int i = 0; i < ObjectInstances.Count; i++)
                if (ObjectInstances[i] != null) ValidateObjectInstance(ObjectInstances[i], issues);

            return issues;
        }

        public AuthoredTownRuntime BuildRuntime(ICapacityService capacityService)
        {
            if (capacityService == null) throw new ArgumentNullException(nameof(capacityService));
            List<ContentValidationIssue> issues = Validate();
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == ValidationSeverity.Error)
                    throw new InvalidOperationException("Authored town content is invalid: " + issues[i].Message);

            var runtime = new AuthoredTownRuntime();
            runtime.BuildingGraphs.Graphs.AddRange(BuildingGraphs);
            runtime.BuildingGraphs.RebuildIndexes();
            var facilityTotals = new Dictionary<CapacityKey, int>();
            for (int i = 0; i < BuildingInstances.Count; i++)
            {
                BuildingInstance instance = BuildingInstances[i];
                if (instance == null || !instance.Enabled) continue;
                BuildingDefinition definition = FindBuildingDefinition(instance.Definition);
                AddFacilities(instance, definition, facilityTotals);
                AddRoleSlots(instance, definition, runtime);
            }

            for (int i = 0; i < RoomInstances.Count; i++)
            {
                RoomInstance room = RoomInstances[i];
                if (room == null || !room.Enabled) continue;
                AddRoom(room, FindRoomDefinition(room.Definition), runtime);
                AddRoomFacilities(room, facilityTotals);
                AddRoomRoleSlots(room, runtime);
            }

            runtime.Objects.Definitions.AddRange(ObjectDefinitions);
            for (int i = 0; i < ObjectInstances.Count; i++)
            {
                AuthoredObjectInstance value = ObjectInstances[i];
                if (value == null || !value.Enabled) continue;
                InteractiveObjectDefinition definition = FindObjectDefinition(value.Definition);
                AddObject(value, definition, runtime, facilityTotals);
            }
            runtime.Objects.RebuildSlots();

            for (int i = 0; i < BuildingInstances.Count; i++)
            {
                BuildingInstance instance = BuildingInstances[i];
                if (instance != null && instance.Enabled) AddResidence(instance, runtime);
            }
            runtime.OccupancyLeases.Configure(runtime.Objects);
            runtime.Detention.Configure(BuildingInstances, runtime.Objects);

            foreach (KeyValuePair<CapacityKey, int> pair in facilityTotals)
                capacityService.SetCapacity(pair.Key, pair.Value);
            return runtime;
        }

        private static void AddObject(
            AuthoredObjectInstance authored,
            InteractiveObjectDefinition definition,
            AuthoredTownRuntime runtime,
            Dictionary<CapacityKey, int> facilityTotals)
        {
            RoomRuntimeInstance room = runtime.FindRoom(authored.Room);
            var state = new InteractiveObjectState
            {
                Id = authored.Id,
                Definition = authored.Definition,
                Room = authored.Room,
                Building = authored.Building,
                Place = room == null ? new PlaceId() : room.Place,
                Anchor = authored.Anchor,
                LocalPosition = authored.LocalPosition,
                Ownership = CloneOwnership(authored.Ownership),
                AssignedSimIds = new List<string>(authored.AssignedSimIds),
                Lock = CloneLock(authored.Lock),
                Door = CloneDoor(authored.Door),
                MaximumHealth = definition.MaximumHealth,
                Health = authored.InitialHealth < 0f ? definition.MaximumHealth : authored.InitialHealth,
                Cleanliness = authored.InitialCleanliness < 0f
                    ? definition.DefaultCleanliness
                    : authored.InitialCleanliness,
                Enabled = authored.Enabled,
                Tags = new List<string>(authored.Tags)
            };
            if (state.Health <= 0f) state.Conditions |= ObjectConditionFlags.Broken;
            if (state.Cleanliness < 40f) state.Conditions |= ObjectConditionFlags.Dirty;
            runtime.Objects.Objects.Add(state);

            for (int i = 0; i < definition.Affordances.Count; i++)
            {
                ObjectAffordanceDefinition affordance = definition.Affordances[i];
                if (affordance == null || affordance.SlotCount <= 0
                    || affordance.Facility == FacilityKind.Generic) continue;
                var key = new CapacityKey(room == null ? new PlaceId() : room.Place, affordance.Facility);
                int current;
                facilityTotals.TryGetValue(key, out current);
                facilityTotals[key] = current + affordance.SlotCount;
            }
        }

        private static ObjectOwnership CloneOwnership(ObjectOwnership source)
        {
            return source == null ? new ObjectOwnership() : new ObjectOwnership
            {
                Kind = source.Kind,
                OwnerId = source.OwnerId,
                Access = source.Access
            };
        }

        private static DoorState CloneDoor(DoorState source)
        {
            if (source == null) return null;
            var clone = new DoorState
            {
                IsOpen = source.IsOpen
            };
            clone.ConnectionIds.AddRange(source.ConnectionIds);
            clone.GatewayIds.AddRange(source.GatewayIds);
            return clone;
        }

        private static ObjectLock CloneLock(ObjectLock source)
        {
            return source == null ? null : new ObjectLock { State = source.State, KeyId = source.KeyId };
        }

        private static void AddRoom(
            RoomInstance room,
            RoomDefinition definition,
            AuthoredTownRuntime runtime)
        {
            runtime.Rooms.Add(new RoomRuntimeInstance
            {
                Id = room.Id,
                Definition = room.Definition,
                Building = room.Building,
                Place = room.Place,
                Scene = room.Scene,
                DefaultAnchor = room.DefaultAnchor,
                Access = room.HasAccessOverride ? room.Access : definition.DefaultAccess,
                Anchors = new List<RoomAnchorPlacement>(room.Anchors),
                Tags = new List<string>(room.Tags)
            });
        }

        private static void AddRoomFacilities(RoomInstance room, Dictionary<CapacityKey, int> totals)
        {
            for (int i = 0; i < room.Facilities.Count; i++)
            {
                RoomFacilityPlacement facility = room.Facilities[i];
                if (facility == null) continue;
                var key = new CapacityKey(room.Place, facility.Kind);
                int current;
                totals.TryGetValue(key, out current);
                totals[key] = current + Math.Max(0, facility.Capacity);
            }
        }

        private void AddRoomRoleSlots(RoomInstance room, AuthoredTownRuntime runtime)
        {
            for (int positionIndex = 0; positionIndex < room.Positions.Count; positionIndex++)
            {
                BuildingRoleTemplate position = room.Positions[positionIndex];
                if (position == null) continue;
                JobDefinition job = FindJob(position.Job);
                for (int slotIndex = 0; slotIndex < Math.Max(0, position.Capacity); slotIndex++)
                {
                    string localId = string.IsNullOrEmpty(position.LocalId)
                        ? "position." + positionIndex
                        : position.LocalId;
                    runtime.RoleSlots.Add(new BuildingRoleSlot
                    {
                        Id = new RoleSlotId(room.Id.Value + "/positions/" + localId + "/" + slotIndex),
                        Building = room.Building,
                        Job = position.Job,
                        Workplace = room.Place,
                        Room = room.Id,
                        WorkAnchor = position.WorkAnchor,
                        Schedule = CloneSchedule(position.ScheduleOverride ?? job.DefaultSchedule),
                        HourlyPay = position.HasHourlyPayOverride ? position.HourlyPayOverride : job.BaseHourlyPay,
                        RequiresFacility = position.HasFacilityOverride ? position.RequiresFacility : job.RequiresFacility,
                        WorkFacility = position.HasFacilityOverride ? position.WorkFacility : job.WorkFacility,
                        Tags = new List<string>(position.Tags)
                    });
                }
            }
        }

        private void AddRoleSlots(BuildingInstance instance, BuildingDefinition definition, AuthoredTownRuntime runtime)
        {
            for (int roleIndex = 0; roleIndex < definition.Roles.Count; roleIndex++)
            {
                BuildingRoleTemplate role = definition.Roles[roleIndex];
                if (role == null) continue;
                JobDefinition job = FindJob(role.Job);
                int count = instance.GetRoleCapacity(role);
                for (int slotIndex = 0; slotIndex < count; slotIndex++)
                {
                    string localId = string.IsNullOrEmpty(role.LocalId) ? "role." + roleIndex : role.LocalId;
                    runtime.RoleSlots.Add(new BuildingRoleSlot
                    {
                        Id = new RoleSlotId(instance.Id.Value + "/roles/" + localId + "/" + slotIndex),
                        Building = instance.Id,
                        Job = role.Job,
                        Workplace = instance.Address.Place,
                        WorkAnchor = role.WorkAnchor,
                        Schedule = CloneSchedule(role.ScheduleOverride ?? job.DefaultSchedule),
                        HourlyPay = role.HasHourlyPayOverride ? role.HourlyPayOverride : job.BaseHourlyPay,
                        RequiresFacility = role.HasFacilityOverride ? role.RequiresFacility : job.RequiresFacility,
                        WorkFacility = role.HasFacilityOverride ? role.WorkFacility : job.WorkFacility,
                        Tags = new List<string>(role.Tags)
                    });
                }
            }
        }

        private static void AddFacilities(
            BuildingInstance instance,
            BuildingDefinition definition,
            Dictionary<CapacityKey, int> totals)
        {
            for (int i = 0; i < definition.Facilities.Count; i++)
            {
                FacilityTemplate facility = definition.Facilities[i];
                if (facility == null) continue;
                var key = new CapacityKey(instance.Address.Place, facility.Kind);
                int current;
                totals.TryGetValue(key, out current);
                totals[key] = current + instance.GetFacilityCapacity(facility);
            }
        }

        private static void AddResidence(BuildingInstance instance, AuthoredTownRuntime runtime)
        {
            if (!instance.IsResidence) return;
            var residence = new ResidenceInstance
            {
                Building = instance.Id,
                ManagedBy = instance.IsManagedSubBuilding ? instance.ParentBuilding : instance.Id,
                Place = instance.Address.Place,
                Address = instance.Address,
                Occupancy = instance.Occupancy,
                ResidentCapacity = CountSleepSlots(runtime.Objects, instance.Id, instance.Occupancy),
                RatePerDay = Math.Max(0f, instance.RatePerDay),
                PopulationEligible = instance.PopulationEligible
            };
            for (int i = 0; i < runtime.Rooms.Count; i++)
                if (runtime.Rooms[i] != null && runtime.Rooms[i].Building == instance.Id)
                    residence.Rooms.Add(runtime.Rooms[i].Id);
            runtime.Residences.Add(residence);
            runtime.OccupancyLeases.Residences.Add(residence);
        }

        private static int CountSleepSlots(ObjectStateStore objects, BuildingId building,
            BuildingOccupancyKind occupancy)
        {
            int count = 0;
            for (int objectIndex = 0; objectIndex < objects.Objects.Count; objectIndex++)
            {
                InteractiveObjectState value = objects.Objects[objectIndex];
                if (value == null || !value.Enabled || value.Building != building) continue;
                InteractiveObjectDefinition definition = objects.FindDefinition(value.Definition);
                if (definition == null) continue;
                for (int affordanceIndex = 0; affordanceIndex < definition.Affordances.Count; affordanceIndex++)
                {
                    ObjectAffordanceDefinition affordance = definition.Affordances[affordanceIndex];
                    if (affordance != null && IsAuthoritativeBed(affordance.Sleeping, occupancy))
                        count += Math.Max(0, affordance.SlotCount);
                }
            }
            return count;
        }

        private static bool IsAuthoritativeBed(SleepingArrangement arrangement,
            BuildingOccupancyKind occupancy)
        {
            if (occupancy == BuildingOccupancyKind.LodgingResidence)
                return arrangement == SleepingArrangement.Hotel;
            if (occupancy == BuildingOccupancyKind.DetentionCell)
                return arrangement == SleepingArrangement.Jail;
            if (occupancy == BuildingOccupancyKind.InstitutionalResidence)
                return arrangement == SleepingArrangement.Institutional
                    || arrangement == SleepingArrangement.Bunk
                    || arrangement == SleepingArrangement.PrivateSingle;
            return arrangement == SleepingArrangement.PrivateSingle
                || arrangement == SleepingArrangement.SharedBed
                || arrangement == SleepingArrangement.Bunk;
        }

        private void ValidateBuildingInstance(
            BuildingInstance instance,
            List<ContentValidationIssue> issues)
        {
            if (instance.RatePerDay < 0f || float.IsNaN(instance.RatePerDay)
                || float.IsInfinity(instance.RatePerDay))
                AddIssue(issues, ValidationSeverity.Error, "building.occupancy.rate", instance.Id.Value,
                    "Building occupancy rate must be finite and non-negative.");
            for (int i = 0; i < instance.RoleOverrides.Count; i++)
            {
                CapacityOverride roleOverride = instance.RoleOverrides[i];
                if (roleOverride != null && roleOverride.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "building.role_override.capacity_invalid", instance.Id.Value,
                        "Role capacity overrides cannot be negative.");
            }
            for (int i = 0; i < instance.FacilityOverrides.Count; i++)
            {
                CapacityOverride facilityOverride = instance.FacilityOverrides[i];
                if (facilityOverride != null && facilityOverride.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "building.facility_override.capacity_invalid", instance.Id.Value,
                        "Facility capacity overrides cannot be negative.");
            }
        }

        private void ValidateBuildingDefinition(BuildingDefinition definition, List<ContentValidationIssue> issues)
        {
            var localRoleIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Roles.Count; i++)
            {
                BuildingRoleTemplate role = definition.Roles[i];
                if (role == null) continue;
                if (FindJob(role.Job) == null)
                    AddIssue(issues, ValidationSeverity.Error, "building.role.job_missing", definition.Id.Value,
                        "Role '" + role.LocalId + "' references a missing job definition.");
                JobDefinition roleJob = FindJob(role.Job);
                bool requiresFacility = role.HasFacilityOverride
                    ? role.RequiresFacility
                    : roleJob != null && roleJob.RequiresFacility;
                FacilityKind workFacility = role.HasFacilityOverride
                    ? role.WorkFacility
                    : (roleJob == null ? FacilityKind.Generic : roleJob.WorkFacility);
                if (requiresFacility && !HasFacility(definition, workFacility))
                    AddIssue(issues, ValidationSeverity.Error, "building.role.facility_missing", definition.Id.Value,
                        "Role '" + role.LocalId + "' requires a facility not declared by the building.");
                if (string.IsNullOrEmpty(role.LocalId) || !localRoleIds.Add(role.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "building.role.id_invalid", definition.Id.Value,
                        "Building role local IDs must be non-empty and unique within the definition.");
                if (role.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "building.role.capacity_invalid", definition.Id.Value,
                        "Role capacity cannot be negative.");
            }

            var localFacilityIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Facilities.Count; i++)
            {
                FacilityTemplate facility = definition.Facilities[i];
                if (facility == null) continue;
                if (string.IsNullOrEmpty(facility.LocalId) || !localFacilityIds.Add(facility.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "building.facility.id_invalid", definition.Id.Value,
                        "Facility local IDs must be non-empty and unique within the definition.");
                if (facility.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "building.facility.capacity_invalid", definition.Id.Value,
                        "Facility capacity cannot be negative.");
            }
        }

        private void ValidateRoomDefinition(RoomDefinition definition, List<ContentValidationIssue> issues)
        {
            var facilityIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.SuggestedFacilities.Count; i++)
            {
                RoomFacilityDefinition facility = definition.SuggestedFacilities[i];
                if (facility == null) continue;
                if (string.IsNullOrEmpty(facility.LocalId) || !facilityIds.Add(facility.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "room_definition.facility.id_invalid", definition.Id.Value,
                        "Suggested facility local IDs must be non-empty and unique within the room definition.");
                if (facility.SuggestedCapacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "room_definition.facility.capacity_invalid", definition.Id.Value,
                        "Suggested facility capacity cannot be negative.");
            }
        }

        private void ValidateRoomInstance(RoomInstance room, List<ContentValidationIssue> issues)
        {
            if (FindRoomDefinition(room.Definition) == null)
                AddIssue(issues, ValidationSeverity.Error, "room.definition.missing", room.Id.Value,
                    "Room instance references a missing room definition.");

            BuildingInstance building = FindBuildingInstance(room.Building);
            if (building == null)
                AddIssue(issues, ValidationSeverity.Error, "room.building.missing", room.Id.Value,
                    "Every indoor or outdoor room requires an authored root/site or direct child building.");
            if (building != null && !building.Enabled && room.Enabled)
                AddIssue(issues, ValidationSeverity.Error, "room.building.disabled", room.Id.Value,
                    "An enabled room cannot belong to a disabled building instance.");
            if (!HasGraphRoom(room))
                AddIssue(issues, ValidationSeverity.Error, "room.graph.missing", room.Id.Value,
                    "Every room must be a node in its root property's baked room graph.");

            if (string.IsNullOrEmpty(room.Place.Value))
                AddIssue(issues, ValidationSeverity.Error, "room.place.missing", room.Id.Value,
                    "A placed room requires a stable place ID.");
            ValidateRoomAnchors(room, issues);
            ValidateRoomFacilities(room, issues);
            ValidateRoomPositions(room, issues);
            if (!room.Scene.IsDefined || string.IsNullOrEmpty(room.Scene.CullingLayer))
                AddIssue(issues, ValidationSeverity.Error, "room.scene_binding.required", room.Id.Value,
                    "Every room must be generated from a scene object on a named culling layer.");
            if (room.Scene.IsDefined && HasDuplicateSceneRoom(room))
                AddIssue(issues, ValidationSeverity.Error, "room.scene_binding.duplicate", room.Id.Value,
                    "Scene ID and scene object ID must identify exactly one authored room.");
            if (!string.IsNullOrEmpty(room.DefaultAnchor.Value) && !HasRoomAnchor(room, room.DefaultAnchor))
                AddIssue(issues, ValidationSeverity.Error, "room.address_anchor.missing", room.Id.Value,
                    "The room default anchor must be one of the room's authored anchors.");
        }

        private bool HasGraphRoom(RoomInstance room)
        {
            for (int i = 0; i < BuildingGraphs.Count; i++)
            {
                BuildingRoomGraph graph = BuildingGraphs[i];
                if (graph == null) continue;
                bool member = graph.Building == room.Building;
                for (int b = 0; b < graph.MemberBuildings.Count && !member; b++)
                    member = graph.MemberBuildings[b] == room.Building;
                if (!member) continue;
                for (int r = 0; r < graph.Rooms.Count; r++)
                    if (graph.Rooms[r] == room.Id) return true;
            }
            return false;
        }

        private static void ValidateRoomAnchors(
            RoomInstance room,
            List<ContentValidationIssue> issues)
        {
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < room.Anchors.Count; i++)
            {
                RoomAnchorPlacement anchor = room.Anchors[i];
                if (anchor == null) continue;
                if (string.IsNullOrEmpty(anchor.Id.Value) || !anchorIds.Add(anchor.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "room.anchor.id_invalid", room.Id.Value,
                        "Room anchor IDs must be non-empty and unique within the room.");
                if (!IsFinite(anchor.LocalPosition.X) || !IsFinite(anchor.LocalPosition.Y)
                    || !IsFinite(anchor.LocalPosition.Z))
                    AddIssue(issues, ValidationSeverity.Error, "room.anchor.position_invalid", room.Id.Value,
                        "Room-local anchor coordinates must be finite.");
                if (anchor.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "room.anchor.capacity_invalid", room.Id.Value,
                        "Room anchor capacity cannot be negative.");
            }
        }

        private static void ValidateRoomFacilities(
            RoomInstance room,
            List<ContentValidationIssue> issues)
        {
            var localIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < room.Facilities.Count; i++)
            {
                RoomFacilityPlacement facility = room.Facilities[i];
                if (facility == null) continue;
                if (string.IsNullOrEmpty(facility.LocalId) || !localIds.Add(facility.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "room.facility.id_invalid", room.Id.Value,
                        "Room facility local IDs must be non-empty and unique within the room.");
                if (facility.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "room.facility.capacity_invalid", room.Id.Value,
                        "Room facility capacity cannot be negative.");
                if (!string.IsNullOrEmpty(facility.Anchor.Value) && !HasRoomAnchor(room, facility.Anchor))
                    AddIssue(issues, ValidationSeverity.Error, "room.facility.anchor_missing", room.Id.Value,
                        "A room facility anchor must be one of the room's authored anchors.");
            }
        }

        private void ValidateRoomPositions(
            RoomInstance room,
            List<ContentValidationIssue> issues)
        {
            var localIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < room.Positions.Count; i++)
            {
                BuildingRoleTemplate position = room.Positions[i];
                if (position == null) continue;
                if (string.IsNullOrEmpty(position.LocalId) || !localIds.Add(position.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "room.position.id_invalid", room.Id.Value,
                        "Room position local IDs must be non-empty and unique within the room.");
                JobDefinition job = FindJob(position.Job);
                if (job == null)
                    AddIssue(issues, ValidationSeverity.Error, "room.position.job_missing", room.Id.Value,
                        "Room position '" + position.LocalId + "' references a missing job definition.");
                if (position.Capacity < 0)
                    AddIssue(issues, ValidationSeverity.Error, "room.position.capacity_invalid", room.Id.Value,
                        "Room position capacity cannot be negative.");
                if (!string.IsNullOrEmpty(position.WorkAnchor.Value) && !HasRoomAnchor(room, position.WorkAnchor))
                    AddIssue(issues, ValidationSeverity.Error, "room.position.anchor_missing", room.Id.Value,
                        "A room position work anchor must be one of the room's authored anchors.");
                bool requiresFacility = position.HasFacilityOverride
                    ? position.RequiresFacility
                    : job != null && job.RequiresFacility;
                FacilityKind kind = position.HasFacilityOverride
                    ? position.WorkFacility
                    : (job == null ? FacilityKind.Generic : job.WorkFacility);
                if (requiresFacility && !HasRoomFacility(room, kind))
                    AddIssue(issues, ValidationSeverity.Error, "room.position.facility_missing", room.Id.Value,
                        "Room position '" + position.LocalId + "' requires a facility not placed in the room.");
            }
        }

        private static bool HasRoomFacility(RoomInstance room, FacilityKind kind)
        {
            for (int i = 0; i < room.Facilities.Count; i++)
                if (room.Facilities[i] != null
                    && room.Facilities[i].Kind == kind
                    && room.Facilities[i].Capacity > 0) return true;
            return false;
        }

        private static bool HasRoomAnchor(RoomInstance room, AnchorId id)
        {
            for (int i = 0; i < room.Anchors.Count; i++)
                if (room.Anchors[i] != null && room.Anchors[i].Id == id) return true;
            return false;
        }

        private static bool IsUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        private void ValidateObjectDefinition(
            InteractiveObjectDefinition definition,
            List<ContentValidationIssue> issues)
        {
            if (!IsPositiveFinite(definition.MaximumHealth))
                AddIssue(issues, ValidationSeverity.Error, "object_definition.health.invalid", definition.Id.Value,
                    "Object maximum health must be finite and greater than zero.");
            if (!IsUnitPercentage(definition.DefaultCleanliness))
                AddIssue(issues, ValidationSeverity.Error, "object_definition.cleanliness.invalid", definition.Id.Value,
                    "Object default cleanliness must be from zero to one hundred.");
            var localIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Affordances.Count; i++)
            {
                ObjectAffordanceDefinition affordance = definition.Affordances[i];
                if (affordance == null) continue;
                if (string.IsNullOrEmpty(affordance.LocalId) || !localIds.Add(affordance.LocalId))
                    AddIssue(issues, ValidationSeverity.Error, "object_definition.affordance.id_invalid", definition.Id.Value,
                        "Object affordance local IDs must be non-empty and unique within the definition.");
                if (affordance.SlotCount < 0)
                    AddIssue(issues, ValidationSeverity.Error, "object_definition.affordance.slots_invalid", definition.Id.Value,
                        "Object affordance slot counts cannot be negative.");
                if (!IsFinite(affordance.BaseUtility) || !IsFinite(affordance.Comfort)
                    || !IsUnitPercentage(affordance.Privacy) || !IsUnit(affordance.EmergencyThreshold))
                    AddIssue(issues, ValidationSeverity.Error, "object_definition.affordance.values_invalid", definition.Id.Value,
                        "Affordance utility, comfort, privacy, and emergency values must be finite and valid.");
            }
        }

        private void ValidateObjectInstance(
            AuthoredObjectInstance value,
            List<ContentValidationIssue> issues)
        {
            InteractiveObjectDefinition definition = FindObjectDefinition(value.Definition);
            if (definition == null)
            {
                AddIssue(issues, ValidationSeverity.Error, "object.definition.missing", value.Id.Value,
                    "Object instance references a missing object definition.");
                return;
            }
            RoomInstance room = FindRoomInstance(value.Room);
            if (room == null)
                AddIssue(issues, ValidationSeverity.Error, "object.room.missing", value.Id.Value,
                    "Object instance references a missing authored room.");
            else
            {
                if (value.Building != room.Building)
                    AddIssue(issues, ValidationSeverity.Error, "object.building.mismatch", value.Id.Value,
                        "Object building must match the room's parent building.");
                if (!string.IsNullOrEmpty(value.Anchor.Value)
                    && !HasRoomAnchor(room, value.Anchor))
                    AddIssue(issues, ValidationSeverity.Error, "object.anchor.missing", value.Id.Value,
                        "Object anchor must be one of the room's authored anchors.");
            }
            if (!IsFinite(value.LocalPosition.X) || !IsFinite(value.LocalPosition.Y)
                || !IsFinite(value.LocalPosition.Z))
                AddIssue(issues, ValidationSeverity.Error, "object.position.invalid", value.Id.Value,
                    "Object room-local coordinates must be finite.");
            if (value.InitialHealth != -1f
                && (!IsFinite(value.InitialHealth) || value.InitialHealth < 0f
                    || value.InitialHealth > definition.MaximumHealth))
                AddIssue(issues, ValidationSeverity.Error, "object.health.invalid", value.Id.Value,
                    "Initial object health must not exceed the definition maximum.");
            if (value.InitialCleanliness != -1f && !IsUnitPercentage(value.InitialCleanliness))
                AddIssue(issues, ValidationSeverity.Error, "object.cleanliness.invalid", value.Id.Value,
                    "Initial object cleanliness must be from zero to one hundred.");
            if (value.Ownership != null && value.Ownership.Kind != OwnershipKind.None
                && value.Ownership.Kind != OwnershipKind.Public
                && string.IsNullOrWhiteSpace(value.Ownership.OwnerId))
                AddIssue(issues, ValidationSeverity.Error, "object.owner.missing", value.Id.Value,
                    "Non-public object ownership requires a stable owner ID.");
            var assignedSims = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < value.AssignedSimIds.Count; i++)
                if (string.IsNullOrWhiteSpace(value.AssignedSimIds[i]) || !assignedSims.Add(value.AssignedSimIds[i]))
                    AddIssue(issues, ValidationSeverity.Error, "object.assigned_sim.invalid", value.Id.Value,
                        "Assigned Sim IDs must be non-empty and unique within the object.");
            int assignableBeds = 0;
            for (int i = 0; i < definition.Affordances.Count; i++)
                if (definition.Affordances[i] != null
                    && definition.Affordances[i].Sleeping != SleepingArrangement.None)
                    assignableBeds += Math.Max(0, definition.Affordances[i].SlotCount);
            if (value.AssignedSimIds.Count > assignableBeds)
                AddIssue(issues, ValidationSeverity.Error, "object.bed.assignment_over_capacity", value.Id.Value,
                    "Assigned bed occupants cannot exceed authored sleeping slots.");
            if (definition.IsPortalController && value.Door == null)
                AddIssue(issues, ValidationSeverity.Error, "object.door_state.missing", value.Id.Value,
                    "A portal-controlling object requires authored door state.");
            if (!definition.IsPortalController && value.Door != null)
                AddIssue(issues, ValidationSeverity.Error, "object.door_state.unexpected", value.Id.Value,
                    "Only portal-controlling object definitions may carry door state.");
            if (value.Door != null)
            {
                var connections = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < value.Door.ConnectionIds.Count; i++)
                    if (string.IsNullOrEmpty(value.Door.ConnectionIds[i]) || !connections.Add(value.Door.ConnectionIds[i]))
                        AddIssue(issues, ValidationSeverity.Error, "object.door.connection_invalid", value.Id.Value,
                            "Door connection IDs must be non-empty and unique within the object.");
                var gateways = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < value.Door.GatewayIds.Count; i++)
                {
                    string gatewayId = value.Door.GatewayIds[i].Value;
                    if (string.IsNullOrEmpty(gatewayId) || !gateways.Add(gatewayId))
                        AddIssue(issues, ValidationSeverity.Error, "object.door.gateway_invalid", value.Id.Value,
                            "Door gateway IDs must be non-empty and unique within the object.");
                    else if (!HasGateway(value.Door.GatewayIds[i]))
                        AddIssue(issues, ValidationSeverity.Error, "object.door.gateway_missing", value.Id.Value,
                            "Door object references a missing building gateway.");
                }
                if (connections.Count == 0 && gateways.Count == 0)
                    AddIssue(issues, ValidationSeverity.Error, "object.door.connections_missing", value.Id.Value,
                        "A portal object must control at least one world connection or building gateway.");
            }
        }

        private void ValidateBuildingGraphs(List<ContentValidationIssue> issues)
        {
            var graphIds = new HashSet<string>(StringComparer.Ordinal);
            var graphBuildings = new HashSet<string>(StringComparer.Ordinal);
            var gatewayIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < BuildingGraphs.Count; i++)
            {
                BuildingRoomGraph graph = BuildingGraphs[i];
                if (graph == null) continue;
                if (string.IsNullOrEmpty(graph.Id.Value) || !graphIds.Add(graph.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "building_graph.id_invalid", graph.Id.Value,
                        "Building graph IDs must be non-empty and unique.");
                if (FindBuildingInstance(graph.Building) == null)
                    AddIssue(issues, ValidationSeverity.Error, "building_graph.building_missing", graph.Id.Value,
                        "Building graph references a missing authored building.");
                if (!graphBuildings.Add(graph.Building.Value))
                    AddIssue(issues, ValidationSeverity.Error, "building_graph.building_duplicate", graph.Id.Value,
                        "An authored building may own only one room graph.");

                var memberBuildings = new HashSet<BuildingId>();
                memberBuildings.Add(graph.Building);
                for (int memberIndex = 0; memberIndex < graph.MemberBuildings.Count; memberIndex++)
                {
                    BuildingId memberId = graph.MemberBuildings[memberIndex];
                    BuildingInstance member = FindBuildingInstance(memberId);
                    if (!memberBuildings.Add(memberId) || member == null || member.ParentBuilding != graph.Building)
                        AddIssue(issues, ValidationSeverity.Error, "building_graph.member_invalid", graph.Id.Value,
                            "Graph members must be unique direct child buildings of the graph root.");
                    if (!graphBuildings.Add(memberId.Value))
                        AddIssue(issues, ValidationSeverity.Error, "building_graph.member_duplicate", graph.Id.Value,
                            "A building may belong to only one room graph.");
                }

                var rooms = new HashSet<string>(StringComparer.Ordinal);
                for (int roomIndex = 0; roomIndex < graph.Rooms.Count; roomIndex++)
                {
                    RoomId roomId = graph.Rooms[roomIndex];
                    RoomInstance room = FindRoomInstance(roomId);
                    if (string.IsNullOrEmpty(roomId.Value) || !rooms.Add(roomId.Value))
                        AddIssue(issues, ValidationSeverity.Error, "building_graph.room_invalid", graph.Id.Value,
                            "Graph room IDs must be non-empty and unique.");
                    else if (room == null || !memberBuildings.Contains(room.Building))
                        AddIssue(issues, ValidationSeverity.Error, "building_graph.room_wrong_building", graph.Id.Value,
                            "Every graph node must belong to the graph root or one of its direct child buildings.");
                }

                var arcIds = new HashSet<string>(StringComparer.Ordinal);
                var graphGatewayIds = new HashSet<string>(StringComparer.Ordinal);
                for (int gatewayIndex = 0; gatewayIndex < graph.Gateways.Count; gatewayIndex++)
                {
                    BuildingGateway gateway = graph.Gateways[gatewayIndex];
                    if (gateway == null) continue;
                    if (string.IsNullOrEmpty(gateway.Id.Value) || !gatewayIds.Add(gateway.Id.Value))
                        AddIssue(issues, ValidationSeverity.Error, "gateway.id_invalid", graph.Id.Value,
                            "Gateway IDs must be non-empty and globally unique.");
                    else graphGatewayIds.Add(gateway.Id.Value);
                    if (gateway.Arcs.Count == 0)
                        AddIssue(issues, ValidationSeverity.Warning, "gateway.arcs_missing", gateway.Id.Value,
                            "Gateway has no directed traversal arcs.");
                    if (gateway.Kind == GatewayKind.OpenBoundary
                        && (gateway.Portal == null || gateway.Portal.Shape == GatewayPortalShape.Point))
                        AddIssue(issues, ValidationSeverity.Error, "gateway.open_boundary.extent_missing", gateway.Id.Value,
                            "An open outdoor boundary must use a segment or area portal extent.");
                    if (gateway.Portal == null || !IsFinite(gateway.Portal.Start.X)
                        || !IsFinite(gateway.Portal.Start.Y) || !IsFinite(gateway.Portal.Start.Z)
                        || !IsFinite(gateway.Portal.End.X) || !IsFinite(gateway.Portal.End.Y)
                        || !IsFinite(gateway.Portal.End.Z) || gateway.Portal.HalfWidth < 0f
                        || !IsFinite(gateway.Portal.HalfWidth))
                        AddIssue(issues, ValidationSeverity.Error, "gateway.portal.invalid", gateway.Id.Value,
                            "Gateway portal geometry must be finite and non-negative.");
                    for (int arcIndex = 0; arcIndex < gateway.Arcs.Count; arcIndex++)
                    {
                        GatewayArc arc = gateway.Arcs[arcIndex];
                        if (arc == null) continue;
                        if (arc.Gateway != gateway.Id)
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.owner_mismatch", gateway.Id.Value,
                                "Gateway arc must reference its owning gateway.");
                        if (string.IsNullOrEmpty(arc.Id) || !arcIds.Add(arc.Id))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.id_invalid", gateway.Id.Value,
                                "Gateway arc IDs must be non-empty and unique within the building graph.");
                        bool sourceInterior = !string.IsNullOrEmpty(arc.SourceRoom.Value);
                        if (sourceInterior == arc.StartsOutside)
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.source_invalid", gateway.Id.Value,
                                "Gateway arc must start at exactly one room or exterior socket.");
                        if (sourceInterior && !rooms.Contains(arc.SourceRoom.Value))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.source_missing", gateway.Id.Value,
                                "Gateway arc source must be a graph room.");
                        RoomInstance sourceRoom = sourceInterior ? FindRoomInstance(arc.SourceRoom) : null;
                        if (sourceRoom != null && arc.SourceBuilding != sourceRoom.Building)
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.source_building", gateway.Id.Value,
                                "Gateway arc source-building ownership must match its source room.");
                        bool interior = !string.IsNullOrEmpty(arc.DestinationRoom.Value);
                        if (interior == arc.LeadsOutside)
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.destination_invalid", gateway.Id.Value,
                                "Gateway arc must lead to exactly one room or exterior socket.");
                        if (interior && !rooms.Contains(arc.DestinationRoom.Value))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.destination_missing", gateway.Id.Value,
                                "Gateway arc destination must be a graph room.");
                        RoomInstance destinationRoom = interior ? FindRoomInstance(arc.DestinationRoom) : null;
                        if (destinationRoom != null && arc.DestinationBuilding != destinationRoom.Building)
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.destination_building", gateway.Id.Value,
                                "Gateway arc destination-building ownership must match its destination room.");
                        if (arc.LeadsOutside && (string.IsNullOrEmpty(arc.ExteriorDestination.Cell.Value)
                            || string.IsNullOrEmpty(arc.ExteriorDestination.WorldConnectionId)))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.socket_invalid", gateway.Id.Value,
                                "Exterior gateway arcs require a cell and world connection ID.");
                        if (arc.StartsOutside && (string.IsNullOrEmpty(arc.ExteriorSource.Cell.Value)
                            || string.IsNullOrEmpty(arc.ExteriorSource.WorldConnectionId)))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.source_socket_invalid", gateway.Id.Value,
                                "Exterior-source gateway arcs require a cell and world connection ID.");
                        if (!IsNonNegativeFinite(arc.CrossingDistance) || !IsPositiveFinite(arc.CostMultiplier)
                            || arc.FixedDelaySeconds < 0d || double.IsNaN(arc.FixedDelaySeconds)
                            || double.IsInfinity(arc.FixedDelaySeconds))
                            AddIssue(issues, ValidationSeverity.Error, "gateway.arc.cost_invalid", gateway.Id.Value,
                                "Gateway distances, multipliers, and delays must be finite and non-negative.");
                    }
                }

                var pathIds = new HashSet<string>(StringComparer.Ordinal);
                for (int pathIndex = 0; pathIndex < graph.RoomPaths.Count; pathIndex++)
                {
                    RoomTraversalPath path = graph.RoomPaths[pathIndex];
                    if (path == null) continue;
                    if (string.IsNullOrEmpty(path.Id.Value) || !pathIds.Add(path.Id.Value))
                        AddIssue(issues, ValidationSeverity.Error, "room_path.id_invalid", graph.Id.Value,
                            "Room path IDs must be non-empty and unique within the graph.");
                    if (!rooms.Contains(path.Room.Value))
                        AddIssue(issues, ValidationSeverity.Error, "room_path.room_missing", path.Id.Value,
                            "Room path must reference a graph room.");
                    if (!string.IsNullOrEmpty(path.IncomingGateway.Value) && !graphGatewayIds.Contains(path.IncomingGateway.Value)
                        || !string.IsNullOrEmpty(path.OutgoingGateway.Value) && !graphGatewayIds.Contains(path.OutgoingGateway.Value))
                        AddIssue(issues, ValidationSeverity.Error, "room_path.gateway_missing", path.Id.Value,
                            "Room path gateway endpoints must exist.");
                    if (!IsNonNegativeFinite(path.PhysicalDistance))
                        AddIssue(issues, ValidationSeverity.Error, "room_path.distance_invalid", path.Id.Value,
                            "Room path distance must be finite and non-negative.");
                }
            }
        }

        private bool HasGateway(GatewayId id)
        {
            for (int i = 0; i < BuildingGraphs.Count; i++)
            {
                BuildingRoomGraph graph = BuildingGraphs[i];
                if (graph == null) continue;
                for (int g = 0; g < graph.Gateways.Count; g++)
                    if (graph.Gateways[g] != null && graph.Gateways[g].Id == id) return true;
            }
            return false;
        }

        private bool HasDuplicateSceneRoom(RoomInstance room)
        {
            int matches = 0;
            for (int i = 0; i < RoomInstances.Count; i++)
                if (RoomInstances[i] != null && RoomInstances[i].Scene.IsDefined
                    && string.Equals(RoomInstances[i].Scene.SceneId, room.Scene.SceneId, StringComparison.Ordinal)
                    && string.Equals(RoomInstances[i].Scene.SceneObjectId, room.Scene.SceneObjectId, StringComparison.Ordinal))
                    matches++;
            return matches > 1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsUnitPercentage(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 100f;
        }

        private static bool HasFacility(BuildingDefinition definition, FacilityKind kind)
        {
            for (int i = 0; i < definition.Facilities.Count; i++)
                if (definition.Facilities[i] != null
                    && definition.Facilities[i].Kind == kind
                    && definition.Facilities[i].Capacity > 0) return true;
            return false;
        }

        private void ValidateUniqueJobIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Jobs.Count; i++)
            {
                JobDefinition job = Jobs[i];
                if (job == null || string.IsNullOrEmpty(job.Id.Value) || !seen.Add(job.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "job.id.invalid", job == null ? string.Empty : job.Id.Value,
                        "Job IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueBuildingDefinitionIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < BuildingDefinitions.Count; i++)
            {
                BuildingDefinition definition = BuildingDefinitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id.Value) || !seen.Add(definition.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "building_definition.id.invalid",
                        definition == null ? string.Empty : definition.Id.Value,
                        "Building definition IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueBuildingInstanceIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < BuildingInstances.Count; i++)
            {
                BuildingInstance instance = BuildingInstances[i];
                if (instance == null || string.IsNullOrEmpty(instance.Id.Value) || !seen.Add(instance.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "building_instance.id.invalid",
                        instance == null ? string.Empty : instance.Id.Value,
                        "Building instance IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueRoomDefinitionIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < RoomDefinitions.Count; i++)
            {
                RoomDefinition definition = RoomDefinitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id.Value) || !seen.Add(definition.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "room_definition.id.invalid",
                        definition == null ? string.Empty : definition.Id.Value,
                        "Room definition IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueRoomInstanceIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < RoomInstances.Count; i++)
            {
                RoomInstance room = RoomInstances[i];
                if (room == null || string.IsNullOrEmpty(room.Id.Value) || !seen.Add(room.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "room_instance.id.invalid",
                        room == null ? string.Empty : room.Id.Value,
                        "Room instance IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueRoomAnchorIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int roomIndex = 0; roomIndex < RoomInstances.Count; roomIndex++)
            {
                RoomInstance room = RoomInstances[roomIndex];
                if (room == null) continue;
                for (int anchorIndex = 0; anchorIndex < room.Anchors.Count; anchorIndex++)
                {
                    RoomAnchorPlacement anchor = room.Anchors[anchorIndex];
                    if (anchor != null && !string.IsNullOrEmpty(anchor.Id.Value) && !seen.Add(anchor.Id.Value))
                        AddIssue(issues, ValidationSeverity.Error, "room.anchor.id_duplicate", room.Id.Value,
                            "Room anchor IDs must be globally unique within authored town content.");
                }
            }
        }

        private void ValidateBuildingHierarchy(List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < BuildingInstances.Count; i++)
            {
                BuildingInstance building = BuildingInstances[i];
                if (building == null) continue;
                if (building.IsManagedSubBuilding)
                {
                    BuildingInstance parent = FindBuildingInstance(building.ParentBuilding);
                    if (parent == null)
                        AddIssue(issues, ValidationSeverity.Error, "building.parent.missing", building.Id.Value,
                            "Managed sub-building references a missing parent building.");
                    else if (parent.IsManagedSubBuilding)
                        AddIssue(issues, ValidationSeverity.Error, "building.parent.depth", building.Id.Value,
                            "Building hierarchy is limited to one parent and one child level.");
                    if (building.Occupancy == BuildingOccupancyKind.DetentionCell
                        && (parent == null || parent.DetentionMeals == null || !parent.DetentionMeals.IsValid))
                        AddIssue(issues, ValidationSeverity.Error, "detention.meal_schedule.required", building.Id.Value,
                            "A detention-cell parent must author a valid guard delivery meal schedule.");
                    if (building.Occupancy == BuildingOccupancyKind.DetentionCell
                        && parent != null && !HasDetentionFeedingRole(parent.Id))
                        AddIssue(issues, ValidationSeverity.Error, "detention.feeding_role.required", building.Id.Value,
                            "An occupied jail requires an authored parent-building job duty that feeds prisoners.");
                    if (building.Occupancy == BuildingOccupancyKind.DetentionCell
                        && parent != null && parent.DetentionMeals != null && parent.DetentionMeals.IsValid
                        && !HasDetentionMealCoverage(parent.Id, parent.DetentionMeals))
                        AddIssue(issues, ValidationSeverity.Error, "detention.feeding_schedule.uncovered", building.Id.Value,
                            "Guard shifts with the feed-prisoners duty must cover every authored meal delivery window.");
                }
                if (building.Occupancy != BuildingOccupancyKind.None)
                {
                    int bedCapacity = CountAuthoredBedSlots(building);
                    if (bedCapacity <= 0)
                        AddIssue(issues, ValidationSeverity.Error, "building.occupancy.capacity", building.Id.Value,
                            "Occupied buildings derive resident capacity from authored beds and require at least one valid sleeping slot.");
                    if ((building.Occupancy == BuildingOccupancyKind.LodgingResidence
                        || building.Occupancy == BuildingOccupancyKind.DetentionCell)
                        && !building.IsManagedSubBuilding)
                        AddIssue(issues, ValidationSeverity.Error, "building.occupancy.parent_required", building.Id.Value,
                            "Lodging residences and detention cells must be managed direct children.");
                    if (building.Occupancy == BuildingOccupancyKind.LodgingResidence
                        && building.PopulationEligible)
                        AddIssue(issues, ValidationSeverity.Error, "building.lodging.bootstrap_eligible", building.Id.Value,
                            "Lodging child residences are filled by leases, not permanent population bootstrap.");
                }
            }
        }

        private bool HasDetentionFeedingRole(BuildingId manager)
        {
            BuildingInstance parent = FindBuildingInstance(manager);
            BuildingDefinition definition = parent == null ? null : FindBuildingDefinition(parent.Definition);
            if (definition != null)
                for (int i = 0; i < definition.Roles.Count; i++)
                    if (definition.Roles[i] != null && JobHasDuty(definition.Roles[i].Job, DutyKind.FeedPrisoners))
                        return true;
            for (int roomIndex = 0; roomIndex < RoomInstances.Count; roomIndex++)
            {
                RoomInstance room = RoomInstances[roomIndex];
                if (room == null || room.Building != manager) continue;
                for (int positionIndex = 0; positionIndex < room.Positions.Count; positionIndex++)
                    if (room.Positions[positionIndex] != null
                        && JobHasDuty(room.Positions[positionIndex].Job, DutyKind.FeedPrisoners)) return true;
            }
            return false;
        }

        private bool JobHasDuty(JobDefinitionId jobId, DutyKind duty)
        {
            JobDefinition job = FindJob(jobId);
            if (job == null) return false;
            for (int i = 0; i < job.Duties.Count; i++)
                if (job.Duties[i] != null && job.Duties[i].Kind == duty) return true;
            return false;
        }

        private bool HasDetentionMealCoverage(BuildingId manager, DetentionMealSchedule meals)
        {
            var roles = new List<BuildingRoleTemplate>();
            BuildingInstance parent = FindBuildingInstance(manager);
            BuildingDefinition definition = parent == null ? null : FindBuildingDefinition(parent.Definition);
            if (definition != null) roles.AddRange(definition.Roles);
            for (int i = 0; i < RoomInstances.Count; i++)
                if (RoomInstances[i] != null && RoomInstances[i].Building == manager)
                    roles.AddRange(RoomInstances[i].Positions);
            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                DayOfWeek day = (DayOfWeek)dayIndex;
                for (int mealIndex = 0; mealIndex < meals.MinutesOfDay.Count; mealIndex++)
                {
                    bool covered = false;
                    int start = meals.MinutesOfDay[mealIndex];
                    int end = start + meals.DeliveryWindowMinutes;
                    for (int roleIndex = 0; roleIndex < roles.Count && !covered; roleIndex++)
                    {
                        BuildingRoleTemplate role = roles[roleIndex];
                        JobDefinition job = role == null ? null : FindJob(role.Job);
                        if (job == null || !JobHasDuty(job.Id, DutyKind.FeedPrisoners)) continue;
                        WorkSchedule schedule = role.ScheduleOverride ?? job.DefaultSchedule;
                        for (int minute = start; minute <= end && !covered; minute++)
                        {
                            DayOfWeek checkDay = (DayOfWeek)(((int)day + (minute / 1440)) % 7);
                            covered = schedule.IsWorking(checkDay, minute % 1440);
                        }
                    }
                    if (!covered) return false;
                }
            }
            return true;
        }

        private int CountAuthoredBedSlots(BuildingInstance building)
        {
            int count = 0;
            for (int i = 0; i < ObjectInstances.Count; i++)
            {
                AuthoredObjectInstance value = ObjectInstances[i];
                if (value == null || !value.Enabled || value.Building != building.Id) continue;
                InteractiveObjectDefinition definition = FindObjectDefinition(value.Definition);
                if (definition == null) continue;
                for (int a = 0; a < definition.Affordances.Count; a++)
                {
                    ObjectAffordanceDefinition affordance = definition.Affordances[a];
                    if (affordance != null && IsAuthoritativeBed(affordance.Sleeping, building.Occupancy))
                        count += Math.Max(0, affordance.SlotCount);
                }
            }
            return count;
        }

        private void ValidateUniqueObjectDefinitionIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ObjectDefinitions.Count; i++)
            {
                InteractiveObjectDefinition definition = ObjectDefinitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id.Value) || !seen.Add(definition.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "object_definition.id.invalid",
                        definition == null ? string.Empty : definition.Id.Value,
                        "Object definition IDs must be non-empty and unique.");
            }
        }

        private void ValidateUniqueObjectInstanceIds(List<ContentValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ObjectInstances.Count; i++)
            {
                AuthoredObjectInstance value = ObjectInstances[i];
                if (value == null || string.IsNullOrEmpty(value.Id.Value) || !seen.Add(value.Id.Value))
                    AddIssue(issues, ValidationSeverity.Error, "object_instance.id.invalid",
                        value == null ? string.Empty : value.Id.Value,
                        "Object instance IDs must be non-empty and unique.");
            }
        }

        private static WorkSchedule CloneSchedule(WorkSchedule source)
        {
            var clone = new WorkSchedule();
            if (source == null) return clone;
            for (int i = 0; i < source.Shifts.Count; i++)
            {
                WorkShift shift = source.Shifts[i];
                if (shift != null) clone.Shifts.Add(new WorkShift(shift.Day, shift.StartMinute, shift.DurationMinutes));
            }
            return clone;
        }

        private static void AddIssue(
            List<ContentValidationIssue> issues,
            ValidationSeverity severity,
            string code,
            string entityId,
            string message)
        {
            issues.Add(new ContentValidationIssue
            {
                Severity = severity,
                Code = code,
                EntityId = entityId,
                Message = message
            });
        }
    }
}
