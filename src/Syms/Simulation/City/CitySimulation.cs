using System;
using System.Collections.Generic;
using Syms.Simulation.Activities;
using Syms.Simulation.People;
using Syms.Simulation.Stats;
using Syms.Simulation.Travel;
using Syms.Simulation.World;
using Syms.Simulation.Buildings;
using Syms.Content;
using Syms.Simulation.Social;
using Syms.Population;
using Syms.Simulation.Objects;

namespace Syms.Simulation.City
{
    [Serializable]
    public sealed class CitySimulation
    {
        public string Name;
        public double CityTime;
        public List<Sim> Sims = new List<Sim>();
        public WorldTopology Topology = new WorldTopology();
        public RouteStore RouteData = new RouteStore();
        public CompositeRouteStore CompositeRouteData = new CompositeRouteStore();
        public CapacityService CapacityData = new CapacityService();
        public AuthoredTownContent TownContent = new AuthoredTownContent();
        public AuthoredTownRuntime TownRuntime = new AuthoredTownRuntime();
        public LifeStageThresholds LifeStages = new LifeStageThresholds();
        public RelationshipGraph Relationships = new RelationshipGraph();
        public SocialBiasProfile SocialBiases = new SocialBiasProfile();
        public EconomicPressureSettings EconomicPressure = new EconomicPressureSettings();
        public WorldRiskSettings RiskSettings = new WorldRiskSettings();
        public List<WorldSimulationEvent> WorldEvents = new List<WorldSimulationEvent>();
        public PopulationState PopulationData;

        [NonSerialized] private Dictionary<string, Sim> _simById;
        [NonSerialized] private SimulationEventQueue _wakeEvents;
        [NonSerialized] private ITravelTimeCalculator _travelTime;
        [NonSerialized] private IRoutePlanner _routePlanner;
        [NonSerialized] private ICompositeRoutePlanner _compositeRoutePlanner;
        [NonSerialized] private ICompositeRouteStore _compositeRoutes;
        [NonSerialized] private IRouteDependencyIndex _routeDependencies;
        [NonSerialized] private IRouteStore _routes;
        [NonSerialized] private IActivityPlanner _activityPlanner;
        [NonSerialized] private ICapacityService _capacity;
        [NonSerialized] private IActivityCandidateSource _candidateSource;
        [NonSerialized] private bool _relationshipContextsDirty = true;
        [NonSerialized] private PopulationStore _populationStore;

        public ICapacityService Capacity { get { EnsureRuntimeServices(); return _capacity; } }
        public IRouteStore Routes { get { EnsureRuntimeServices(); return _routes; } }
        public IRouteDependencyIndex RouteDependencies { get { EnsureRuntimeServices(); return _routeDependencies; } }
        public PopulationSelectors PopulationQueries
        {
            get
            {
                EnsureRuntimeServices();
                bool relationshipsWereDirty = _relationshipContextsDirty;
                EnsureImmediateRelationships();
                if (relationshipsWereDirty) TouchPopulationQueries();
                return _populationStore == null ? null : _populationStore.Select;
            }
        }

        public CitySimulation(string name)
        {
            Name = name ?? string.Empty;
            EnsureRuntimeServices();
        }

        public void ConfigureRuntime(
            IActivityCandidateSource candidateSource,
            IActivityPlanner activityPlanner,
            ITravelTimeCalculator travelTime,
            IRouteStore routes,
            ICapacityService capacity)
        {
            _candidateSource = candidateSource ?? throw new ArgumentNullException(nameof(candidateSource));
            _activityPlanner = activityPlanner ?? throw new ArgumentNullException(nameof(activityPlanner));
            _travelTime = travelTime ?? throw new ArgumentNullException(nameof(travelTime));
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _routePlanner = new CellRoutePlanner(Topology, _travelTime);
            _compositeRoutePlanner = null;
        }

        public void AddSim(Sim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            EnsureRuntimeServices();
            if (_simById.ContainsKey(sim.Id)) throw new InvalidOperationException("A Sim with this ID already exists.");
            Sims.Add(sim);
            _simById.Add(sim.Id, sim);
            _relationshipContextsDirty = true;
            if (sim.LifeStatus == LifeStatus.Alive) Schedule(sim, CityTime);
        }

        public void InitializeAuthoredTown(AuthoredTownContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            EnsureRuntimeServices();
            TownContent = content;
            TownRuntime = content.BuildRuntime(_capacity);
            _compositeRoutePlanner = null;
            SynchronizeObjectTopology();
        }

        public bool TryLeaseResidence(BuildingId residence, string householdId,
            IList<string> residentSimIds, double endsAt, out OccupancyLease lease)
        {
            EnsureRuntimeServices();
            lease = null;
            if (residentSimIds == null) return false;
            ResidenceInstance residenceData = TownRuntime.OccupancyLeases.FindResidence(residence);
            if (residenceData == null) return false;
            for (int i = 0; i < residentSimIds.Count; i++)
            {
                Sim sim = FindSim(residentSimIds[i]);
                if (sim == null) return false;
                if (residenceData.Occupancy == BuildingOccupancyKind.LodgingResidence)
                {
                    if (sim.Residence.Temporary != null && sim.Residence.Temporary.IsActiveAt(CityTime)) return false;
                }
                else if (sim.Residence.Active) return false;
            }
            if (!TownRuntime.OccupancyLeases.TryLease(
                    residence, householdId, residentSimIds, CityTime, endsAt, out lease)) return false;
            for (int i = 0; i < residentSimIds.Count; i++)
            {
                Sim sim = RequireSim(residentSimIds[i]);
                InteractiveObjectState bed = TownRuntime.Objects.Find(lease.AssignedBeds[i]);
                if (bed == null)
                    throw new InvalidOperationException("A residence lease must resolve every resident to an authored bed.");
                RoomId bedroom = bed.Room;
                if (residenceData.Occupancy != BuildingOccupancyKind.LodgingResidence)
                {
                    sim.Residence = new Residence
                    {
                        HouseholdId = householdId,
                        LeaseId = lease.Id,
                        Building = residence,
                        ManagedBy = residenceData.ManagedBy,
                        HomeAddress = residenceData.Address,
                        Bedroom = bedroom,
                        AssignedBed = lease.AssignedBeds[i],
                        Active = true
                    };
                    _relationshipContextsDirty = true;
                }
                else
                    sim.Residence.Temporary = new TemporaryResidence
                    {
                        LeaseId = lease.Id,
                        HouseholdId = householdId,
                        Building = residence,
                        ManagedBy = residenceData.ManagedBy,
                        Bedroom = bedroom,
                        AssignedBed = lease.AssignedBeds[i],
                        StartsAt = lease.StartsAt,
                        EndsAt = lease.EndsAt
                    };
            }
            _relationshipContextsDirty = true;
            return true;
        }

        public bool EndResidenceLease(string leaseId)
        {
            OccupancyLease lease = TownRuntime.OccupancyLeases.Find(leaseId);
            if (lease == null || !TownRuntime.OccupancyLeases.End(leaseId, CityTime)) return false;
            ClearLeaseResidence(lease);
            return true;
        }

