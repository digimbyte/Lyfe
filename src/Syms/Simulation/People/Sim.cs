using System;
using System.Collections.Generic;
using Syms.Simulation.Jobs;
using Syms.Simulation.Social;
using Syms.Simulation.Stats;
using Syms.Simulation.Activities;
using Syms.Simulation.World;

namespace Syms.Simulation.People
{
    public enum LifeStatus
    {
        Alive,
        Dead
    }

    [Serializable]
    public sealed class Sim
    {
        public string Id;
        public string GivenName;
        public string FamilyName;
        public LifeStatus LifeStatus = LifeStatus.Alive;
        public Mood Mood = Mood.Neutral;
        public List<Trait> Traits = new List<Trait>();
        public List<Faction> Factions = new List<Faction>();
        public List<Alliance> Alliances = new List<Alliance>();
        public List<string> AccessClaims = new List<string>();
        public SimStats Stats = new SimStats();
        public Employment Employment = new Employment();
        public Residence Residence = new Residence();
        public Personality Personality = new Personality();
        public Demographics Demographics = new Demographics();
        public ActivitySegment Activity = new ActivitySegment();
        public ActivitySegment PendingActivity;
        public SimSpatialState Spatial = new SimSpatialState();
        public GatewayKnowledgeState GatewayKnowledge = new GatewayKnowledgeState();
        public RouteFailureState RouteFailure;
        public long DecisionSequence;
        public double LastSimulatedAt;
        public double NextWakeTime;

        public Sim(string id, string givenName, string familyName)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A Sim needs an ID.", nameof(id));
            Id = id;
            GivenName = givenName ?? string.Empty;
            FamilyName = familyName ?? string.Empty;
        }

        public void StartActivity(ActivitySegment activity, double cityTime)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            long previousRevision = Activity == null ? 0 : Activity.Revision;
            activity.Revision = previousRevision + 1;
            activity.Status = ActivityStatus.Active;
            Activity = activity;
            LastSimulatedAt = cityTime;
            DecisionSequence++;
            Spatial.InvalidateObservedPose();
        }
    }
}
