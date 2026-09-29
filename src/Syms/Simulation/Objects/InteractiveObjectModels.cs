using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.Stats;
using Syms.Simulation.World;

namespace Syms.Simulation.Objects
{
    public enum ObjectCategory
    {
        Seating,
        Sleeping,
        Plumbing,
        Surface,
        Storage,
        Workstation,
        Door,
        Window,
        Gate,
        Heating,
        Lighting,
        Recreation,
        Medical,
        Detention,
        Service,
        Decoration,
        Utility
    }

    public enum ObjectActionKind
    {
        None,
        Sit,
        Lounge,
        Sleep,
        Nap,
        Eat,
        Drink,
        Toilet,
        Wash,
        Bathe,
        Work,
        Serve,
        Store,
        Open,
        Close,
        Lock,
        Unlock,
        Warm,
        Play,
        Mourn,
        ReceiveCare
    }

    public enum SleepingArrangement
    {
        None,
        PrivateSingle,
        SharedBed,
        Bunk,
        Couch,
        Hotel,
        Institutional,
        Hospital,
        Jail
    }

    public enum OwnershipKind
    {
        None,
        Public,
        Sim,
        Household,
        Building,
        Business,
        Institution,
        Faction
    }

    public enum ObjectAccessPolicy
    {
        Public,
        OwnerOnly,
        Household,
        HouseholdAndGuests,
        Customers,
        Staff,
        Residents,
        Prisoners,
        Restricted
    }

    public enum LockState
    {
        None,
        Unlocked,
        Locked,
        Jammed,
        Broken
    }

    [Flags]
    public enum ObjectConditionFlags
    {
        None = 0,
        Dirty = 1,
        Broken = 2,
        Burning = 4,
        Wet = 8,
        Contaminated = 16,
        Occupied = 32,
        Disabled = 64
    }

    [Serializable]
    public sealed class ObjectAffordanceDefinition
    {
        public string LocalId;
        public ObjectActionKind Action;
        public ActivityKind Activity;
        public FacilityKind Facility = FacilityKind.Generic;
        public int SlotCount = 1;
        public float BaseUtility;
        public float Comfort;
        public float Privacy;
        public SleepingArrangement Sleeping;
        public bool RequiresOperational = true;
        public bool RequiresPrivacy;
        public bool AllowsEmergencyTrespass;
        public SimStat EmergencyNeed;
        public float EmergencyThreshold = 0.9f;
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class InteractiveObjectDefinition
    {
        public ObjectDefinitionId Id;
        public string DisplayName;
        public ObjectCategory Category;
        public float MaximumHealth = 100f;
        public float DefaultCleanliness = 100f;
        public bool IsPortalController;
        public List<ObjectAffordanceDefinition> Affordances = new List<ObjectAffordanceDefinition>();
        public List<string> Tags = new List<string>();

        public ObjectAffordanceDefinition FindAffordance(string localId)
        {
            for (int i = 0; i < Affordances.Count; i++)
                if (Affordances[i] != null
                    && string.Equals(Affordances[i].LocalId, localId, StringComparison.Ordinal)) return Affordances[i];
            return null;
        }
    }

    [Serializable]
    public sealed class ObjectOwnership
    {
        public OwnershipKind Kind;
        public string OwnerId;
        public ObjectAccessPolicy Access = ObjectAccessPolicy.Public;
    }

    [Serializable]
    public sealed class ObjectLock
    {
        public LockState State;
        public string KeyId;
    }

    [Serializable]
    public sealed class DoorState
    {
        public bool IsOpen;
        public List<string> ConnectionIds = new List<string>();
        public List<GatewayId> GatewayIds = new List<GatewayId>();
    }

    /// <summary>Designer-authored placement in a logical room.</summary>
    [Serializable]
    public sealed class AuthoredObjectInstance
    {
        public ObjectId Id;
        public ObjectDefinitionId Definition;
        public RoomId Room;
        public BuildingId Building;
        public AnchorId Anchor;
        public Float3 LocalPosition;
        public ObjectOwnership Ownership = new ObjectOwnership();
        public List<string> AssignedSimIds = new List<string>();
        public ObjectLock Lock;
        public DoorState Door;
        public float InitialHealth = -1f;
        public float InitialCleanliness = -1f;
        public bool Enabled = true;
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class InteractiveObjectState
    {
        public ObjectId Id;
        public ObjectDefinitionId Definition;
        public RoomId Room;
        public BuildingId Building;
        public PlaceId Place;
        public AnchorId Anchor;
        public Float3 LocalPosition;
        public ObjectOwnership Ownership = new ObjectOwnership();
        public List<string> AssignedSimIds = new List<string>();
        public ObjectLock Lock;
        public DoorState Door;
        public float Health;
        public float MaximumHealth;
        public float Cleanliness;
        public ObjectConditionFlags Conditions;
        public bool Enabled = true;
        public List<string> Tags = new List<string>();

        public bool IsOperational
        {
            get
            {
                return Enabled && Health > 0f
                    && (Conditions & (ObjectConditionFlags.Broken | ObjectConditionFlags.Disabled)) == 0;
            }
        }

        public float HealthRatio { get { return MaximumHealth <= 0f ? 0f : Health / MaximumHealth; } }

        public void ApplyDamage(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Health = Math.Max(0f, Health - amount);
            if (Health <= 0f) Conditions |= ObjectConditionFlags.Broken;
        }

        public void Repair(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Health = Math.Min(MaximumHealth, Health + amount);
            if (Health > 0f) Conditions &= ~ObjectConditionFlags.Broken;
        }

        public void SetCleanliness(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            Cleanliness = Math.Max(0f, Math.Min(100f, value));
            if (Cleanliness < 40f) Conditions |= ObjectConditionFlags.Dirty;
            else Conditions &= ~ObjectConditionFlags.Dirty;
        }

        public void SetCondition(ObjectConditionFlags condition, bool enabled)
        {
            if (enabled) Conditions |= condition;
            else Conditions &= ~condition;
        }
    }

    [Serializable]
    public sealed class ObjectAffordanceSlot
    {
        public AffordanceSlotId Id;
        public ObjectId Object;
        public string AffordanceLocalId;
        public int SlotIndex;
        public string ReservedBySimId;
        public double ReservedUntil;

        public bool IsAvailable(double cityTime)
        {
            return string.IsNullOrEmpty(ReservedBySimId) || ReservedUntil <= cityTime;
        }
    }
}
