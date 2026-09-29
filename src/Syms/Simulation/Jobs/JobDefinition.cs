using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.World;
using Syms.Simulation.Activities;

namespace Syms.Simulation.Jobs
{
    public enum DutyKind
    {
        General,
        Patrol,
        FeedPrisoners,
        SupervisePrisoners,
        Reception,
        Housekeeping,
        ServeGuests
    }

    public enum JobCategory
    {
        Unemployed,
        Agriculture,
        Domestic,
        Trade,
        Commerce,
        Civic,
        Medical,
        Religious,
        Education,
        Transport,
        Military,
        Criminal
    }

    [Serializable]
    public sealed class DutyDefinition
    {
        public string Id;
        public string DisplayName;
        public DutyKind Kind;
        public string RequiredFacility;
        public float UtilityWeight = 1f;
        public double TypicalDurationSeconds = 1800d;
    }

    [Serializable]
    public sealed class JobDefinition
    {
        public JobDefinitionId Id;
        public string DisplayName;
        public JobCategory Category;
        public float BaseHourlyPay;
        public bool RequiresFacility;
        public FacilityKind WorkFacility;
        public WorkSchedule DefaultSchedule = new WorkSchedule();
        public List<string> RequiredSkills = new List<string>();
        public List<string> RequiredTools = new List<string>();
        public List<DutyDefinition> Duties = new List<DutyDefinition>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class EmploymentContract
    {
        public string Id;
        public RoleSlotId RoleSlot;
        public JobDefinitionId Job;
        public BuildingId Building;
        public string DisplayName;
        public PlaceId Workplace;
        public RoomId Room;
        public AnchorId WorkAnchor;
        public WorkSchedule Schedule = new WorkSchedule();
        public float HourlyPay;
        public bool RequiresFacility;
        public FacilityKind WorkFacility;
        public double StartTime;
        public bool Active = true;
    }
}
