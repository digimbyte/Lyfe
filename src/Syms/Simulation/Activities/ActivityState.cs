using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Travel;
using Syms.Simulation.World;

namespace Syms.Simulation.Activities
{
    public enum ActivityKind
    {
        Idle,
        Sleep,
        Eat,
        Drink,
        Toilet,
        Hygiene,
        Relax,
        Play,
        Exercise,
        Mourn,
        Socialize,
        Shop,
        Commute,
        Work,
        WorkTask,
        DeliverMeal,
        WorkBreak,
        Healthcare,
        Flee,
        Incapacitated
    }

    public enum ActivityStatus { Planned, Active, Completed, Interrupted, Blocked }

    [Serializable]
    public sealed class ActivitySegment
    {
        public ActivityKind Kind = ActivityKind.Idle;
        public ActivityStatus Status = ActivityStatus.Planned;
        public PlaceId Place;
        public RoomId Room;
        public AnchorId Anchor;
        public CellId Cell;
        public string TargetId;
        public ObjectId Object;
        public AffordanceSlotId ObjectSlot;
        public string ObjectAffordance;
        public List<string> AccessClaims = new List<string>();
        public double StartTime;
        public double EndTime;
        public double PlannedDurationSeconds;
        public string ReservationId;
        public RouteId Route;
        public long Revision;
        public bool RequiresFacility;
        public FacilityKind Facility;

        public bool IsActiveAt(double cityTime)
        {
            return Status == ActivityStatus.Active && cityTime >= StartTime && cityTime < EndTime;
        }
    }

    [Serializable]
    public sealed class ActivityCandidate
    {
        public ActivityKind Kind;
        public PlaceId Place;
        public RoomId Room;
        public AnchorId Anchor;
        public CellId Cell;
        public string TargetId;
        public ObjectId Object;
        public string ObjectAffordance;
        public List<string> AccessClaims = new List<string>();
        public double DurationSeconds = 900d;
        public float BaseUtility;
        public float WorkObligation;
        public double TravelSeconds;
        public bool RequiresFacility;
        public FacilityKind Facility;
    }

    [Serializable]
    public sealed class ActivityScore
    {
        public ActivityKind Kind;
        public float BaseScore;
        public float NeedScore;
        public float PersonalityScore;
        public float TraitScore;
        public float MoodScore;
        public float ObligationScore;
        public float TravelPenalty;
        public float Variation;
        public float Total;
    }
}
