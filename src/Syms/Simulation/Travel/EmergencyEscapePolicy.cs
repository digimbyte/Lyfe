using System;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Stats;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    public sealed class EmergencyEscapePolicy
    {
        public bool MayAttemptDangerousGateway(Sim sim, GatewayArc arc,
            GatewayRuntimeState state, bool safeRouteAvailable, bool scriptedDirective,
            WorldRiskSettings settings)
        {
            if (sim == null || arc == null || arc.Fall == null) return false;
            settings = settings ?? new WorldRiskSettings();
            if (safeRouteAvailable) return false;
            bool fleeing = sim.Activity != null && sim.Activity.Kind == ActivityKind.Flee;
            bool afraid = sim.Mood == Mood.Afraid || sim.Mood == Mood.Anxious
                || sim.Stats.Get(SimStat.Anxiety) >= 80f;
            bool threatened = state != null && (state.Blockers & (GatewayBlocker.Fire
                | GatewayBlocker.Flood | GatewayBlocker.Collapse | GatewayBlocker.EnvironmentalHazard)) != 0;
            if (!scriptedDirective && !(fleeing || afraid) && !threatened) return false;
            if (arc.Fall.FatalChance > settings.MaximumEmergencyRouteFatalChance && !scriptedDirective) return false;
            if (!string.IsNullOrEmpty(arc.Fall.RequiredClimbingCapability)
                && !HasClaim(sim, "capability:" + arc.Fall.RequiredClimbingCapability)) return false;
            if (HasTrait(sim, Trait.Calm) && !threatened && !scriptedDirective) return false;
            return true;
        }

        private static bool HasClaim(Sim sim, string claim)
        {
            for (int i = 0; i < sim.AccessClaims.Count; i++)
                if (string.Equals(sim.AccessClaims[i], claim, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool HasTrait(Sim sim, Trait trait)
        { for (int i = 0; i < sim.Traits.Count; i++) if (sim.Traits[i] == trait) return true; return false; }
    }
}
