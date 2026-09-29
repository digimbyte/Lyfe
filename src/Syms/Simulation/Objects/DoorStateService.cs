using System;
using Syms.Simulation.World;
using Syms.Simulation.Buildings;

namespace Syms.Simulation.Objects
{
    public enum DoorCommand { Open, Close, Lock, Unlock }

    public sealed class DoorStateService
    {
        public bool TryApply(
            InteractiveObjectState value,
            DoorCommand command,
            ObjectUseContext context,
            WorldTopology topology)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (context == null || context.Sim == null) throw new ArgumentNullException(nameof(context));
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (value.Door == null || !value.IsOperational) return false;

            bool hasKey = value.Lock != null && (string.IsNullOrEmpty(value.Lock.KeyId)
                ? IsOwner(value, context)
                : context.HasClaim("key:" + value.Lock.KeyId) || IsOwner(value, context));
            switch (command)
            {
                case DoorCommand.Open:
                    if (value.Lock != null
                        && (value.Lock.State == LockState.Locked || value.Lock.State == LockState.Jammed)) return false;
                    value.Door.IsOpen = true;
                    break;
                case DoorCommand.Close:
                    value.Door.IsOpen = false;
                    break;
                case DoorCommand.Lock:
                    if (!hasKey || value.Door.IsOpen || value.Lock.State == LockState.Jammed) return false;
                    value.Lock.State = LockState.Locked;
                    break;
                case DoorCommand.Unlock:
                    if (!hasKey || value.Lock.State == LockState.Jammed) return false;
                    value.Lock.State = LockState.Unlocked;
                    break;
                default: return false;
            }
            Synchronize(value, topology);
            return true;
        }

        public void Synchronize(InteractiveObjectState value, WorldTopology topology)
        {
            if (value == null || value.Door == null) return;
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            bool broken = !value.IsOperational || (value.Lock != null && value.Lock.State == LockState.Broken);
            bool closed = !broken && !value.Door.IsOpen;
            bool locked = !broken && value.Lock != null
                && (value.Lock.State == LockState.Locked || value.Lock.State == LockState.Jammed);
            for (int i = 0; i < value.Door.ConnectionIds.Count; i++)
            {
                CellConnection connection;
                if (!topology.TryGetConnection(value.Door.ConnectionIds[i], out connection)) continue;
                if (connection.Portal != PortalKind.Door)
                    throw new InvalidOperationException("Door objects may control only baked door connections.");
                topology.SetBlocker(connection.Id, ConnectionBlocker.Closed, closed);
                topology.SetBlocker(connection.Id, ConnectionBlocker.Locked, locked);
            }
        }

        /// <summary>Synchronizes authored building gateways without touching unrelated blockers.</summary>
        public void Synchronize(InteractiveObjectState value, IGatewayStateService gateways)
        {
            if (value == null || value.Door == null) return;
            if (gateways == null) throw new ArgumentNullException(nameof(gateways));
            bool broken = !value.IsOperational || (value.Lock != null && value.Lock.State == LockState.Broken);
            bool closed = !broken && !value.Door.IsOpen;
            bool locked = !broken && value.Lock != null
                && (value.Lock.State == LockState.Locked || value.Lock.State == LockState.Jammed);
            bool jammed = !broken && value.Lock != null && value.Lock.State == LockState.Jammed;
            for (int i = 0; i < value.Door.GatewayIds.Count; i++)
            {
                GatewayId id = value.Door.GatewayIds[i];
                gateways.SetBlocker(id, GatewayBlocker.Closed, closed);
                gateways.SetBlocker(id, GatewayBlocker.Locked, locked);
                gateways.SetBlocker(id, GatewayBlocker.Jammed, jammed);
                gateways.SetHealth(id, value.Health);
            }
        }

        private static bool IsOwner(InteractiveObjectState value, ObjectUseContext context)
        {
            if (value.Ownership == null) return false;
            if (value.Ownership.Kind == OwnershipKind.Sim)
                return string.Equals(value.Ownership.OwnerId, context.Sim.Id, StringComparison.Ordinal);
            if (value.Ownership.Kind == OwnershipKind.Household)
                return context.Sim.Residence != null && context.Sim.Residence.Active
                    && string.Equals(value.Ownership.OwnerId, context.Sim.Residence.EffectiveHouseholdId, StringComparison.Ordinal);
            return false;
        }
    }
}
