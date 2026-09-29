using System;
using Syms.Simulation.Buildings;
using Syms.Content;
using Syms.Simulation.World;

namespace Syms.Simulation.People
{
    public enum HousingStatus
    {
        Homeless,
        Housed
    }

    /// <summary>
    /// Stable logical residence data. Current physical position must not be used
    /// to infer a household or neighbourhood.
    /// </summary>
    [Serializable]
    public sealed class TemporaryResidence
    {
        public string LeaseId;
        public string HouseholdId;
        public BuildingId Building;
        public BuildingId ManagedBy;
        public RoomId Bedroom;
        public ObjectId AssignedBed;
        public double StartsAt;
        public double EndsAt;

        public bool IsActiveAt(double cityTime)
        {
            return !string.IsNullOrEmpty(LeaseId) && cityTime >= StartsAt && cityTime < EndsAt;
        }
    }

    [Serializable]
    public sealed class Residence
    {
        public string HouseholdId;
        public string LeaseId;
        public BuildingId Building;
        public BuildingId ManagedBy;
        public ObjectId AssignedBed;
        public RoomId Bedroom;
        public WorldAddress HomeAddress;
        public bool Active;
        public TemporaryResidence Temporary;

        public bool Occupies(BuildingId building, double cityTime)
        {
            return (Active && (Building == building || ManagedBy == building))
                || (Temporary != null && Temporary.IsActiveAt(cityTime)
                    && (Temporary.Building == building || Temporary.ManagedBy == building));
        }

        public bool IsResidentOf(BuildingId building, double cityTime)
        {
            return (Active && Building == building)
                || (Temporary != null && Temporary.IsActiveAt(cityTime) && Temporary.Building == building);
        }

        public bool IsManagedBy(BuildingId building, double cityTime)
        {
            return (Active && ManagedBy == building)
                || (Temporary != null && Temporary.IsActiveAt(cityTime) && Temporary.ManagedBy == building);
        }

        public bool HasHousehold
        {
            get { return !string.IsNullOrWhiteSpace(EffectiveHouseholdId); }
        }

        public string EffectiveHouseholdId
        {
            get
            {
                if (Active && !string.IsNullOrWhiteSpace(HouseholdId)) return HouseholdId;
                return Temporary == null ? string.Empty : Temporary.HouseholdId ?? string.Empty;
            }
        }

        public bool HasNeighbourhood
        {
            get { return Active && !string.IsNullOrWhiteSpace(HomeAddress.Block.Value); }
        }

        public HousingStatus Status
        {
            get { return Active || Temporary != null ? HousingStatus.Housed : HousingStatus.Homeless; }
        }

        public bool IsHomeless { get { return Status == HousingStatus.Homeless; } }
    }
}
