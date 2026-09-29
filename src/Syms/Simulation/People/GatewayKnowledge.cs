using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.World;

namespace Syms.Simulation.People
{
    public enum GatewayKnowledgeKind { Unknown, Remembered, IntrinsicResidence, IntrinsicWorkplace }

    [Serializable]
    public sealed class ObservedGatewayFact
    {
        public GatewayId Gateway;
        public string ArcId;
        public GatewayBlocker Blockers;
        public List<string> Requirements = new List<string>();
        public GatewayTraversalOutcome Outcome;
        public double ObservedAt;
        public double LastAccessedAt;
        public long GatewayRevision;
    }

    [Serializable]
    public sealed class GatewayKnowledgeState
    {
        public const int MaximumFacts = 20;
        public List<ObservedGatewayFact> Facts = new List<ObservedGatewayFact>();
        public double ExpireAt;

        public void Observe(ObservedGatewayFact fact, double cityTime)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            int found = FindIndex(fact.Gateway, fact.ArcId);
            if (found >= 0) Facts.RemoveAt(found);
            fact.ObservedAt = cityTime;
            fact.LastAccessedAt = cityTime;
            Facts.Add(fact);
            while (Facts.Count > MaximumFacts) Facts.RemoveAt(FindOldest());
        }

        public bool TryRecall(GatewayId gateway, string arcId, long currentRevision,
            double cityTime, out ObservedGatewayFact fact)
        {
            ExpireIfDue(cityTime);
            int index = FindIndex(gateway, arcId);
            if (index < 0 || Facts[index].GatewayRevision != currentRevision)
            { fact = null; return false; }
            fact = Facts[index];
            return true;
        }

        public void BeginEphemeralExpiry(double cityTime) { ExpireAt = cityTime + 60d; }
        public void CancelEphemeralExpiry() { ExpireAt = 0d; }
        public bool ExpireIfDue(double cityTime)
        {
            if (ExpireAt <= 0d || cityTime < ExpireAt) return false;
            Facts.Clear(); ExpireAt = 0d; return true;
        }

        private int FindIndex(GatewayId gateway, string arcId)
        {
            for (int i = 0; i < Facts.Count; i++)
                if (Facts[i] != null && Facts[i].Gateway == gateway
                    && string.Equals(Facts[i].ArcId, arcId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private int FindOldest()
        {
            int result = 0;
            for (int i = 1; i < Facts.Count; i++)
            {
                int compare = Facts[i].ObservedAt.CompareTo(Facts[result].ObservedAt);
                if (compare < 0 || (compare == 0
                    && string.CompareOrdinal(Facts[i].Gateway.Value, Facts[result].Gateway.Value) < 0)) result = i;
            }
            return result;
        }
    }

    [Serializable]
    public sealed class GatewayKnowledgeResult
    {
        public GatewayKnowledgeKind Kind;
        public GatewayBlocker KnownBlockers;
        public ObservedGatewayFact Fact;
        public bool IsKnown { get { return Kind != GatewayKnowledgeKind.Unknown; } }
    }

    public interface IGatewayKnowledgeService
    {
        GatewayKnowledgeResult GetKnowledge(Sim sim, BuildingId building, BuildingGateway gateway,
            GatewayArc arc, GatewayRuntimeState state, double cityTime);
    }

    public sealed class GatewayKnowledgeService : IGatewayKnowledgeService
    {
        public GatewayKnowledgeResult GetKnowledge(Sim sim, BuildingId building, BuildingGateway gateway,
            GatewayArc arc, GatewayRuntimeState state, double cityTime)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (gateway == null || arc == null) throw new ArgumentNullException(nameof(gateway));
            GatewayBlocker current = state == null ? GatewayBlocker.None : state.Blockers;
            if (sim.Residence != null && KnowsAsResident(sim.Residence, building, arc, cityTime))
                return new GatewayKnowledgeResult { Kind = GatewayKnowledgeKind.IntrinsicResidence, KnownBlockers = current };
            if (sim.Employment != null && sim.Employment.Contract != null
                && sim.Employment.Contract.Active
                && (sim.Employment.Contract.Building == building
                    || sim.Employment.Contract.Building == arc.SourceBuilding
                    || sim.Employment.Contract.Building == arc.DestinationBuilding))
                return new GatewayKnowledgeResult { Kind = GatewayKnowledgeKind.IntrinsicWorkplace, KnownBlockers = current };
            ObservedGatewayFact fact;
            long revision = state == null ? gateway.Revision : Math.Max(gateway.Revision, state.Revision);
            if (sim.GatewayKnowledge.TryRecall(gateway.Id, arc.Id, revision, cityTime, out fact))
                return new GatewayKnowledgeResult
                { Kind = GatewayKnowledgeKind.Remembered, KnownBlockers = fact.Blockers, Fact = fact };
            return new GatewayKnowledgeResult { Kind = GatewayKnowledgeKind.Unknown };
        }

        private static bool KnowsAsResident(Residence residence, BuildingId graphRoot,
            GatewayArc arc, double cityTime)
        {
            if (residence.IsResidentOf(graphRoot, cityTime)) return true;
            if (residence.IsResidentOf(arc.SourceBuilding, cityTime)
                || residence.IsResidentOf(arc.DestinationBuilding, cityTime)) return true;
            return residence.IsManagedBy(graphRoot, cityTime)
                && arc.SourceBuilding == graphRoot
                && (string.IsNullOrEmpty(arc.DestinationRoom.Value)
                    || arc.DestinationBuilding == graphRoot);
        }
    }

    public interface IGatewayObservationSink
    {
        bool Observe(Sim sim, int distanceInBlocks, ObservedGatewayFact fact, double cityTime);
    }

    public sealed class GatewayObservationSink : IGatewayObservationSink
    {
        public bool Observe(Sim sim, int distanceInBlocks, ObservedGatewayFact fact, double cityTime)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (distanceInBlocks > 1 || sim.Spatial.Kind != SpatialStateKind.Materialized) return false;
            sim.GatewayKnowledge.Observe(fact, cityTime);
            return true;
        }
    }
}
