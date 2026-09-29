using System;
using System.Collections.Generic;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Social
{
    [Serializable]
    public sealed class SocialCircleRefreshResult
    {
        public int HouseholdPairs;
        public int RoommatePairs;
        public int ColleaguePairs;
        public int NeighbourPairs;
        public int RelationshipCount;
    }

    /// <summary>
    /// Materializes only immediate social connections. Grouping avoids comparing
    /// every Sim with every other Sim in the town.
    /// </summary>
    public sealed class ImmediateSocialCircleService
    {
        private static readonly RelationshipKind[] DerivedKinds =
        {
            RelationshipKind.Household,
            RelationshipKind.Roommate,
            RelationshipKind.Colleague,
            RelationshipKind.Neighbour
        };

        public SocialCircleRefreshResult Refresh(
            IReadOnlyList<Sim> sims,
            RelationshipGraph graph,
            double cityTime)
        {
            if (sims == null) throw new ArgumentNullException(nameof(sims));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            graph.ClearDerivedKinds(DerivedKinds, cityTime);
            var result = new SocialCircleRefreshResult();
            var households = new Dictionary<string, List<Sim>>(StringComparer.Ordinal);
            var workplaces = new Dictionary<PlaceId, List<Sim>>();
            var neighbourhoods = new Dictionary<BlockId, List<Sim>>();

            for (int i = 0; i < sims.Count; i++)
            {
                Sim sim = sims[i];
                if (sim == null || sim.LifeStatus != LifeStatus.Alive) continue;
                if (sim.Residence != null && sim.Residence.HasHousehold)
                    AddToGroup(households, sim.Residence.EffectiveHouseholdId, sim);
                if (sim.Residence != null && sim.Residence.HasNeighbourhood)
                    AddToGroup(neighbourhoods, sim.Residence.HomeAddress.Block, sim);
                if (sim.Employment != null && sim.Employment.IsEmployed
                    && !string.IsNullOrWhiteSpace(sim.Employment.Workplace.Value))
                    AddToGroup(workplaces, sim.Employment.Workplace, sim);
            }

            foreach (List<Sim> group in households.Values)
                AddHouseholdPairs(group, graph, cityTime, result);
            foreach (List<Sim> group in workplaces.Values)
                result.ColleaguePairs += AddPairs(group, graph, RelationshipKind.Colleague, cityTime, null);
            foreach (List<Sim> group in neighbourhoods.Values)
                result.NeighbourPairs += AddPairs(group, graph, RelationshipKind.Neighbour, cityTime, DifferentHouseholds);

            result.RelationshipCount = graph.Relationships.Count;
            return result;
        }

        private static void AddHouseholdPairs(
            List<Sim> group,
            RelationshipGraph graph,
            double cityTime,
            SocialCircleRefreshResult result)
        {
            for (int first = 0; first < group.Count; first++)
            {
                for (int second = first + 1; second < group.Count; second++)
                {
                    SocialRelationship relationship = graph.AddKind(
                        group[first].Id, group[second].Id, RelationshipKind.Household, cityTime);
                    result.HouseholdPairs++;
                    if (relationship.HasKind(RelationshipKind.Family)
                        || relationship.HasKind(RelationshipKind.Partner)) continue;
                    graph.AddKind(group[first].Id, group[second].Id, RelationshipKind.Roommate, cityTime);
                    result.RoommatePairs++;
                }
            }
        }

        private static int AddPairs(
            List<Sim> group,
            RelationshipGraph graph,
            RelationshipKind kind,
            double cityTime,
            Func<Sim, Sim, bool> predicate)
        {
            int count = 0;
            for (int first = 0; first < group.Count; first++)
            {
                for (int second = first + 1; second < group.Count; second++)
                {
                    if (predicate != null && !predicate(group[first], group[second])) continue;
                    graph.AddKind(group[first].Id, group[second].Id, kind, cityTime);
                    count++;
                }
            }
            return count;
        }

        private static bool DifferentHouseholds(Sim first, Sim second)
        {
            string firstHousehold = first.Residence == null ? null : first.Residence.EffectiveHouseholdId;
            string secondHousehold = second.Residence == null ? null : second.Residence.EffectiveHouseholdId;
            return string.IsNullOrWhiteSpace(firstHousehold)
                || string.IsNullOrWhiteSpace(secondHousehold)
                || !string.Equals(firstHousehold, secondHousehold, StringComparison.Ordinal);
        }

        private static void AddToGroup<TKey>(Dictionary<TKey, List<Sim>> groups, TKey key, Sim sim)
        {
            List<Sim> group;
            if (!groups.TryGetValue(key, out group))
            {
                group = new List<Sim>();
                groups.Add(key, group);
            }
            group.Add(sim);
        }
    }
}
