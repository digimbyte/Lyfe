using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Population
{
    [Serializable]
    public sealed class HouseholdRecord
    {
        public HouseholdUid Id;
        public string StableKey;
        public string FamilyName;
        public HouseholdKind Kind;
        public BuildingId Building;
        public BuildingId ManagedBy;
        public WorldAddress Address;
        public int Capacity;
        public int PlannedResidents;
        public List<string> MemberSimIds = new List<string>();
    }

    [Serializable]
    public sealed class EmploymentAssignmentRecord
    {
        public string SimId;
        public RoleSlotId RoleSlot;
        public BuildingId Building;
        public PlaceId Workplace;
    }

    [Serializable]
    public sealed class PopulationState
    {
        public string BootstrapId;
        public long Seed;
        public long Revision;
        public List<Sim> Sims = new List<Sim>();
        public List<HouseholdRecord> Households = new List<HouseholdRecord>();
        public List<EmploymentAssignmentRecord> EmploymentAssignments = new List<EmploymentAssignmentRecord>();
        public RelationshipGraph Relationships = new RelationshipGraph();

        [NonSerialized] private Dictionary<string, Sim> _simById;
        [NonSerialized] private Dictionary<string, HouseholdRecord> _householdById;
        [NonSerialized] private HashSet<string> _employedSimIds;
        [NonSerialized] private HashSet<string> _assignedRoleIds;
        [NonSerialized] private Dictionary<string, HouseholdRecord> _householdByMember;

        internal void EnsureMutationIndexes()
        {
            if (_simById != null && _simById.Count == Sims.Count
                && _householdById != null && _householdById.Count == Households.Count
                && _employedSimIds != null && _employedSimIds.Count == EmploymentAssignments.Count) return;
            _simById = new Dictionary<string, Sim>(StringComparer.Ordinal);
            _householdById = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);
            _employedSimIds = new HashSet<string>(StringComparer.Ordinal);
            _assignedRoleIds = new HashSet<string>(StringComparer.Ordinal);
            _householdByMember = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);
            for (int i = 0; i < Sims.Count; i++)
                if (Sims[i] != null && !string.IsNullOrWhiteSpace(Sims[i].Id)) _simById[Sims[i].Id] = Sims[i];
            for (int i = 0; i < Households.Count; i++)
            {
                HouseholdRecord household = Households[i];
                if (household == null) continue;
                _householdById[household.Id.Value ?? string.Empty] = household;
                for (int member = 0; member < household.MemberSimIds.Count; member++)
                    _householdByMember[household.MemberSimIds[member]] = household;
            }
            for (int i = 0; i < EmploymentAssignments.Count; i++)
            {
                EmploymentAssignmentRecord employment = EmploymentAssignments[i];
                if (employment == null) continue;
                _employedSimIds.Add(employment.SimId);
                _assignedRoleIds.Add(employment.RoleSlot.Value ?? string.Empty);
            }
        }

        internal bool ContainsSim(string id) { EnsureMutationIndexes(); return _simById.ContainsKey(id); }
        internal bool ContainsHousehold(string id) { EnsureMutationIndexes(); return _householdById.ContainsKey(id); }
        internal HouseholdRecord FindHousehold(string id)
        {
            EnsureMutationIndexes();
            HouseholdRecord value;
            return _householdById.TryGetValue(id, out value) ? value : null;
        }
        internal bool HasHousehold(string simId) { EnsureMutationIndexes(); return _householdByMember.ContainsKey(simId); }
        internal bool IsEmployed(string simId) { EnsureMutationIndexes(); return _employedSimIds.Contains(simId); }
        internal bool IsRoleAssigned(string roleId) { EnsureMutationIndexes(); return _assignedRoleIds.Contains(roleId); }
        internal void Track(Sim sim) { EnsureMutationIndexes(); _simById.Add(sim.Id, sim); }
        internal void Track(HouseholdRecord household)
        {
            EnsureMutationIndexes();
            _householdById.Add(household.Id.Value ?? string.Empty, household);
        }
        internal void TrackMember(string simId, HouseholdRecord household)
        {
            EnsureMutationIndexes();
            _householdByMember.Add(simId, household);
        }
        internal void Track(EmploymentAssignmentRecord employment)
        {
            EnsureMutationIndexes();
            _employedSimIds.Add(employment.SimId);
            _assignedRoleIds.Add(employment.RoleSlot.Value ?? string.Empty);
        }
    }

    public enum PopulationActionKind
    {
        AddSim,
        AddHousehold,
        AssignHouseholdMember,
        AssignEmployment,
        UpsertRelationship,
        RefreshIndexes
    }

    [Serializable]
    public sealed class PopulationAction
    {
        public PopulationActionKind Kind;
        public Sim Sim;
        public HouseholdRecord Household;
        public HouseholdUid HouseholdId;
        public string MemberSimId;
        public EmploymentAssignmentRecord Employment;
        public SocialRelationship Relationship;

        public static PopulationAction Add(Sim sim)
        {
            return new PopulationAction { Kind = PopulationActionKind.AddSim, Sim = sim };
        }

        public static PopulationAction Add(HouseholdRecord household)
        {
            return new PopulationAction { Kind = PopulationActionKind.AddHousehold, Household = household };
        }

        public static PopulationAction Assign(EmploymentAssignmentRecord employment)
        {
            return new PopulationAction { Kind = PopulationActionKind.AssignEmployment, Employment = employment };
        }

        public static PopulationAction Assign(HouseholdUid householdId, string simId)
        {
            return new PopulationAction
            {
                Kind = PopulationActionKind.AssignHouseholdMember,
                HouseholdId = householdId,
                MemberSimId = simId
            };
        }

        public static PopulationAction Upsert(SocialRelationship relationship)
        {
            return new PopulationAction { Kind = PopulationActionKind.UpsertRelationship, Relationship = relationship };
        }

        public static PopulationAction Refresh()
        {
            return new PopulationAction { Kind = PopulationActionKind.RefreshIndexes };
        }
    }

    /// <summary>
    /// Single mutation boundary for population state. It mutates a serializable
    /// state in place to avoid copying thousands of Sims, while preserving Redux's
    /// action -> reducer -> revision flow.
    /// </summary>
    public static class PopulationReducer
    {
        public static void Reduce(PopulationState state, PopulationAction action)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (action == null) throw new ArgumentNullException(nameof(action));
            switch (action.Kind)
            {
                case PopulationActionKind.AddSim:
                    AddSim(state, action.Sim);
                    break;
                case PopulationActionKind.AddHousehold:
                    AddHousehold(state, action.Household);
                    break;
                case PopulationActionKind.AssignHouseholdMember:
                    AssignHouseholdMember(state, action.HouseholdId, action.MemberSimId);
                    break;
                case PopulationActionKind.AssignEmployment:
                    AssignEmployment(state, action.Employment);
                    break;
                case PopulationActionKind.UpsertRelationship:
                    UpsertRelationship(state, action.Relationship);
                    break;
                case PopulationActionKind.RefreshIndexes:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action.Kind));
            }
            state.Revision++;
        }

        private static void AddSim(PopulationState state, Sim sim)
        {
            if (sim == null || string.IsNullOrWhiteSpace(sim.Id))
                throw new InvalidOperationException("AddSim requires a Sim with a stable ID.");
            if (state.ContainsSim(sim.Id)) throw new InvalidOperationException("Duplicate Sim ID '" + sim.Id + "'.");
            state.Track(sim);
            state.Sims.Add(sim);
        }

        private static void AddHousehold(PopulationState state, HouseholdRecord household)
        {
            if (household == null || string.IsNullOrWhiteSpace(household.Id.Value))
                throw new InvalidOperationException("AddHousehold requires a stable household ID.");
            if (household.Capacity <= 0 || household.PlannedResidents < 0
                || household.PlannedResidents > household.Capacity)
                throw new InvalidOperationException("Household occupancy must fit authored capacity.");
            if (state.ContainsHousehold(household.Id.Value ?? string.Empty))
                throw new InvalidOperationException("Duplicate household ID '" + household.Id.Value + "'.");
            state.Track(household);
            state.Households.Add(household);
        }

        private static void AssignEmployment(PopulationState state, EmploymentAssignmentRecord employment)
        {
            if (employment == null || string.IsNullOrWhiteSpace(employment.SimId)
                || string.IsNullOrWhiteSpace(employment.RoleSlot.Value))
                throw new InvalidOperationException("AssignEmployment requires Sim and role-slot IDs.");
            if (!state.ContainsSim(employment.SimId))
                throw new InvalidOperationException("Employment references a missing Sim.");
            if (state.IsEmployed(employment.SimId))
                throw new InvalidOperationException("A Sim cannot hold two bootstrap jobs.");
            if (state.IsRoleAssigned(employment.RoleSlot.Value ?? string.Empty))
                throw new InvalidOperationException("A role slot cannot have two bootstrap workers.");
            state.Track(employment);
            state.EmploymentAssignments.Add(employment);
        }

        private static void AssignHouseholdMember(
            PopulationState state,
            HouseholdUid householdId,
            string simId)
        {
            HouseholdRecord household = state.FindHousehold(householdId.Value ?? string.Empty);
            if (household == null) throw new InvalidOperationException("Household does not exist.");
            if (!state.ContainsSim(simId)) throw new InvalidOperationException("Household member Sim does not exist.");
            if (household.MemberSimIds.Contains(simId)) return;
            if (state.HasHousehold(simId)) throw new InvalidOperationException("A Sim cannot belong to two households.");
            if (household.MemberSimIds.Count >= household.Capacity)
                throw new InvalidOperationException("Household exceeds authored resident capacity.");
            state.TrackMember(simId, household);
            household.MemberSimIds.Add(simId);
        }

        private static void UpsertRelationship(PopulationState state, SocialRelationship relationship)
        {
            if (relationship == null) throw new InvalidOperationException("A relationship payload is required.");
            if (!state.ContainsSim(relationship.FirstSimId) || !state.ContainsSim(relationship.SecondSimId))
                throw new InvalidOperationException("Relationship references a missing Sim.");
            state.Relationships.Upsert(relationship);
        }
    }

    public sealed class PopulationStore
    {
        private readonly PopulationState _state;
        private readonly PopulationSelectors _selectors;
        public PopulationState State { get { return _state; } }
        public PopulationSelectors Select { get { return _selectors; } }

        public PopulationStore(PopulationState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _selectors = new PopulationSelectors(_state);
        }

        public void Dispatch(PopulationAction action)
        {
            PopulationReducer.Reduce(_state, action);
        }
    }
}
