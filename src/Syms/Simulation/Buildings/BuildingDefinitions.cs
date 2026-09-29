using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Jobs;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    public enum BuildingKind
    {
        Residence,
        Farm,
        Workshop,
        Shop,
        Inn,
        Hotel,
        Detention,
        Civic,
        Religious,
        Medical,
        Education,
        Industrial,
        PublicGrounds,
        Cemetery,
        SportsGround,
        MixedUse
    }

    public enum BuildingOccupancyKind
    {
        None,
        HouseholdResidence,
        RentalResidence,
        LodgingResidence,
        DetentionCell,
        InstitutionalResidence
    }

    [Serializable]
    public sealed class FacilityTemplate
    {
        public string LocalId;
        public FacilityKind Kind;
        public int Capacity = 1;
        public AnchorId Anchor;
    }

    [Serializable]
    public sealed class BuildingRoleTemplate
    {
        public string LocalId;
        public JobDefinitionId Job;
        public int Capacity = 1;
        public AnchorId WorkAnchor;
        public WorkSchedule ScheduleOverride;
        public bool HasHourlyPayOverride;
        public float HourlyPayOverride;
        public bool RequiresFacility;
        public FacilityKind WorkFacility;
        public bool HasFacilityOverride;
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public BuildingDefinitionId Id;
        public string DisplayName;
        public BuildingKind Kind;
        public List<FacilityTemplate> Facilities = new List<FacilityTemplate>();
        public List<BuildingRoleTemplate> Roles = new List<BuildingRoleTemplate>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class CapacityOverride
    {
        public string LocalId;
        public int Capacity;
    }

    [Serializable]
    public sealed class BuildingInstance
    {
        public BuildingId Id;
        /// <summary>Empty for a root; otherwise a direct parent. Grandchildren are invalid.</summary>
        public BuildingId ParentBuilding;
        public BuildingDefinitionId Definition;
        public WorldAddress Address;
        public BuildingOccupancyKind Occupancy;
        public float RatePerDay;
        public bool PopulationEligible = true;
        /// <summary>Required on a root manager that owns detention-cell children.</summary>
        public DetentionMealSchedule DetentionMeals;
        public bool Enabled = true;
        public List<CapacityOverride> FacilityOverrides = new List<CapacityOverride>();
        public List<CapacityOverride> RoleOverrides = new List<CapacityOverride>();
        public List<string> Tags = new List<string>();

        public int GetFacilityCapacity(FacilityTemplate facility)
        {
            return FindCapacity(FacilityOverrides, facility.LocalId, facility.Capacity);
        }

        public int GetRoleCapacity(BuildingRoleTemplate role)
        {
            return FindCapacity(RoleOverrides, role.LocalId, role.Capacity);
        }

        public bool IsManagedSubBuilding { get { return !string.IsNullOrEmpty(ParentBuilding.Value); } }
        public bool IsResidence
        {
            get
            {
                return Occupancy == BuildingOccupancyKind.HouseholdResidence
                    || Occupancy == BuildingOccupancyKind.RentalResidence
                    || Occupancy == BuildingOccupancyKind.LodgingResidence
                    || Occupancy == BuildingOccupancyKind.InstitutionalResidence;
            }
        }

        private static int FindCapacity(List<CapacityOverride> overrides, string localId, int fallback)
        {
            for (int i = 0; i < overrides.Count; i++)
                if (string.Equals(overrides[i].LocalId, localId, StringComparison.Ordinal))
                    return Math.Max(0, overrides[i].Capacity);
            return Math.Max(0, fallback);
        }
    }

    [Serializable]
    public sealed class BuildingRoleSlot
    {
        public RoleSlotId Id;
        public BuildingId Building;
        public JobDefinitionId Job;
        public PlaceId Workplace;
        public RoomId Room;
        public AnchorId WorkAnchor;
        public WorkSchedule Schedule = new WorkSchedule();
        public float HourlyPay;
        public bool RequiresFacility;
        public FacilityKind WorkFacility;
        public string AssignedSimId;
        public List<string> Tags = new List<string>();

        public bool IsVacant { get { return string.IsNullOrEmpty(AssignedSimId); } }
    }
}
