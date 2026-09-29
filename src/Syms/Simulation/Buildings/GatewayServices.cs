using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    [Serializable]
    public sealed class GatewayRuntimeState
    {
        public GatewayId Gateway;
        public GatewayBlocker Blockers;
        public float Health = 100f;
        public bool Enabled = true;
        public long Revision;
    }

    public interface IGatewayStateService
    {
        bool TryGet(GatewayId gateway, out GatewayRuntimeState state);
        GatewayRuntimeState GetOrCreate(GatewayId gateway);
        bool SetBlocker(GatewayId gateway, GatewayBlocker blocker, bool enabled);
        bool SetHealth(GatewayId gateway, float health);
    }

    [Serializable]
    public sealed class GatewayStateService : IGatewayStateService
    {
        public List<GatewayRuntimeState> States = new List<GatewayRuntimeState>();
        [NonSerialized] private Dictionary<GatewayId, GatewayRuntimeState> _byId;
        [NonSerialized] private int _indexedCount = -1;

        public bool TryGet(GatewayId gateway, out GatewayRuntimeState state)
        { EnsureIndex(); return _byId.TryGetValue(gateway, out state); }

        public GatewayRuntimeState GetOrCreate(GatewayId gateway)
        {
            EnsureIndex();
            GatewayRuntimeState state;
            if (_byId.TryGetValue(gateway, out state)) return state;
            state = new GatewayRuntimeState { Gateway = gateway };
            States.Add(state);
            _byId[gateway] = state;
            _indexedCount = States.Count;
            return state;
        }

        public bool SetBlocker(GatewayId gateway, GatewayBlocker blocker, bool enabled)
        {
            GatewayRuntimeState state = GetOrCreate(gateway);
            GatewayBlocker next = enabled ? state.Blockers | blocker : state.Blockers & ~blocker;
            if (next == state.Blockers) return false;
            state.Blockers = next;
            state.Revision++;
            return true;
        }

        public bool SetHealth(GatewayId gateway, float health)
        {
            if (float.IsNaN(health) || float.IsInfinity(health)) throw new ArgumentOutOfRangeException(nameof(health));
            GatewayRuntimeState state = GetOrCreate(gateway);
            float next = Math.Max(0f, health);
            if (Math.Abs(next - state.Health) < 0.0001f) return false;
            state.Health = next;
            state.Revision++;
            return true;
        }

        private void EnsureIndex()
        {
            if (_byId != null && _indexedCount == States.Count) return;
            _byId = new Dictionary<GatewayId, GatewayRuntimeState>();
            for (int i = 0; i < States.Count; i++)
                if (States[i] != null) _byId[States[i].Gateway] = States[i];
            _indexedCount = States.Count;
        }
    }

    [Serializable]
    public sealed class GatewayAccessContext
    {
        public Sim Sim;
        public TravelMode Mode = TravelMode.Walk;
        public List<string> Claims = new List<string>();
        public List<string> Capabilities = new List<string>();
        public bool Emergency;
        public bool AllowDangerousTraversal;
        public double CityTime;
    }

    [Serializable]
    public sealed class GatewayAccessEvaluation
    {
        public bool Allowed;
        public bool Dangerous;
        public bool RequiresAction;
        public GatewayBlocker BlockingReasons;
        public List<string> MissingRequirements = new List<string>();
    }

    public interface IGatewayAccessEvaluator
    {
        GatewayAccessEvaluation Evaluate(GatewayArc arc, BuildingGateway gateway,
            GatewayRuntimeState state, GatewayAccessContext context);
    }

    public sealed class GatewayAccessEvaluator : IGatewayAccessEvaluator
    {
        private const GatewayBlocker Dangerous = GatewayBlocker.Fire | GatewayBlocker.Flood
            | GatewayBlocker.Collapse | GatewayBlocker.EnvironmentalHazard;
        private const GatewayBlocker Hard = GatewayBlocker.Jammed | GatewayBlocker.Barred
            | GatewayBlocker.Barricaded | GatewayBlocker.Sealed | GatewayBlocker.Debris
            | GatewayBlocker.DamagedImpassable | GatewayBlocker.Collapse
            | GatewayBlocker.DestinationUnavailable;

        public GatewayAccessEvaluation Evaluate(GatewayArc arc, BuildingGateway gateway,
            GatewayRuntimeState state, GatewayAccessContext context)
        {
            if (arc == null) throw new ArgumentNullException(nameof(arc));
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));
            if (context == null || context.Sim == null) throw new ArgumentNullException(nameof(context));
            var result = new GatewayAccessEvaluation();
            GatewayRequirementSet requirements = arc.Requirements ?? new GatewayRequirementSet();
            GatewayBlocker blockers = state != null && !state.Enabled
                ? GatewayBlocker.DestinationUnavailable
                : (state == null ? GatewayBlocker.None : state.Blockers);
            result.Dangerous = arc.Fall != null || (blockers & Dangerous) != 0;

            if ((gateway.Channels & GatewayChannel.Traversal) == 0)
                blockers |= GatewayBlocker.DestinationUnavailable;
            if (context.Mode == TravelMode.None || (requirements.AllowedModes & context.Mode) == 0)
                result.MissingRequirements.Add("travel-mode");
            for (int i = 0; i < requirements.RequiredCapabilities.Count; i++)
                if (!Contains(context.Capabilities, requirements.RequiredCapabilities[i]))
                    result.MissingRequirements.Add("capability:" + requirements.RequiredCapabilities[i]);

            bool claimSatisfied = requirements.AnyAccessClaims.Count == 0;
            for (int i = 0; i < requirements.AnyAccessClaims.Count && !claimSatisfied; i++)
                claimSatisfied = HasClaim(context, requirements.AnyAccessClaims[i]);
            if (!claimSatisfied)
            {
                blockers |= GatewayBlocker.PermissionDenied;
                result.MissingRequirements.Add("access-claim");
            }
            if (!string.IsNullOrEmpty(requirements.RequiredKeyId)
                && !HasClaim(context, "key:" + requirements.RequiredKeyId))
            {
                blockers |= GatewayBlocker.Locked;
                result.MissingRequirements.Add("key:" + requirements.RequiredKeyId);
            }

            if ((blockers & GatewayBlocker.Closed) != 0)
            { blockers &= ~GatewayBlocker.Closed; result.RequiresAction = true; }
            if ((blockers & GatewayBlocker.Obstruction) != 0
                && requirements.AllowMoveObstruction && Contains(context.Capabilities, "move-obstruction"))
            { blockers &= ~GatewayBlocker.Obstruction; result.RequiresAction = true; }
            if ((blockers & (GatewayBlocker.Locked | GatewayBlocker.PermissionDenied)) != 0
                && requirements.AllowBreak && Contains(context.Capabilities, "break-gateway"))
            { blockers &= ~(GatewayBlocker.Locked | GatewayBlocker.PermissionDenied); result.RequiresAction = true; }
            if (result.Dangerous && !context.AllowDangerousTraversal) blockers |= GatewayBlocker.EnvironmentalHazard;
            if ((blockers & Hard) != 0 && requirements.AllowBreak
                && Contains(context.Capabilities, "break-gateway"))
            { blockers &= ~Hard; result.RequiresAction = true; }

            result.BlockingReasons = blockers;
            result.Allowed = blockers == GatewayBlocker.None && result.MissingRequirements.Count == 0;
            return result;
        }

        private static bool HasClaim(GatewayAccessContext context, string claim)
        {
            if (Contains(context.Claims, claim) || Contains(context.Sim.AccessClaims, claim)) return true;
            int separator = claim.IndexOf(':');
            if (separator <= 0 || separator == claim.Length - 1) return false;
            string kind = claim.Substring(0, separator);
            string target = claim.Substring(separator + 1);
            if ((string.Equals(kind, "staff", StringComparison.Ordinal)
                || string.Equals(kind, "employee", StringComparison.Ordinal))
                && context.Sim.Employment != null && context.Sim.Employment.Contract != null
                && context.Sim.Employment.Contract.Active)
                return string.Equals(context.Sim.Employment.Contract.Building.Value, target, StringComparison.Ordinal);
            if (string.Equals(kind, "resident", StringComparison.Ordinal)
                && context.Sim.Residence != null)
                return context.Sim.Residence.Occupies(new BuildingId(target), context.CityTime);
            if (string.Equals(kind, "household", StringComparison.Ordinal)
                && context.Sim.Residence != null)
                return string.Equals(context.Sim.Residence.EffectiveHouseholdId, target, StringComparison.Ordinal);
            return false;
        }

        private static bool Contains(IList<string> values, string value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
