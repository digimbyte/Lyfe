using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.People;
using Syms.Simulation.Social;
using Syms.Simulation.Stats;

namespace Syms.Simulation.Objects
{
    [Serializable]
    public sealed class ObjectUseContext
    {
        public Sim Sim;
        public RelationshipGraph Relationships;
        public double CityTime;
        public List<string> Claims = new List<string>();

        public bool HasClaim(string claim)
        {
            for (int i = 0; i < Claims.Count; i++)
                if (string.Equals(Claims[i], claim, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class ObjectUseEvaluation
    {
        public bool Allowed;
        public bool Trespassing;
        public float UtilityModifier;
        public List<string> Reasons = new List<string>();
    }

    public interface IObjectUsePolicy
    {
        ObjectUseEvaluation Evaluate(
            InteractiveObjectState value,
            InteractiveObjectDefinition definition,
            ObjectAffordanceDefinition affordance,
            ObjectUseContext context);
    }

    public sealed class ObjectUsePolicy : IObjectUsePolicy
    {
        public ObjectUseEvaluation Evaluate(
            InteractiveObjectState value,
            InteractiveObjectDefinition definition,
            ObjectAffordanceDefinition affordance,
            ObjectUseContext context)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (affordance == null) throw new ArgumentNullException(nameof(affordance));
            if (context == null || context.Sim == null) throw new ArgumentNullException(nameof(context));

            var result = new ObjectUseEvaluation { Allowed = true };
            if (!value.Enabled)
                return Deny(result, "object.disabled");
            if (affordance.RequiresOperational && !value.IsOperational)
                return Deny(result, "object.broken");
            if (value.Lock != null && value.Lock.State != LockState.None
                && value.Lock.State != LockState.Unlocked && value.Lock.State != LockState.Broken
                && !CanUnlock(value, context))
                return Deny(result, "object.locked");

            bool authorized = IsAuthorized(value, context);
            float urgency = context.Sim.Stats.GetUrgency(affordance.EmergencyNeed);
            if (!authorized)
            {
                if (!affordance.AllowsEmergencyTrespass || urgency < affordance.EmergencyThreshold)
                    return Deny(result, "access.denied");
                result.Trespassing = true;
                result.UtilityModifier -= 35f;
                result.Reasons.Add("access.emergency_trespass");
            }

            result.UtilityModifier += affordance.BaseUtility;
            result.UtilityModifier += affordance.Comfort * 0.25f;
            result.UtilityModifier -= Math.Max(0f, 100f - value.Cleanliness) * 0.2f;
            if ((value.Conditions & ObjectConditionFlags.Dirty) != 0) result.UtilityModifier -= 10f;
            if ((value.Conditions & ObjectConditionFlags.Contaminated) != 0) result.UtilityModifier -= 40f;
            if (affordance.RequiresPrivacy) result.UtilityModifier -= Math.Max(0f, 50f - affordance.Privacy) * 0.4f;
            ApplySleepingConsequences(result, value, affordance, context, urgency);
            return result;
        }

        private static bool IsAuthorized(InteractiveObjectState value, ObjectUseContext context)
        {
            ObjectOwnership ownership = value.Ownership ?? new ObjectOwnership();
            string owner = ownership.OwnerId ?? string.Empty;
            switch (ownership.Access)
            {
                case ObjectAccessPolicy.Public: return true;
                case ObjectAccessPolicy.OwnerOnly: return IsOwner(ownership, context);
                case ObjectAccessPolicy.Household:
                    return IsOwner(ownership, context) || IsHouseholdMember(owner, context.Sim);
                case ObjectAccessPolicy.HouseholdAndGuests:
                    return IsOwner(ownership, context) || IsHouseholdMember(owner, context.Sim)
                        || context.HasClaim("guest:" + owner) || IsCloseToSimOwner(ownership, context);
                case ObjectAccessPolicy.Customers:
                    return context.HasClaim("customer:" + owner) || IsOwner(ownership, context);
                case ObjectAccessPolicy.Staff:
                    return context.HasClaim("staff:" + owner) || IsOwner(ownership, context)
                        || (!string.IsNullOrEmpty(value.Building.Value)
                            && context.Sim.Employment != null
                            && context.Sim.Employment.Contract != null
                            && context.Sim.Employment.Contract.Active
                            && (context.Sim.Employment.WorkRoom == value.Room
                                || context.Sim.Employment.Contract.RoleSlot.Value.StartsWith(value.Building.Value + "/", StringComparison.Ordinal)));
                case ObjectAccessPolicy.Residents:
                    return context.Sim.Residence != null
                        && (context.Sim.Residence.Occupies(value.Building, context.CityTime)
                            || IsHouseholdMember(owner, context.Sim));
                case ObjectAccessPolicy.Prisoners: return context.HasClaim("prisoner:" + owner);
                case ObjectAccessPolicy.Restricted: return context.HasClaim("access:" + owner) || IsOwner(ownership, context);
                default: return false;
            }
        }

        private static bool IsOwner(ObjectOwnership ownership, ObjectUseContext context)
        {
            if (ownership.Kind == OwnershipKind.Public) return true;
            if (ownership.Kind == OwnershipKind.Sim)
                return string.Equals(ownership.OwnerId, context.Sim.Id, StringComparison.Ordinal);
            if (ownership.Kind == OwnershipKind.Household) return IsHouseholdMember(ownership.OwnerId, context.Sim);
            if (ownership.Kind == OwnershipKind.Building)
                return context.Sim.Residence != null
                    && context.Sim.Residence.Occupies(new BuildingId(ownership.OwnerId), context.CityTime);
            return false;
        }

        private static bool IsHouseholdMember(string householdId, Sim sim)
        {
            return sim.Residence != null && sim.Residence.Active
                && string.Equals(sim.Residence.EffectiveHouseholdId, householdId, StringComparison.Ordinal);
        }

        private static bool IsCloseToSimOwner(ObjectOwnership ownership, ObjectUseContext context)
        {
            if (ownership.Kind != OwnershipKind.Sim || context.Relationships == null) return false;
            SocialRelationship relationship = context.Relationships.Find(context.Sim.Id, ownership.OwnerId);
            return relationship != null && (relationship.HasKind(RelationshipKind.Partner)
                || relationship.HasKind(RelationshipKind.Family));
        }

        private static bool CanUnlock(InteractiveObjectState value, ObjectUseContext context)
        {
            if (value.Lock == null) return true;
            if (string.IsNullOrEmpty(value.Lock.KeyId)) return IsOwner(value.Ownership, context);
            return context.HasClaim("key:" + value.Lock.KeyId) || IsOwner(value.Ownership, context);
        }

        private static void ApplySleepingConsequences(
            ObjectUseEvaluation result,
            InteractiveObjectState value,
            ObjectAffordanceDefinition affordance,
            ObjectUseContext context,
            float urgency)
        {
            float penalty;
            switch (affordance.Sleeping)
            {
                case SleepingArrangement.PrivateSingle:
                    penalty = IsAssigned(value, context.Sim.Id) ? 0f
                        : (IsCloseToAssigned(value, context) ? -10f
                            : (IsHouseholdOwner(value, context.Sim) ? -25f : -45f));
                    break;
                case SleepingArrangement.SharedBed:
                    penalty = IsAssigned(value, context.Sim.Id) ? 0f
                        : (IsCloseToAssigned(value, context) ? -5f
                            : (IsHouseholdOwner(value, context.Sim) ? -15f : -50f));
                    break;
                case SleepingArrangement.Bunk: penalty = -10f; break;
                case SleepingArrangement.Couch: penalty = -25f; break;
                case SleepingArrangement.Hotel: penalty = -5f; break;
                case SleepingArrangement.Institutional: penalty = -15f; break;
                case SleepingArrangement.Hospital: penalty = -20f; break;
                case SleepingArrangement.Jail: penalty = -60f; break;
                default: return;
            }

            if (penalty < 0f)
            {
                // Desperation makes a poor or socially awkward bed acceptable,
                // but never turns it into a preferred bed in its own right.
                float relieved = Math.Abs(penalty) * Math.Min(1f, Math.Max(0f, urgency)) * 0.9f;
                penalty = Math.Min(0f, penalty + relieved);
                result.Reasons.Add(urgency >= affordance.EmergencyThreshold
                    ? "sleep.desperate" : "sleep.suboptimal");
            }
            result.UtilityModifier += penalty;
        }

        private static bool IsAssigned(InteractiveObjectState value, string simId)
        {
            for (int i = 0; i < value.AssignedSimIds.Count; i++)
                if (string.Equals(value.AssignedSimIds[i], simId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsHouseholdOwner(InteractiveObjectState value, Sim sim)
        {
            return value.Ownership != null && value.Ownership.Kind == OwnershipKind.Household
                && IsHouseholdMember(value.Ownership.OwnerId, sim);
        }

        private static bool IsCloseToAssigned(InteractiveObjectState value, ObjectUseContext context)
        {
            if (context.Relationships == null) return false;
            for (int i = 0; i < value.AssignedSimIds.Count; i++)
            {
                SocialRelationship relationship = context.Relationships.Find(context.Sim.Id, value.AssignedSimIds[i]);
                if (relationship != null && (relationship.HasKind(RelationshipKind.Partner)
                    || relationship.HasKind(RelationshipKind.Family))) return true;
            }
            return false;
        }

        private static ObjectUseEvaluation Deny(ObjectUseEvaluation result, string reason)
        {
            result.Allowed = false;
            result.UtilityModifier = float.MinValue;
            result.Reasons.Add(reason);
            return result;
        }
    }
}