        private void ClearLeaseResidence(OccupancyLease lease)
        {
            bool changed = false;
            for (int i = 0; i < lease.ResidentSimIds.Count; i++)
            {
                Sim sim = FindSim(lease.ResidentSimIds[i]);
                if (sim != null && sim.Residence.Temporary != null
                    && string.Equals(sim.Residence.Temporary.LeaseId, lease.Id, StringComparison.Ordinal))
                {
                    sim.Residence.Temporary = null;
                    changed = true;
                }
                if (sim != null && sim.Residence.Active
                    && string.Equals(sim.Residence.LeaseId, lease.Id, StringComparison.Ordinal))
                {
                    sim.Residence = new Residence();
                    changed = true;
                }
            }
            if (changed) _relationshipContextsDirty = true;
        }

        public bool TryBeginCustody(string simId, BuildingId cellBuilding,
            out CustodyAssignment custody)
        {
            Sim sim = RequireSim(simId);
            custody = null;
            BuildingInstance cell = TownContent.FindBuildingInstance(cellBuilding);
            RoomRuntimeInstance currentRoom = TownRuntime.FindRoom(sim.Spatial.Room);
            if (cell == null || cell.Occupancy != BuildingOccupancyKind.DetentionCell
                || currentRoom == null || currentRoom.Building != cellBuilding) return false;
            if (!TownRuntime.Detention.TryBeginCustody(
                    simId, cellBuilding, sim.Spatial.Room, CityTime, out custody)) return false;
            string claim = "prisoner:" + cell.ParentBuilding.Value;
            if (!sim.AccessClaims.Contains(claim)) sim.AccessClaims.Add(claim);
            return true;
        }

        public bool EndCustody(string simId)
        {
            Sim sim = RequireSim(simId);
            CustodyAssignment custody = TownRuntime.Detention.FindActive(simId);
            if (custody == null || !TownRuntime.Detention.EndCustody(simId, CityTime)) return false;
            sim.AccessClaims.Remove("prisoner:" + custody.ManagedBy.Value);
            return true;
        }

        public ObjectUseEvaluation EvaluateObjectUse(
            string simId,
            ObjectId objectId,
            string affordanceLocalId,
            IList<string> claims)
        {
            Sim sim = RequireSim(simId);
            InteractiveObjectState value = TownRuntime.Objects.Find(objectId);
            if (value == null) throw new InvalidOperationException("The authored object does not exist.");
            InteractiveObjectDefinition definition = TownRuntime.Objects.FindDefinition(value.Definition);
            ObjectAffordanceDefinition affordance = definition == null
                ? null : definition.FindAffordance(affordanceLocalId);
            if (affordance == null) throw new InvalidOperationException("The object does not expose that affordance.");
            var context = new ObjectUseContext
            {
                Sim = sim,
                Relationships = Relationships,
                CityTime = CityTime
            };
            context.Claims.AddRange(sim.AccessClaims);
            if (claims != null) context.Claims.AddRange(claims);
            return new ObjectUsePolicy().Evaluate(value, definition, affordance, context);
        }

        public bool TryReserveObjectUse(
            string simId,
            ObjectId objectId,
            string affordanceLocalId,
            double reservedUntil,
            IList<string> claims,
            out ObjectAffordanceSlot slot,
            out ObjectUseEvaluation evaluation)
        {
            evaluation = EvaluateObjectUse(simId, objectId, affordanceLocalId, claims);
            if (!evaluation.Allowed)
            {
                slot = null;
                return false;
            }
            return TownRuntime.Objects.TryReserve(
                objectId, affordanceLocalId, simId, CityTime, reservedUntil, out slot);
        }

        public bool ApplyObjectDamage(ObjectId objectId, float amount)
        {
            if (!TownRuntime.Objects.ApplyDamage(objectId, amount)) return false;
            InteractiveObjectState value = TownRuntime.Objects.Find(objectId);
            if (value != null && value.Door != null)
            {
                var doors = new DoorStateService();
                doors.Synchronize(value, Topology);
                doors.Synchronize(value, TownRuntime.GatewayStates);
                NotifyGatewayChanged(value.Door);
            }
            if (value != null && !value.IsOperational) InterruptObjectUsers(value.Id);
            return true;
        }

        public bool RepairObject(ObjectId objectId, float amount)
        {
            if (!TownRuntime.Objects.Repair(objectId, amount)) return false;
            InteractiveObjectState value = TownRuntime.Objects.Find(objectId);
            if (value != null && value.Door != null)
            {
                var doors = new DoorStateService();
                doors.Synchronize(value, Topology);
                doors.Synchronize(value, TownRuntime.GatewayStates);
                NotifyGatewayChanged(value.Door);
            }
            return true;
        }

        public bool TryApplyDoorCommand(
            string simId,
            ObjectId objectId,
            DoorCommand command,
            IList<string> claims)
        {
            Sim sim = RequireSim(simId);
            InteractiveObjectState value = TownRuntime.Objects.Find(objectId);
            if (value == null) return false;
            var context = new ObjectUseContext { Sim = sim, Relationships = Relationships, CityTime = CityTime };
            context.Claims.AddRange(sim.AccessClaims);
            if (claims != null) context.Claims.AddRange(claims);
            bool changed = new DoorStateService().TryApply(value, command, context, Topology);
            if (changed)
            {
                TownRuntime.Objects.Touch();
                new DoorStateService().Synchronize(value, TownRuntime.GatewayStates);
                NotifyGatewayChanged(value.Door);
            }
            return changed;
        }

        public bool TryApplyObjectLockCommand(
            string simId,
            ObjectId objectId,
            ObjectLockCommand command,
            IList<string> claims)
        {
            Sim sim = RequireSim(simId);
            InteractiveObjectState value = TownRuntime.Objects.Find(objectId);
            if (value == null) return false;
            var context = new ObjectUseContext { Sim = sim, Relationships = Relationships, CityTime = CityTime };
            context.Claims.AddRange(sim.AccessClaims);
            if (claims != null) context.Claims.AddRange(claims);
            bool changed = new ObjectLockService().TryApply(value, command, context);
            if (!changed) return false;
            TownRuntime.Objects.Touch();
            if (value.Door != null)
            {
                var doors = new DoorStateService();
                doors.Synchronize(value, Topology);
                doors.Synchronize(value, TownRuntime.GatewayStates);
                NotifyGatewayChanged(value.Door);
            }
            return true;
        }

        public void SynchronizeObjectTopology()
        {
            var doors = new DoorStateService();
            for (int i = 0; i < TownRuntime.Objects.Objects.Count; i++)
            {
                InteractiveObjectState value = TownRuntime.Objects.Objects[i];
                if (value != null && value.Door != null)
                {
                    doors.Synchronize(value, Topology);
                    doors.Synchronize(value, TownRuntime.GatewayStates);
                }
            }
        }

        public void NotifyRouteDependencyChanged(string dependency)
        {
            EnsureRuntimeServices();
            IReadOnlyList<string> waiting = _routeDependencies.GetWaitingSims(dependency);
            for (int i = 0; i < waiting.Count; i++)
            {
                Sim sim = FindSim(waiting[i]);
                if (sim == null) continue;
                _routeDependencies.Clear(sim.Id);
                sim.RouteFailure = null;
                if (sim.Activity != null) sim.Activity.Status = ActivityStatus.Interrupted;
                Schedule(sim, CityTime);
            }
        }

        public bool SetGatewayBlocker(GatewayId gateway, GatewayBlocker blocker, bool enabled)
        {
            EnsureRuntimeServices();
            bool changed = TownRuntime.GatewayStates.SetBlocker(gateway, blocker, enabled);
            if (changed) NotifyRouteDependencyChanged("gateway:" + gateway.Value);
            return changed;
        }

