using System;
using System.Collections.Generic;
using Syms.Simulation.People;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Population
{
    /// <summary>
    /// Read-only selectors over serializable population state. Indexes are
    /// rebuilt lazily only when the reducer increments the state revision.
    /// </summary>
    public sealed class PopulationSelectors
    {
        private readonly PopulationState _state;
        private long _indexedRevision = long.MinValue;
        private Dictionary<string, Sim> _simById;
        private Dictionary<string, HouseholdRecord> _householdBySim;
        private Dictionary<string, List<Sim>> _householdMembers;
        private Dictionary<string, EmploymentAssignmentRecord> _employmentBySim;
        private Dictionary<PlaceId, List<Sim>> _workplaceMembers;
        private Dictionary<BlockId, List<Sim>> _blockResidents;
        private Dictionary<string, List<SocialRelationship>> _relationshipsBySim;

        public PopulationSelectors(PopulationState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public Sim SimById(string simId)
        {
            EnsureIndexes();
            Sim sim;
            return simId != null && _simById.TryGetValue(simId, out sim) ? sim : null;
        }

        public Sim SimById(SimUid simId) { return SimById(simId.Value); }

        public HouseholdRecord HouseholdForSim(string simId)
        {
            EnsureIndexes();
            HouseholdRecord household;
            return simId != null && _householdBySim.TryGetValue(simId, out household) ? household : null;
        }

        public IReadOnlyList<Sim> HouseholdMembers(HouseholdUid householdId)
        {
            EnsureIndexes();
            List<Sim> members;
            return _householdMembers.TryGetValue(householdId.Value ?? string.Empty, out members)
                ? members : (IReadOnlyList<Sim>)Array.Empty<Sim>();
        }

        public EmploymentAssignmentRecord EmploymentForSim(string simId)
        {
            EnsureIndexes();
            EmploymentAssignmentRecord employment;
            return simId != null && _employmentBySim.TryGetValue(simId, out employment) ? employment : null;
        }

        public IReadOnlyList<Sim> WorkplaceMembers(PlaceId workplace)
        {
            EnsureIndexes();
            List<Sim> members;
            return _workplaceMembers.TryGetValue(workplace, out members)
                ? members : (IReadOnlyList<Sim>)Array.Empty<Sim>();
        }

        public IReadOnlyList<Sim> BlockResidents(BlockId block)
        {
            EnsureIndexes();
            List<Sim> residents;
            return _blockResidents.TryGetValue(block, out residents)
                ? residents : (IReadOnlyList<Sim>)Array.Empty<Sim>();
        }

        public IReadOnlyList<SocialRelationship> RelationshipsForSim(string simId)
        {
            EnsureIndexes();
            List<SocialRelationship> relationships;
            return simId != null && _relationshipsBySim.TryGetValue(simId, out relationships)
                ? relationships : (IReadOnlyList<SocialRelationship>)Array.Empty<SocialRelationship>();
        }

        private void EnsureIndexes()
        {
            if (_indexedRevision == _state.Revision) return;
            _simById = new Dictionary<string, Sim>(StringComparer.Ordinal);
            _householdBySim = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);
            _householdMembers = new Dictionary<string, List<Sim>>(StringComparer.Ordinal);
            _employmentBySim = new Dictionary<string, EmploymentAssignmentRecord>(StringComparer.Ordinal);
            _workplaceMembers = new Dictionary<PlaceId, List<Sim>>();
            _blockResidents = new Dictionary<BlockId, List<Sim>>();
            _relationshipsBySim = new Dictionary<string, List<SocialRelationship>>(StringComparer.Ordinal);
            var householdById = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);

            for (int i = 0; i < _state.Sims.Count; i++)
            {
                Sim sim = _state.Sims[i];
                if (sim != null && !string.IsNullOrWhiteSpace(sim.Id)) _simById[sim.Id] = sim;
            }

            for (int i = 0; i < _state.Households.Count; i++)
            {
                HouseholdRecord household = _state.Households[i];
                if (household == null) continue;
                string householdId = household.Id.Value ?? string.Empty;
                householdById[householdId] = household;
                _householdMembers[householdId] = new List<Sim>(household.MemberSimIds.Count);
            }

            for (int i = 0; i < _state.Sims.Count; i++)
            {
                Sim sim = _state.Sims[i];
                if (sim == null || sim.LifeStatus != LifeStatus.Alive || sim.Residence == null
                    || !sim.Residence.HasHousehold) continue;
                HouseholdRecord household;
                if (!householdById.TryGetValue(sim.Residence.HouseholdId, out household)) continue;
                _householdMembers[household.Id.Value ?? string.Empty].Add(sim);
                _householdBySim[sim.Id] = household;
                Add(_blockResidents, sim.Residence.HomeAddress.Block, sim);
            }

            for (int i = 0; i < _state.EmploymentAssignments.Count; i++)
            {
                EmploymentAssignmentRecord employment = _state.EmploymentAssignments[i];
                if (employment == null) continue;
                _employmentBySim[employment.SimId] = employment;
                Sim sim;
                if (_simById.TryGetValue(employment.SimId, out sim)
                    && sim.LifeStatus == LifeStatus.Alive)
                    Add(_workplaceMembers, employment.Workplace, sim);
            }

            for (int i = 0; i < _state.Relationships.Relationships.Count; i++)
            {
                SocialRelationship relationship = _state.Relationships.Relationships[i];
                if (relationship == null) continue;
                Add(_relationshipsBySim, relationship.FirstSimId, relationship);
                Add(_relationshipsBySim, relationship.SecondSimId, relationship);
            }

            _indexedRevision = _state.Revision;
        }

        private static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> index, TKey key, TValue value)
        {
            List<TValue> values;
            if (!index.TryGetValue(key, out values))
            {
                values = new List<TValue>();
                index.Add(key, values);
            }
            values.Add(value);
        }
    }
}
