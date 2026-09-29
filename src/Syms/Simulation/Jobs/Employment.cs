using System;
using Syms.Content;
using Syms.Simulation.World;
using Syms.Simulation.Activities;

namespace Syms.Simulation.Jobs
{
    [Serializable]
    public sealed class Employment
    {
        public EmploymentContract Contract;
        public int Performance;

        public string PositionId => Contract == null ? string.Empty : Contract.Job.Value;
        public WorkSchedule Schedule => Contract == null ? EmptySchedule : Contract.Schedule;
        public PlaceId Workplace => Contract == null ? new PlaceId() : Contract.Workplace;
        public RoomId WorkRoom => Contract == null ? new RoomId() : Contract.Room;
        public AnchorId WorkAnchor => Contract == null ? new AnchorId() : Contract.WorkAnchor;
        public float HourlyPay => Contract == null ? 0f : Contract.HourlyPay;
        public bool RequiresFacility => Contract != null && Contract.RequiresFacility;
        public FacilityKind WorkFacility => Contract == null ? FacilityKind.Generic : Contract.WorkFacility;
        public bool IsEmployed => Contract != null && Contract.Active;

        private static readonly WorkSchedule EmptySchedule = new WorkSchedule();
    }
}
