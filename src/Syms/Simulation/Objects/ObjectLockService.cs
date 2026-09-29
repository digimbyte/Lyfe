using System;

namespace Syms.Simulation.Objects
{
    public enum ObjectLockCommand { Lock, Unlock, Jam, Break }

    public sealed class ObjectLockService
    {
        public bool TryApply(
            InteractiveObjectState value,
            ObjectLockCommand command,
            ObjectUseContext context)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (context == null || context.Sim == null) throw new ArgumentNullException(nameof(context));
            if (value.Lock == null) return false;

            bool authorized = IsOwner(value, context)
                || (!string.IsNullOrEmpty(value.Lock.KeyId)
                    && context.HasClaim("key:" + value.Lock.KeyId));
            switch (command)
            {
                case ObjectLockCommand.Lock:
                    if (!authorized || value.Lock.State == LockState.Jammed
                        || value.Lock.State == LockState.Broken) return false;
                    value.Lock.State = LockState.Locked;
                    return true;
                case ObjectLockCommand.Unlock:
                    if (!authorized || value.Lock.State == LockState.Jammed
                        || value.Lock.State == LockState.Broken) return false;
                    value.Lock.State = LockState.Unlocked;
                    return true;
                case ObjectLockCommand.Jam:
                    if (!context.HasClaim("locksmith") && !context.HasClaim("orchestrator")) return false;
                    value.Lock.State = LockState.Jammed;
                    return true;
                case ObjectLockCommand.Break:
                    if (!context.HasClaim("force-lock") && !context.HasClaim("orchestrator")) return false;
                    value.Lock.State = LockState.Broken;
                    return true;
                default: return false;
            }
        }

        private static bool IsOwner(InteractiveObjectState value, ObjectUseContext context)
        {
            ObjectOwnership ownership = value.Ownership;
            if (ownership == null) return false;
            if (ownership.Kind == OwnershipKind.Sim)
                return string.Equals(ownership.OwnerId, context.Sim.Id, StringComparison.Ordinal);
            if (ownership.Kind == OwnershipKind.Household)
                return context.Sim.Residence != null && context.Sim.Residence.Active
                    && string.Equals(ownership.OwnerId, context.Sim.Residence.EffectiveHouseholdId, StringComparison.Ordinal);
            return context.HasClaim("owner:" + ownership.OwnerId);
        }
    }
}