        public void ReplaceBuildingGraph(BuildingRoomGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            EnsureRuntimeServices();
            BuildingRoomGraph previous;
            if (TownRuntime.BuildingGraphs.TryGetGraph(graph.Building, out previous))
                NotifyRouteDependencyChanged("building-graph:" + previous.Building.Value + ":" + previous.Revision);
            TownRuntime.BuildingGraphs.AddOrReplaceGraph(graph);
            NotifyRouteDependencyChanged("building:" + graph.Building.Value);
        }

        public bool ObserveGateway(string simId, int distanceInBlocks, GatewayId gateway,
            string arcId, GatewayTraversalOutcome outcome)
        {
            Sim sim = RequireSim(simId);
            BuildingGateway authored;
            if (!TownRuntime.BuildingGraphs.TryGetGateway(gateway, out authored)) return false;
            GatewayRuntimeState state;
            TownRuntime.GatewayStates.TryGet(gateway, out state);
            return new GatewayObservationSink().Observe(sim, distanceInBlocks,
                new ObservedGatewayFact
                {
                    Gateway = gateway,
                    ArcId = arcId,
                    Blockers = state == null ? GatewayBlocker.None : state.Blockers,
                    Outcome = outcome,
                    GatewayRevision = state == null ? authored.Revision : Math.Max(authored.Revision, state.Revision)
                }, CityTime);
        }

        public bool GrantAccessClaim(string simId, string claim)
        {
            if (string.IsNullOrWhiteSpace(claim)) throw new ArgumentException("An access claim is required.", nameof(claim));
            Sim sim = RequireSim(simId);
            if (sim.AccessClaims.Contains(claim)) return false;
            sim.AccessClaims.Add(claim);
            NotifyRouteDependencyChanged("access");
            return true;
        }

        public bool RevokeAccessClaim(string simId, string claim)
        {
            Sim sim = RequireSim(simId);
            bool changed = sim.AccessClaims.Remove(claim);
            if (changed) NotifyRouteDependencyChanged("access");
            return changed;
        }

        private void NotifyGatewayChanged(DoorState door)
        {
            if (door == null) return;
            for (int i = 0; i < door.GatewayIds.Count; i++)
                NotifyRouteDependencyChanged("gateway:" + door.GatewayIds[i].Value);
        }

