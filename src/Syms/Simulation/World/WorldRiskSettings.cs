using System;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Stats;

namespace Syms.Simulation.World
{
    public enum FatalFallPolicy { NeverFatal, Orchestrated, Simulated }

    [Serializable]
    public sealed class WorldRiskSettings
    {
        public int Version = 1;
        public FatalFallPolicy FatalFallPolicy = FatalFallPolicy.Orchestrated;
        public float MaximumNormalRouteFatalChance = 0f;
        public float MaximumEmergencyRouteFatalChance = 0.75f;
    }

    [Serializable]
    public sealed class FallOutcome
    {
        public float Damage;
        public bool Incapacitated;
        public bool PotentiallyFatal;
        public bool Died;
        public string EventId;
    }

    [Serializable]
    public sealed class WorldSimulationEvent
    {
        public string Id;
        public string SimId;
        public GatewayId Gateway;
        public double CityTime;
        public float Damage;
        public bool PotentiallyFatal;
        public bool Died;
    }

    public sealed class GatewayFallResolver
    {
        public FallOutcome Resolve(Sim sim, GatewayId gateway, GatewayFallProfile profile,
            long traversalSequence, long timeBucket, WorldRiskSettings settings)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            settings = settings ?? new WorldRiskSettings();
            float damage = Math.Max(0f, profile.DropMetres * profile.InjuryPerMetre
                * Math.Max(0f, profile.LandingModifier));
            float roll = StableUnit(sim.Id + "|" + gateway.Value + "|" + traversalSequence + "|" + timeBucket);
            bool potentiallyFatal = roll < profile.FatalChance;
            sim.Stats.Adjust(SimStat.Health, -damage);
            bool incapacitated = sim.Stats.Get(SimStat.Health) <= 0f;
            bool died = potentiallyFatal && settings.FatalFallPolicy == FatalFallPolicy.Simulated;
            if (died) sim.LifeStatus = LifeStatus.Dead;
            return new FallOutcome
            {
                Damage = damage,
                Incapacitated = incapacitated,
                PotentiallyFatal = potentiallyFatal,
                Died = died,
                EventId = potentiallyFatal && settings.FatalFallPolicy == FatalFallPolicy.Orchestrated
                    ? "world.fall.potentially_fatal" : "world.fall"
            };
        }

        private static float StableUnit(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619; }
                return (hash & 0x00ffffff) / 16777216f;
            }
        }
    }
}