        private void InterruptObjectUsers(ObjectId objectId)
        {
            IReadOnlyList<ObjectAffordanceSlot> slots = TownRuntime.Objects.GetSlots(objectId);
            for (int i = 0; i < slots.Count; i++)
            {
                ObjectAffordanceSlot slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.ReservedBySimId)) continue;
                string simId = slot.ReservedBySimId;
                TownRuntime.Objects.Release(slot.Id, simId);
                Sim sim = FindSim(simId);
                if (sim == null || sim.Activity == null || sim.Activity.Object != objectId) continue;
                if (!string.IsNullOrEmpty(sim.Activity.ReservationId))
                    _capacity.Release(sim.Activity.ReservationId);
                sim.Activity.Status = ActivityStatus.Interrupted;
                sim.Spatial.InvalidateObservedPose();
                Schedule(sim, CityTime);
            }
        }

        public void InitializePopulation(PopulationBootstrapResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Report == null || result.Report.HasErrors)
                throw new InvalidOperationException("Population bootstrap contains validation errors.");
            if (Sims.Count != 0)
                throw new InvalidOperationException("Population can only be initialized into an empty city.");
            if (result.State == null) throw new InvalidOperationException("Population state is missing.");

            for (int i = 0; i < result.State.EmploymentAssignments.Count; i++)
            {
                EmploymentAssignmentRecord assignment = result.State.EmploymentAssignments[i];
                BuildingRoleSlot slot = TownRuntime.FindRoleSlot(assignment.RoleSlot);
                if (slot == null || !slot.IsVacant)
                    throw new InvalidOperationException(
                        "Population references a missing or occupied authored role slot '"
                        + assignment.RoleSlot.Value + "'.");
            }

            PopulationData = result.State;
            Relationships = result.State.Relationships;
            for (int i = 0; i < result.State.Sims.Count; i++) AddSim(result.State.Sims[i]);
            for (int i = 0; i < result.State.EmploymentAssignments.Count; i++)
            {
                EmploymentAssignmentRecord assignment = result.State.EmploymentAssignments[i];
                AssignEmployment(assignment.SimId, assignment.RoleSlot);
            }
            RefreshImmediateRelationships();
            _populationStore = new PopulationStore(PopulationData);
        }

        public EmploymentContract AssignEmployment(string simId, RoleSlotId roleSlotId)
        {
            Sim sim = FindSim(simId);
            if (sim == null) throw new InvalidOperationException("The Sim does not exist in this city.");
            BuildingRoleSlot slot = TownRuntime.FindRoleSlot(roleSlotId);
            if (slot == null) throw new InvalidOperationException("The authored building role slot does not exist.");
            if (sim.Employment.Contract != null && sim.Employment.Contract.Active)
                throw new InvalidOperationException("The Sim already has an active employment contract.");
            sim.Employment.Contract = new EmploymentService().Assign(slot, sim.Id, CityTime);
            _relationshipContextsDirty = true;
            WakeSim(sim.Id);
            return sim.Employment.Contract;
        }

        public void InitializeResidence(
            string simId,
            string householdId,
            BuildingId building,
            WorldAddress homeAddress)
        {
            SetResidenceCore(simId, householdId, building, homeAddress);
        }

        private void SetResidenceCore(
            string simId,
            string householdId,
            BuildingId building,
            WorldAddress homeAddress)
        {
            if (string.IsNullOrWhiteSpace(householdId))
                throw new ArgumentException("A household ID is required.", nameof(householdId));
            Sim sim = RequireSim(simId);
            BuildingInstance residenceBuilding = TownContent.FindBuildingInstance(building);
            sim.Residence = new Residence
            {
                HouseholdId = householdId,
                Building = building,
                ManagedBy = residenceBuilding != null && residenceBuilding.IsManagedSubBuilding
                    ? residenceBuilding.ParentBuilding : building,
                HomeAddress = homeAddress,
                Active = true
            };
            _relationshipContextsDirty = true;
        }

        private void ClearResidenceCore(string simId)
        {
            RequireSim(simId).Residence = new Residence();
            _relationshipContextsDirty = true;
        }

        /// <summary>
        /// Reconciles sparse social records from authoritative family, household,
        /// workplace, and home-block data. Call after bulk population setup; the
        /// simulation also invokes it lazily before advancing or querying circles.
        /// </summary>
        public SocialCircleRefreshResult RefreshImmediateRelationships()
        {
            EnsureRuntimeServices();
            SocialCircleRefreshResult result = new ImmediateSocialCircleService()
                .Refresh(Sims, Relationships, CityTime);
            _relationshipContextsDirty = false;
            return result;
        }

        public List<SocialRelationship> GetImmediateRelationships(string simId)
        {
            RequireSim(simId);
            EnsureImmediateRelationships();
            return Relationships.GetForSim(simId);
        }

        public SocialRelationship InitializePartners(string firstSimId, string secondSimId)
        {
            return SetPartnersCore(firstSimId, secondSimId);
        }

        private SocialRelationship SetPartnersCore(string firstSimId, string secondSimId)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            SocialRelationship relationship = Relationships.SetPartners(firstSimId, secondSimId, CityTime);
            _relationshipContextsDirty = true;
            return relationship;
        }

        public SocialRelationship ApplyScriptedPartnership(string firstSimId, string secondSimId)
        {
            SocialRelationship relationship = SetPartnersCore(firstSimId, secondSimId);
            TouchPopulationQueries();
            return relationship;
        }

        public bool ApplyScriptedBreakup(string firstSimId, string secondSimId)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            bool ended = Relationships.EndPartnership(firstSimId, secondSimId, CityTime);
            if (ended) _relationshipContextsDirty = true;
            if (ended) TouchPopulationQueries();
            return ended;
        }

        public void ApplyScriptedAgeChange(string simId, int ageYears, double ageProgress = 0d)
        {
            RequireSim(simId).Demographics.SetAge(ageYears, ageProgress);
            TouchPopulationQueries();
        }

        public void ApplyScriptedRelocation(
            string simId,
            string householdId,
            BuildingId building,
            WorldAddress homeAddress)
        {
            SetResidenceCore(simId, householdId, building, homeAddress);
            TouchPopulationQueries();
        }

        public void ApplyScriptedHomelessness(string simId)
        {
            ClearResidenceCore(simId);
            TouchPopulationQueries();
        }

        public SocialRelationship ApplyScriptedMarriage(string firstSimId, string secondSimId)
        {
            EnsureNoOtherMarriage(firstSimId, secondSimId);
            EnsureNoOtherMarriage(secondSimId, firstSimId);
            SocialRelationship relationship = SetPartnersCore(firstSimId, secondSimId);
            Relationships.SetFamily(
                firstSimId, FamilyRole.Spouse, secondSimId, FamilyRole.Spouse, CityTime);
            _relationshipContextsDirty = true;
            TouchPopulationQueries();
            return relationship;
        }

        public bool ApplyScriptedDivorce(string firstSimId, string secondSimId)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            bool divorced = Relationships.EndMarriage(firstSimId, secondSimId, CityTime);
            if (divorced) _relationshipContextsDirty = true;
            if (divorced) TouchPopulationQueries();
            return divorced;
        }

        public void ApplyScriptedBirth(Sim newborn)
        {
            if (newborn == null) throw new ArgumentNullException(nameof(newborn));
            if (newborn.LifeStatus != LifeStatus.Alive)
                throw new InvalidOperationException("A newborn must be alive.");
            if (newborn.Demographics.AgeYears != 0 || newborn.Demographics.AgeProgress != 0d)
                throw new InvalidOperationException("A scripted birth must introduce a newborn with age zero.");
            if (FindSim(newborn.Id) != null)
                throw new InvalidOperationException("The newborn Sim ID already exists.");
            if (_populationStore != null) _populationStore.Dispatch(PopulationAction.Add(newborn));
            AddSim(newborn);
        }

        public void ApplyScriptedDeath(string simId)
        {
            Sim sim = RequireSim(simId);
            if (sim.LifeStatus == LifeStatus.Dead) return;
            sim.LifeStatus = LifeStatus.Dead;
            sim.DecisionSequence++;
            sim.NextWakeTime = double.PositiveInfinity;
            sim.PendingActivity = null;
            if (sim.Activity != null && !string.IsNullOrEmpty(sim.Activity.ReservationId))
                _capacity.Release(sim.Activity.ReservationId);
            if (sim.Activity != null && !string.IsNullOrEmpty(sim.Activity.ObjectSlot.Value))
                TownRuntime.Objects.Release(sim.Activity.ObjectSlot, sim.Id);
            sim.Spatial.InvalidateObservedPose();
            _relationshipContextsDirty = true;
            TouchPopulationQueries();
        }

        public SocialRelationship InitializeFamily(
            string firstSimId,
            FamilyRole firstRole,
            string secondSimId,
            FamilyRole secondRole)
        {
            return SetFamilyCore(firstSimId, firstRole, secondSimId, secondRole);
        }

        public SocialRelationship ApplyScriptedFamilyLink(
            string firstSimId,
            FamilyRole firstRole,
            string secondSimId,
            FamilyRole secondRole)
        {
            SocialRelationship relationship = SetFamilyCore(firstSimId, firstRole, secondSimId, secondRole);
            TouchPopulationQueries();
            return relationship;
        }

        private SocialRelationship SetFamilyCore(
            string firstSimId,
            FamilyRole firstRole,
            string secondSimId,
            FamilyRole secondRole)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            SocialRelationship relationship = Relationships.SetFamily(
                firstSimId, firstRole, secondSimId, secondRole, CityTime);
            _relationshipContextsDirty = true;
            return relationship;
        }

        public SocialRelationship SetRelationshipAffinity(string firstSimId, string secondSimId, int affinity)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            SocialRelationship relationship = Relationships.SetAffinity(firstSimId, secondSimId, affinity, CityTime);
            TouchPopulationQueries();
            return relationship;
        }

        public SocialRelationship AdjustRelationshipAffinity(string firstSimId, string secondSimId, int delta)
        {
            RequireSim(firstSimId);
            RequireSim(secondSimId);
            SocialRelationship relationship = Relationships.GetOrCreate(firstSimId, secondSimId, CityTime);
            relationship.AdjustAffinity(delta);
            relationship.ChangedAt = CityTime;
            TouchPopulationQueries();
            return relationship;
        }

        public Sim GetPartner(string simId)
        {
            RequireSim(simId);
            List<SocialRelationship> relationships = Relationships.GetForSim(simId);
            for (int i = 0; i < relationships.Count; i++)
                if (relationships[i].HasKind(RelationshipKind.Partner))
                    return FindSim(relationships[i].GetOtherSimId(simId));
            return null;
        }

        public List<Sim> GetExPartners(string simId)
        {
            return GetRelatedSims(simId, RelationshipKind.ExPartner);
        }

        public List<Sim> GetFamily(string simId)
        {
            return GetRelatedSims(simId, RelationshipKind.Family);
        }

        public int GetEffectiveAffinity(string observerSimId, string targetSimId)
        {
            Sim observer = RequireSim(observerSimId);
            Sim target = RequireSim(targetSimId);
            return new FamilyDynamicsService(Relationships, SocialBiases).GetEffectiveAffinity(observer, target);
        }

        public Sim FindSim(string id)
        {
            EnsureRuntimeServices();
            Sim sim;
            return id != null && _simById.TryGetValue(id, out sim) ? sim : null;
        }

        /// <summary>
        /// Advances only Sims whose wake events are due. Long gaps are resolved at
        /// the destination time, rather than replaying every missed activity.
        /// </summary>
        public int AdvanceTo(double targetCityTime)
        {
            if (targetCityTime < CityTime) throw new ArgumentOutOfRangeException(nameof(targetCityTime));
            EnsureRuntimeServices();
            EnsureImmediateRelationships();
            CityTime = targetCityTime;
            _capacity.ReleaseExpired(CityTime);
            TownRuntime.Objects.ReleaseExpired(CityTime);
            List<OccupancyLease> expiredLeases = TownRuntime.OccupancyLeases.Expire(CityTime);
            for (int leaseIndex = 0; leaseIndex < expiredLeases.Count; leaseIndex++)
                ClearLeaseResidence(expiredLeases[leaseIndex]);
            List<JailMealObligation> jailMeals = TownRuntime.Detention.Advance(CityTime);
            for (int mealIndex = 0; mealIndex < jailMeals.Count; mealIndex++)
            {
                JailMealObligation meal = jailMeals[mealIndex];
                for (int simIndex = 0; simIndex < Sims.Count; simIndex++)
                    if (TownRuntime.Detention.IsEligibleGuard(Sims[simIndex], TownContent, meal.ManagedBy, CityTime))
                        Schedule(Sims[simIndex], CityTime);
            }

            int processed = 0;
            SimWakeEvent wake;
            while (_wakeEvents.TryPeek(out wake) && wake.Time <= CityTime)
            {
                _wakeEvents.Dequeue();
                Sim sim;
                if (!_simById.TryGetValue(wake.SimId, out sim) || wake.Sequence != sim.DecisionSequence) continue;
                ProcessSim(sim);
                processed++;
            }

            return processed;
        }

        public void WakeSim(string simId)
        {
            Sim sim = FindSim(simId);
            if (sim != null) Schedule(sim, CityTime);
        }

        private void ProcessSim(Sim sim)
        {
            if (sim.LifeStatus != LifeStatus.Alive) return;
            sim.GatewayKnowledge.ExpireIfDue(CityTime);
            AdvanceStats(sim, CityTime - sim.LastSimulatedAt);
            sim.LastSimulatedAt = CityTime;

            if (sim.Spatial.Kind == SpatialStateKind.InTransit && sim.Spatial.Transit != null)
            {
                if (!AdvanceTransit(sim))
                {
                    if (sim.LifeStatus == LifeStatus.Alive && sim.RouteFailure == null
                        && (sim.Activity == null || sim.Activity.Kind != ActivityKind.Incapacitated))
                        Schedule(sim, CityTime + 900d);
                    return;
                }
                if (sim.Spatial.Kind == SpatialStateKind.InTransit)
                {
                    Schedule(sim, sim.Spatial.Transit.ExpectedArrivalTime);
                    return;
                }
            }

            if (sim.Activity != null && sim.Activity.Status == ActivityStatus.Active && sim.Activity.EndTime > CityTime)
            {
                Schedule(sim, sim.Activity.EndTime);
                return;
            }

            if (sim.Activity != null)
            {
                if (sim.Activity.Status == ActivityStatus.Active) ApplyActivityEffects(sim, sim.Activity);
                else if (sim.Activity.Kind == ActivityKind.DeliverMeal)
                    TownRuntime.Detention.ReleaseClaim(sim.Activity.TargetId, sim.Id);
                if (!string.IsNullOrEmpty(sim.Activity.ReservationId)) _capacity.Release(sim.Activity.ReservationId);
                if (!string.IsNullOrEmpty(sim.Activity.ObjectSlot.Value))
                    TownRuntime.Objects.Release(sim.Activity.ObjectSlot, sim.Id);
                sim.Activity.Status = ActivityStatus.Completed;
            }
            SelectNextActivity(sim);
        }

        private bool AdvanceTransit(Sim sim)
        {
            TransitState transit = sim.Spatial.Transit;
            if (transit.Domain == TransitDomain.Composite) return AdvanceCompositeTransit(sim, transit);
            CellRoute route;
            if (!_routes.TryGet(transit.RouteId, out route))
            {
                transit.Status = TransitStatus.Stranded;
                return false;
            }

            if (route.TopologyVersion != Topology.Version)
            {
                transit.InvalidateForTopology(Topology.Version);
                if (!TryReroute(sim, route))
                {
                    transit.Status = TransitStatus.Stranded;
                    return false;
                }
                transit = sim.Spatial.Transit;
                _routes.TryGet(transit.RouteId, out route);
            }

            transit.Advance(route, CityTime);
            if (transit.Status != TransitStatus.Arrived) return true;

            sim.Spatial.Kind = SpatialStateKind.AtPlace;
            sim.Spatial.Address.Cell = transit.CurrentCell;
            TraversalCell arrivedCell;
            if (Topology.TryGetCell(transit.CurrentCell, out arrivedCell))
            {
                sim.Spatial.Address.Chunk = arrivedCell.Chunk;
                sim.Spatial.Address.Block = arrivedCell.Block;
                sim.Spatial.Address.Place = arrivedCell.Place;
            }
            sim.Spatial.Transit = null;
            if (sim.PendingActivity != null)
            {
                sim.Spatial.Address.Place = sim.PendingActivity.Place;
                sim.Spatial.Room = sim.PendingActivity.Room;
                sim.PendingActivity.Cell = transit.CurrentCell;
                ActivitySegment arrivedActivity = sim.PendingActivity;
                sim.PendingActivity = null;
                if (!StartPlannedActivity(sim, arrivedActivity))
                {
                    SelectNextActivity(sim);
                    return true;
                }
            }
            return true;
        }

        private bool AdvanceCompositeTransit(Sim sim, TransitState transit)
        {
            CompositeRoute route;
            if (!_compositeRoutes.TryGet(transit.RouteId, out route))
            {
                transit.Status = TransitStatus.Stranded;
                SetRouteFailure(sim, RouteFailureState.Stranded(
                    sim.PendingActivity == null ? new RoomId() : sim.PendingActivity.Room,
                    CityTime, "route:" + transit.RouteId.Value));
                return false;
            }
            if (CityTime >= transit.ExpectedArrivalTime)
            {
            for (int i = Math.Max(0, transit.SegmentIndex); i < route.Segments.Count; i++)
            {
                CompositeRouteSegment segment = route.Segments[i];
                if (segment.Domain != RouteDomain.GatewayCrossing) continue;
                BuildingGateway gateway;
                if (!TownRuntime.BuildingGraphs.TryGetGateway(segment.Gateway, out gateway)) continue;
                GatewayArc arc = FindArc(gateway, segment.GatewayArcId);
                if (arc == null) continue;
                GatewayRuntimeState state;
                TownRuntime.GatewayStates.TryGet(segment.Gateway, out state);
                var context = new GatewayAccessContext
                {
                    Sim = sim,
                    Mode = transit.Mode,
                    Emergency = sim.Activity != null && sim.Activity.Kind == ActivityKind.Flee,
                    AllowDangerousTraversal = segment.Dangerous
                };
                context.Capabilities.AddRange(transit.Capabilities);
                context.Claims.AddRange(sim.AccessClaims);
                GatewayAccessEvaluation evaluation = new GatewayAccessEvaluator().Evaluate(arc, gateway, state, context);
                if (evaluation.Allowed) continue;
                var failure = new RouteFailureState
                {
                    Kind = (evaluation.BlockingReasons & GatewayBlocker.PermissionDenied) != 0
                        ? RouteFailureKind.Confined : RouteFailureKind.Blocked,
                    DestinationRoom = route.DestinationRoom,
                    Gateway = segment.Gateway,
                    GatewayArcId = segment.GatewayArcId,
                    BlockedAt = CityTime
                };
                failure.Dependencies.Add("gateway:" + segment.Gateway.Value);
                failure.Dependencies.Add("building:" + segment.Building.Value);
                if ((evaluation.BlockingReasons & GatewayBlocker.PermissionDenied) != 0)
                    failure.Dependencies.Add("access");
                transit.Status = TransitStatus.Stranded;
                SetRouteFailure(sim, failure);
                return false;
            }

            for (int i = Math.Max(0, transit.SegmentIndex); i < route.Segments.Count; i++)
            {
                CompositeRouteSegment segment = route.Segments[i];
                if (segment.Domain != RouteDomain.GatewayCrossing || !segment.Dangerous) continue;
                BuildingGateway gateway;
                GatewayRuntimeState state;
                if (!TownRuntime.BuildingGraphs.TryGetGateway(segment.Gateway, out gateway)
                    ) continue;
                TownRuntime.GatewayStates.TryGet(segment.Gateway, out state);
                GatewayArc arc = FindArc(gateway, segment.GatewayArcId);
                if (arc == null || arc.Fall == null) continue;
                FallOutcome outcome = new GatewayFallResolver().Resolve(sim, segment.Gateway, arc.Fall,
                    sim.DecisionSequence, (long)(CityTime / 60d), RiskSettings);
                WorldEvents.Add(new WorldSimulationEvent
                {
                    Id = outcome.EventId, SimId = sim.Id, Gateway = segment.Gateway,
                    CityTime = CityTime, Damage = outcome.Damage,
                    PotentiallyFatal = outcome.PotentiallyFatal, Died = outcome.Died
                });
                if (outcome.Died || outcome.Incapacitated)
                {
                    sim.PendingActivity = null;
                    sim.Spatial.Kind = SpatialStateKind.AtPlace;
                    sim.Spatial.Room = segment.DestinationRoom;
                    sim.Spatial.Transit = null;
                    sim.StartActivity(new ActivitySegment
                    {
                        Kind = ActivityKind.Incapacitated,
                        Status = ActivityStatus.Active,
                        Place = sim.Spatial.Address.Place,
                        Room = sim.Spatial.Room,
                        StartTime = CityTime,
                        EndTime = double.MaxValue
                    }, CityTime);
                    return false;
                }
            }

            }

            transit.Advance(route, CityTime);
            if (transit.Status != TransitStatus.Arrived)
            {
                _routeDependencies.RegisterRoute(sim.Id, route.Id, route.Dependencies);
                return true;
            }
            sim.Spatial.Kind = SpatialStateKind.AtPlace;
            sim.Spatial.Room = route.DestinationRoom;
            sim.Spatial.Transit = null;
            _routeDependencies.Clear(sim.Id);
            sim.RouteFailure = null;
            if (sim.PendingActivity != null)
            {
                ActivitySegment activity = sim.PendingActivity;
                sim.PendingActivity = null;
                return StartPlannedActivity(sim, activity);
            }
            return true;
        }

        private bool TryReroute(Sim sim, CellRoute oldRoute)
        {
            RouteSegment last = oldRoute.Segments[oldRoute.Segments.Count - 1];
            var newId = new RouteId("route." + sim.Id + "." + (sim.DecisionSequence + 1).ToString());
            CellRoute replacement;
            var profile = new TravelProfile
            {
                Mode = sim.Spatial.Transit.Mode,
                SpeedMetresPerSecond = sim.Spatial.Transit.EffectiveSpeed
            };
            profile.Capabilities.AddRange(sim.Spatial.Transit.Capabilities);
            if (!_routePlanner.TryPlan(newId, sim.Spatial.Transit.CurrentCell, sim.Spatial.Transit.Position,
                    oldRoute.Destination, last.DestinationEntry, profile, out replacement)) return false;
            _routes.Store(replacement);
            sim.Spatial.Transit.Begin(replacement, profile, CityTime);
            sim.Spatial.InvalidateObservedPose();
            return true;
        }

        private void SelectNextActivity(Sim sim)
        {
            IReadOnlyList<ActivityCandidate> baseCandidates = _candidateSource.GetCandidates(sim, CityTime);
            var candidates = new List<ActivityCandidate>(baseCandidates);
            candidates.AddRange(new ObjectActivityCandidateSource(TownRuntime, Relationships)
                .GetCandidates(sim, CityTime));
            List<JailMealObligation> jailMeals = TownRuntime.Detention.GetPendingForGuard(sim, TownContent, CityTime);
            for (int i = 0; i < jailMeals.Count; i++)
            {
                RoomRuntimeInstance deliveryRoom = TownRuntime.FindRoom(jailMeals[i].DeliveryRoom);
                if (deliveryRoom == null) continue;
                candidates.Add(new ActivityCandidate
                {
                    Kind = ActivityKind.DeliverMeal,
                    Place = deliveryRoom.Place,
                    Room = jailMeals[i].DeliveryRoom,
                    TargetId = jailMeals[i].Id,
                    DurationSeconds = 300d,
                    BaseUtility = 20f,
                    WorkObligation = 100f
                });
            }
            var feasible = new List<ActivityCandidate>();
            CustodyAssignment activeCustody = TownRuntime.Detention.FindActive(sim.Id);
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i] != null
                    && (activeCustody == null || candidates[i].Kind != ActivityKind.Eat)
                    && (sim.RouteFailure == null || string.IsNullOrEmpty(candidates[i].Room.Value)
                        || candidates[i].Room == sim.Spatial.Room)
                    && (!ShouldRequireConcreteObject(candidates[i].Kind)
                        || !string.IsNullOrEmpty(candidates[i].Object.Value)
                        || !TownRuntime.Objects.HasAffordance(candidates[i].Room, candidates[i].Kind))
                    && IsCandidateFeasible(candidates[i])) feasible.Add(candidates[i]);
            ActivityCandidate selected = _activityPlanner.Choose(sim, feasible, (long)(CityTime / 900d), null);
            if (selected == null)
            {
                selected = new ActivityCandidate
                {
                    Kind = ActivityKind.Idle,
                    Place = sim.Spatial.Address.Place,
                    Room = sim.Spatial.Room,
                    Cell = sim.Spatial.Address.Cell,
                    DurationSeconds = 600d
                };
            }
            BeginCandidate(sim, selected);
        }

        private void BeginCandidate(Sim sim, ActivityCandidate candidate)
        {
            var activity = new ActivitySegment
            {
                Kind = candidate.Kind,
                Status = ActivityStatus.Planned,
                Place = candidate.Place,
                Room = candidate.Room,
                Anchor = candidate.Anchor,
                Cell = candidate.Cell,
                TargetId = candidate.TargetId,
                Object = candidate.Object,
                ObjectAffordance = candidate.ObjectAffordance,
                StartTime = CityTime,
                PlannedDurationSeconds = Math.Max(60d, candidate.DurationSeconds),
                RequiresFacility = candidate.RequiresFacility,
                Facility = candidate.Facility
            };
            activity.AccessClaims.AddRange(candidate.AccessClaims);

            if (candidate.Room != sim.Spatial.Room
                && !string.IsNullOrEmpty(candidate.Room.Value)
                && TryBeginInteriorTravel(sim, activity)) return;

            if (candidate.Cell != sim.Spatial.Address.Cell && !string.IsNullOrEmpty(candidate.Cell.Value))
            {
                CellRoute route;
                var profile = new TravelProfile();
                var routeId = new RouteId("route." + sim.Id + "." + (sim.DecisionSequence + 1).ToString());
                if (_routePlanner.TryPlan(routeId, sim.Spatial.Address.Cell, new NormalizedCellPosition(0.5f, 0.5f, 0.5f),
                        candidate.Cell, new NormalizedCellPosition(0.5f, 0.5f, 0.5f), profile, out route))
                {
                    _routes.Store(route);
                    sim.PendingActivity = activity;
                    var commute = new ActivitySegment
                    {
                        Kind = ActivityKind.Commute,
                        Place = sim.Spatial.Address.Place,
                        Room = sim.Spatial.Room,
                        Cell = sim.Spatial.Address.Cell,
                        StartTime = CityTime,
                        EndTime = CityTime + route.TotalDurationSeconds,
                        Route = route.Id
                    };
                    sim.StartActivity(commute, CityTime);
                    sim.Spatial.Kind = SpatialStateKind.InTransit;
                    sim.Spatial.Room = new RoomId();
                    sim.Spatial.Address.Anchor = new AnchorId();
                    sim.Spatial.Transit = new TransitState();
                    sim.Spatial.Transit.Begin(route, profile, CityTime);
                    Schedule(sim, sim.Spatial.Transit.ExpectedArrivalTime);
                    return;
                }
                activity.Kind = ActivityKind.Idle;
                activity.Status = ActivityStatus.Blocked;
                activity.Place = sim.Spatial.Address.Place;
                activity.Room = sim.Spatial.Room;
                activity.Cell = sim.Spatial.Address.Cell;
            }

            if (!StartPlannedActivity(sim, activity))
                throw new InvalidOperationException("A locally selected activity became unavailable before it could start.");
        }

        private bool TryBeginInteriorTravel(Sim sim, ActivitySegment activity)
        {
            RoomRuntimeInstance origin = TownRuntime.FindRoom(sim.Spatial.Room);
            RoomRuntimeInstance destination = TownRuntime.FindRoom(activity.Room);
            BuildingRoomGraph originGraph;
            BuildingRoomGraph destinationGraph;
            if (origin == null || destination == null
                || !TownRuntime.BuildingGraphs.TryGetGraph(origin.Building, out originGraph)
                || !TownRuntime.BuildingGraphs.TryGetGraph(destination.Building, out destinationGraph)
                || originGraph.Id != destinationGraph.Id) return false;
            var request = new CompositeRouteRequest
            {
                RouteId = new RouteId("route.interior." + sim.Id + "." + (sim.DecisionSequence + 1)),
                Sim = sim,
                Building = originGraph.Building,
                OriginRoom = origin.Id,
                DestinationRoom = destination.Id,
                CityTime = CityTime,
                Emergency = activity.Kind == ActivityKind.Flee,
                AllowDangerousTraversal = false,
                RiskSettings = RiskSettings
            };
            CompositeRoute route;
            RouteFailureState failure;
            bool planned = _compositeRoutePlanner.TryPlanInterior(request, out route, out failure);
            if (!planned && activity.Kind == ActivityKind.Flee)
            {
                request.AllowDangerousTraversal = true;
                planned = _compositeRoutePlanner.TryPlanInterior(request, out route, out failure);
            }
            if (!planned)
            {
                failure.RequestedActivity = activity.Kind;
                SetRouteFailure(sim, failure);
                StartPlannedActivity(sim, new ActivitySegment
                {
                    Kind = ActivityKind.Idle,
                    Status = ActivityStatus.Planned,
                    Place = origin.Place,
                    Room = origin.Id,
                    Anchor = origin.DefaultAnchor,
                    PlannedDurationSeconds = 600d
                });
                return true;
            }
            _compositeRoutes.Store(route);
            _routeDependencies.RegisterRoute(sim.Id, route.Id, route.Dependencies);
            sim.PendingActivity = activity;
            var commute = new ActivitySegment
            {
                Kind = ActivityKind.Commute,
                Place = sim.Spatial.Address.Place,
                Room = sim.Spatial.Room,
                StartTime = CityTime,
                EndTime = CityTime + route.TotalDurationSeconds,
                Route = route.Id
            };
            sim.StartActivity(commute, CityTime);
            sim.Spatial.Kind = SpatialStateKind.InTransit;
            sim.Spatial.Address.Anchor = new AnchorId();
            sim.Spatial.Anchor = new AnchorId();
            sim.Spatial.Transit = new TransitState();
            sim.Spatial.Transit.Begin(route, request.Profile, CityTime);
            Schedule(sim, sim.Spatial.Transit.ExpectedArrivalTime);
            return true;
        }

        private void SetRouteFailure(Sim sim, RouteFailureState failure)
        {
            sim.RouteFailure = failure;
            _routeDependencies.Register(sim.Id, failure);
        }

        private static GatewayArc FindArc(BuildingGateway gateway, string arcId)
        {
            for (int i = 0; i < gateway.Arcs.Count; i++)
                if (gateway.Arcs[i] != null && string.Equals(gateway.Arcs[i].Id, arcId, StringComparison.Ordinal))
                    return gateway.Arcs[i];
            return null;
        }

        private bool IsCandidateFeasible(ActivityCandidate candidate)
        {
            FacilityKind facility;
            return !TryGetFacility(candidate, out facility)
                || _capacity.HasAvailability(new CapacityKey(candidate.Place, facility));
        }

        private bool StartPlannedActivity(Sim sim, ActivitySegment activity)
        {
            double duration = Math.Max(60d, activity.PlannedDurationSeconds);
            bool claimedJailMeal = activity.Kind == ActivityKind.DeliverMeal;
            if (claimedJailMeal && !TownRuntime.Detention.TryClaim(activity.TargetId, sim.Id, CityTime))
                return false;
            CapacityReservation capacityReservation = null;
            FacilityKind facility;
            if (TryGetFacility(activity, out facility))
            {
                if (!_capacity.TryReserve(
                        new CapacityKey(activity.Place, facility), sim.Id, CityTime + duration, out capacityReservation))
                {
                    if (claimedJailMeal) TownRuntime.Detention.ReleaseClaim(activity.TargetId, sim.Id);
                    return false;
                }
                activity.ReservationId = capacityReservation.Id;
            }

            if (!string.IsNullOrEmpty(activity.Object.Value))
            {
                ObjectAffordanceSlot slot;
                ObjectUseEvaluation evaluation;
                if (!TryReserveObjectUse(
                        sim.Id, activity.Object, activity.ObjectAffordance, CityTime + duration,
                        activity.AccessClaims, out slot, out evaluation))
                {
                    if (capacityReservation != null) _capacity.Release(capacityReservation.Id);
                    if (claimedJailMeal) TownRuntime.Detention.ReleaseClaim(activity.TargetId, sim.Id);
                    activity.ReservationId = null;
                    return false;
                }
                activity.ObjectSlot = slot.Id;
            }

            activity.StartTime = CityTime;
            activity.EndTime = CityTime + duration;
            sim.Spatial.Address.Place = activity.Place;
            sim.Spatial.Address.Cell = activity.Cell;
            sim.Spatial.Address.Anchor = new AnchorId();
            sim.Spatial.Room = activity.Room;
            sim.Spatial.Anchor = activity.Anchor;
            sim.StartActivity(activity, CityTime);
            Schedule(sim, activity.EndTime);
            return true;
        }

        private static bool ShouldRequireConcreteObject(ActivityKind kind)
        {
            return kind == ActivityKind.Sleep || kind == ActivityKind.Toilet
                || kind == ActivityKind.Hygiene || kind == ActivityKind.Eat
                || kind == ActivityKind.Drink || kind == ActivityKind.Relax;
        }

        private void Schedule(Sim sim, double time)
        {
            sim.NextWakeTime = time;
            _wakeEvents.Enqueue(new SimWakeEvent { SimId = sim.Id, Time = time, Sequence = sim.DecisionSequence });
        }

        private void EnsureRuntimeServices()
        {
            TownRuntime.OccupancyLeases.Configure(TownRuntime.Objects);
            TownRuntime.Detention.Bind(TownRuntime.Objects);
            if (_simById == null || _simById.Count != Sims.Count)
            {
                if (_simById == null) _relationshipContextsDirty = true;
                _simById = new Dictionary<string, Sim>(StringComparer.Ordinal);
                for (int i = 0; i < Sims.Count; i++)
                    if (Sims[i] != null) _simById[Sims[i].Id] = Sims[i];
            }
            if (_wakeEvents == null)
            {
                _wakeEvents = new SimulationEventQueue();
                for (int i = 0; i < Sims.Count; i++)
                    if (Sims[i] != null && Sims[i].LifeStatus == LifeStatus.Alive)
                        Schedule(Sims[i], Math.Max(CityTime, Sims[i].NextWakeTime));
            }
            if (_travelTime == null) _travelTime = new TravelTimeCalculator();
            if (_routes == null) _routes = RouteData;
            if (_compositeRoutes == null) _compositeRoutes = CompositeRouteData;
            if (_routeDependencies == null) _routeDependencies = new RouteDependencyIndex();
            if (_activityPlanner == null) _activityPlanner = new DeterministicActivityPlanner();
            if (_capacity == null) _capacity = CapacityData;
            if (_candidateSource == null) _candidateSource = new BasicActivityCandidateSource();
            if (_routePlanner == null) _routePlanner = new CellRoutePlanner(Topology, _travelTime);
            if (_compositeRoutePlanner == null) _compositeRoutePlanner = new CompositeRoutePlanner(
                TownRuntime.BuildingGraphs, TownRuntime.GatewayStates, new GatewayAccessEvaluator(),
                new GatewayKnowledgeService(), _travelTime);
            if (_populationStore == null && PopulationData != null)
                _populationStore = new PopulationStore(PopulationData);
        }

        private static bool TryGetFacility(ActivityCandidate candidate, out FacilityKind facility)
        {
            if (candidate.RequiresFacility) { facility = candidate.Facility; return true; }
            return TryGetDefaultFacility(candidate.Kind, out facility);
        }

        private static bool TryGetFacility(ActivitySegment activity, out FacilityKind facility)
        {
            if (activity.RequiresFacility) { facility = activity.Facility; return true; }
            return TryGetDefaultFacility(activity.Kind, out facility);
        }

        private static bool TryGetDefaultFacility(ActivityKind kind, out FacilityKind facility)
        {
            switch (kind)
            {
                case ActivityKind.Sleep: facility = FacilityKind.Bed; return true;
                case ActivityKind.Toilet: facility = FacilityKind.Toilet; return true;
                case ActivityKind.Hygiene: facility = FacilityKind.Shower; return true;
                default: facility = FacilityKind.Generic; return false;
            }
        }

        private void AdvanceStats(Sim sim, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0d) return;
            float hours = (float)(elapsedSeconds / 3600d);
            sim.Stats.Adjust(SimStat.Hunger, 4f * hours);
            sim.Stats.Adjust(SimStat.Thirst, 6f * hours);
            sim.Stats.Adjust(SimStat.Bladder, 5f * hours);
            sim.Stats.Adjust(SimStat.Energy, -3f * hours);
            sim.Stats.Adjust(SimStat.Hygiene, -1f * hours);
            sim.Stats.Adjust(SimStat.Social, -1f * hours);
            sim.Stats.Adjust(SimStat.Fun, -1f * hours);
            EconomicPressureModel.Advance(sim, elapsedSeconds, EconomicPressure);
        }

        private Sim RequireSim(string simId)
        {
            Sim sim = FindSim(simId);
            if (sim == null) throw new InvalidOperationException("The Sim '" + simId + "' does not exist in this city.");
            return sim;
        }

        private void EnsureImmediateRelationships()
        {
            if (_relationshipContextsDirty) RefreshImmediateRelationships();
        }

        private void EnsureNoOtherMarriage(string simId, string intendedSpouseId)
        {
            List<SocialRelationship> relationships = Relationships.GetForSim(simId);
            for (int i = 0; i < relationships.Count; i++)
            {
                SocialRelationship relationship = relationships[i];
                if (!relationship.HasKind(RelationshipKind.Partner)
                    || relationship.GetFamilyRole(simId) != FamilyRole.Spouse
                    || relationship.Connects(simId, intendedSpouseId)) continue;
                throw new InvalidOperationException(
                    "The Sim is already married; apply an explicit scripted divorce first.");
            }
        }

        private void TouchPopulationQueries()
        {
            if (_populationStore != null) _populationStore.Dispatch(PopulationAction.Refresh());
        }

        private List<Sim> GetRelatedSims(string simId, RelationshipKind kind)
        {
            RequireSim(simId);
            var result = new List<Sim>();
            List<SocialRelationship> relationships = Relationships.GetForSim(simId);
            for (int i = 0; i < relationships.Count; i++)
            {
                if (!relationships[i].HasKind(kind)) continue;
                Sim related = FindSim(relationships[i].GetOtherSimId(simId));
                if (related != null) result.Add(related);
            }
            return result;
        }

        private void ApplyActivityEffects(Sim sim, ActivitySegment activity)
        {
            float hours = (float)(Math.Max(0d, activity.EndTime - activity.StartTime) / 3600d);
            switch (activity.Kind)
            {
                case ActivityKind.Eat:
                    sim.Stats.Adjust(SimStat.Hunger, -60f);
                    break;
                case ActivityKind.Drink:
                    sim.Stats.Adjust(SimStat.Thirst, -70f);
                    break;
                case ActivityKind.Toilet:
                    sim.Stats.Set(SimStat.Bladder, 0f);
                    break;
                case ActivityKind.Sleep:
                    sim.Stats.Adjust(SimStat.Energy, 15f * hours);
                    break;
                case ActivityKind.Hygiene:
                    sim.Stats.Adjust(SimStat.Hygiene, 60f);
                    break;
                case ActivityKind.Relax:
                    sim.Stats.Adjust(SimStat.Fun, 40f);
                    sim.Stats.Adjust(SimStat.Comfort, 20f);
                    sim.Stats.Adjust(SimStat.Stress, -20f);
                    sim.Stats.Adjust(SimStat.Anxiety, -10f);
                    break;
                case ActivityKind.Play:
                    sim.Stats.Adjust(SimStat.Fun, 50f);
                    sim.Stats.Adjust(SimStat.Social, 15f);
                    break;
                case ActivityKind.Exercise:
                    sim.Stats.Adjust(SimStat.Fun, 30f);
                    sim.Stats.Adjust(SimStat.Energy, -10f * hours);
                    sim.Stats.Adjust(SimStat.Health, 2f * hours);
                    break;
                case ActivityKind.Mourn:
                    sim.Stats.Adjust(SimStat.Stress, -5f);
                    break;
                case ActivityKind.Socialize:
                    sim.Stats.Adjust(SimStat.Social, 50f);
                    break;
                case ActivityKind.Healthcare:
                    sim.Stats.Adjust(SimStat.Health, 40f);
                    sim.Stats.Adjust(SimStat.Poison, -30f);
                    break;
                case ActivityKind.DeliverMeal:
                    JailMealObligation meal = TownRuntime.Detention.Complete(activity.TargetId, sim.Id, CityTime);
                    Sim prisoner = meal == null ? null : FindSim(meal.PrisonerSimId);
                    if (prisoner != null) prisoner.Stats.Adjust(SimStat.Hunger, -60f);
                    break;
                case ActivityKind.WorkTask:
                    sim.Stats.Adjust(SimStat.Money, sim.Employment.HourlyPay * hours);
                    sim.Stats.Adjust(SimStat.Stress, 5f);
                    sim.Employment.Performance++;
                    break;
                case ActivityKind.WorkBreak:
                    sim.Stats.Adjust(SimStat.Stress, -10f);
                    sim.Stats.Adjust(SimStat.Energy, 5f);
                    break;
            }
        }
    }
}
