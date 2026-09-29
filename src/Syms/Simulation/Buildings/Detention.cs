using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Jobs;
using Syms.Simulation.Objects;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Buildings
{
    [Serializable]
    public sealed class DetentionMealSchedule
    {
        public List<int> MinutesOfDay = new List<int>();
        public int DeliveryWindowMinutes = 90;

        public bool IsValid
        {
            get
            {
                if (MinutesOfDay.Count == 0 || DeliveryWindowMinutes <= 0) return false;
                var seen = new HashSet<int>();
                for (int i = 0; i < MinutesOfDay.Count; i++)
                    if (MinutesOfDay[i] < 0 || MinutesOfDay[i] >= 1440 || !seen.Add(MinutesOfDay[i])) return false;
                return true;
            }
        }

        public double NextDueAfter(double cityTime)
        {
            double dayStart = Math.Floor(Math.Max(0d, cityTime) / 86400d) * 86400d;
            int best = int.MaxValue;
            for (int i = 0; i < MinutesOfDay.Count; i++)
            {
                double due = dayStart + (MinutesOfDay[i] * 60d);
                if (due > cityTime && MinutesOfDay[i] < best) best = MinutesOfDay[i];
            }
            if (best != int.MaxValue) return dayStart + (best * 60d);
            int first = int.MaxValue;
            for (int i = 0; i < MinutesOfDay.Count; i++) first = Math.Min(first, MinutesOfDay[i]);
            return dayStart + 86400d + (first * 60d);
        }

        public double MostRecentDue(double cityTime)
        {
            double dayStart = Math.Floor(Math.Max(0d, cityTime) / 86400d) * 86400d;
            int best = -1;
            for (int i = 0; i < MinutesOfDay.Count; i++)
                if (dayStart + (MinutesOfDay[i] * 60d) <= cityTime)
                    best = Math.Max(best, MinutesOfDay[i]);
            if (best >= 0) return dayStart + (best * 60d);
            int last = -1;
            for (int i = 0; i < MinutesOfDay.Count; i++) last = Math.Max(last, MinutesOfDay[i]);
            return dayStart - 86400d + (last * 60d);
        }

        public long CountDue(double fromInclusive, double toInclusive)
        {
            if (toInclusive < fromInclusive) return 0;
            long firstDay = (long)Math.Floor(Math.Max(0d, fromInclusive) / 86400d);
            long lastDay = (long)Math.Floor(Math.Max(0d, toInclusive) / 86400d);
            long count = 0;
            for (int i = 0; i < MinutesOfDay.Count; i++)
            {
                double offset = MinutesOfDay[i] * 60d;
                long first = (long)Math.Ceiling((fromInclusive - offset) / 86400d);
                long last = (long)Math.Floor((toInclusive - offset) / 86400d);
                first = Math.Max(firstDay, Math.Max(0, first));
                last = Math.Min(lastDay, last);
                if (last >= first) count += last - first + 1;
            }
            return count;
        }
    }

    [Serializable]
    public sealed class DetentionCellInstance
    {
        public BuildingId Building;
        public BuildingId ManagedBy;
        public int BedCapacity;
    }

    [Serializable]
    public sealed class CustodyAssignment
    {
        public DetentionId Id;
        public string SimId;
        public BuildingId CellBuilding;
        public BuildingId ManagedBy;
        public RoomId Room;
        public ObjectId AssignedBed;
        public double StartsAt;
        public double EndsAt = double.MaxValue;
        public double NextMealAt;
        public long MissedMealCount;
        public bool Active = true;
    }

    public enum JailMealStatus { Pending, Claimed, Delivered, Missed, Cancelled }

    [Serializable]
    public sealed class JailMealObligation
    {
        public string Id;
        public DetentionId Custody;
        public string PrisonerSimId;
        public BuildingId ManagedBy;
        public BuildingId CellBuilding;
        public RoomId DeliveryRoom;
        public double DueAt;
        public double Deadline;
        public JailMealStatus Status;
        public string GuardSimId;
        public double CompletedAt;
    }

    [Serializable]
    public sealed class DetentionService
    {
        public List<DetentionCellInstance> Cells = new List<DetentionCellInstance>();
        public List<CustodyAssignment> Custody = new List<CustodyAssignment>();
        public List<JailMealObligation> Meals = new List<JailMealObligation>();
        public List<BuildingInstance> Managers = new List<BuildingInstance>();
        [NonSerialized] private ObjectStateStore _objects;
        public long NextCustodySequence;

        public void Bind(ObjectStateStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public void Configure(IList<BuildingInstance> buildings, ObjectStateStore objects)
        {
            Bind(objects);
            Cells.Clear();
            Managers.Clear();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingInstance building = buildings[i];
                if (building == null || !building.Enabled) continue;
                if (!building.IsManagedSubBuilding && building.DetentionMeals != null)
                    Managers.Add(building);
                if (building.Occupancy != BuildingOccupancyKind.DetentionCell) continue;
                Cells.Add(new DetentionCellInstance
                {
                    Building = building.Id,
                    ManagedBy = building.ParentBuilding,
                    BedCapacity = CountJailBeds(building.Id)
                });
            }
        }

        public bool TryBeginCustody(string simId, BuildingId cellBuilding, RoomId currentRoom, double cityTime,
            out CustodyAssignment assignment)
        {
            assignment = null;
            if (string.IsNullOrWhiteSpace(simId) || FindActive(simId) != null) return false;
            DetentionCellInstance cell = FindCell(cellBuilding);
            BuildingInstance manager = cell == null ? null : FindManager(cell.ManagedBy);
            if (cell == null || manager == null || manager.DetentionMeals == null
                || !manager.DetentionMeals.IsValid) return false;
            InteractiveObjectState bed = FindAvailableJailBed(cellBuilding, currentRoom);
            if (bed == null) return false;
            bed.AssignedSimIds.Add(simId);
            _objects.Touch();
            assignment = new CustodyAssignment
            {
                Id = new DetentionId("custody." + simId + "." + (++NextCustodySequence)),
                SimId = simId,
                CellBuilding = cell.Building,
                ManagedBy = cell.ManagedBy,
                Room = bed.Room,
                AssignedBed = bed.Id,
                StartsAt = cityTime,
                NextMealAt = manager.DetentionMeals.NextDueAfter(cityTime - 1d)
            };
            Custody.Add(assignment);
            return true;
        }

        public bool EndCustody(string simId, double cityTime)
        {
            CustodyAssignment assignment = FindActive(simId);
            if (assignment == null) return false;
            assignment.Active = false;
            assignment.EndsAt = cityTime;
            InteractiveObjectState bed = _objects.Find(assignment.AssignedBed);
            if (bed != null) bed.AssignedSimIds.Remove(simId);
            for (int i = 0; i < Meals.Count; i++)
                if (Meals[i] != null && Meals[i].Custody == assignment.Id
                    && (Meals[i].Status == JailMealStatus.Pending || Meals[i].Status == JailMealStatus.Claimed))
                    Meals[i].Status = JailMealStatus.Cancelled;
            _objects.Touch();
            return true;
        }

        public List<JailMealObligation> Advance(double cityTime)
        {
            var created = new List<JailMealObligation>();
            ExpireMeals(cityTime);
            for (int i = 0; i < Custody.Count; i++)
            {
                CustodyAssignment custody = Custody[i];
                if (custody == null || !custody.Active || custody.NextMealAt > cityTime) continue;
                BuildingInstance manager = FindManager(custody.ManagedBy);
                DetentionMealSchedule schedule = manager == null ? null : manager.DetentionMeals;
                if (schedule == null || !schedule.IsValid) continue;
                double recent = schedule.MostRecentDue(cityTime);
                double deadline = recent + (schedule.DeliveryWindowMinutes * 60d);
                double earlierEnd = recent - 0.001d;
                custody.MissedMealCount += schedule.CountDue(custody.NextMealAt, earlierEnd);
                if (cityTime <= deadline)
                {
                    var meal = new JailMealObligation
                    {
                        Id = "meal." + custody.Id.Value + "." + ((long)recent),
                        Custody = custody.Id,
                        PrisonerSimId = custody.SimId,
                        ManagedBy = custody.ManagedBy,
                        CellBuilding = custody.CellBuilding,
                        DeliveryRoom = custody.Room,
                        DueAt = recent,
                        Deadline = deadline,
                        Status = JailMealStatus.Pending
                    };
                    Meals.Add(meal);
                    created.Add(meal);
                }
                else custody.MissedMealCount++;
                custody.NextMealAt = schedule.NextDueAfter(recent);
            }
            return created;
        }

        public bool IsEligibleGuard(Sim sim, AuthoredTownContent content, BuildingId manager, double cityTime)
        {
            if (sim == null || sim.Employment == null || sim.Employment.Contract == null
                || !sim.Employment.Contract.Active || sim.Employment.Contract.Building != manager) return false;
            int minuteOfWeek = (int)(Math.Max(0d, cityTime) / 60d) % (7 * 24 * 60);
            DayOfWeek day = (DayOfWeek)(minuteOfWeek / (24 * 60));
            int minuteOfDay = minuteOfWeek % (24 * 60);
            if (!sim.Employment.Contract.Schedule.IsWorking(day, minuteOfDay)) return false;
            JobDefinition job = content.FindJob(sim.Employment.Contract.Job);
            if (job == null) return false;
            for (int i = 0; i < job.Duties.Count; i++)
                if (job.Duties[i] != null && job.Duties[i].Kind == DutyKind.FeedPrisoners) return true;
            return false;
        }

        public List<JailMealObligation> GetPendingForGuard(Sim guard, AuthoredTownContent content,
            double cityTime)
        {
            var result = new List<JailMealObligation>();
            for (int i = 0; i < Meals.Count; i++)
            {
                JailMealObligation meal = Meals[i];
                if (meal != null && meal.Status == JailMealStatus.Pending && cityTime <= meal.Deadline
                    && IsEligibleGuard(guard, content, meal.ManagedBy, cityTime)) result.Add(meal);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        public bool TryClaim(string obligationId, string guardSimId, double cityTime)
        {
            JailMealObligation meal = FindMeal(obligationId);
            if (meal == null || meal.Status != JailMealStatus.Pending || cityTime > meal.Deadline) return false;
            meal.Status = JailMealStatus.Claimed;
            meal.GuardSimId = guardSimId;
            return true;
        }

        public JailMealObligation Complete(string obligationId, string guardSimId, double cityTime)
        {
            JailMealObligation meal = FindMeal(obligationId);
            if (meal == null || meal.Status != JailMealStatus.Claimed
                || !string.Equals(meal.GuardSimId, guardSimId, StringComparison.Ordinal)
                || cityTime > meal.Deadline) return null;
            meal.Status = JailMealStatus.Delivered;
            meal.CompletedAt = cityTime;
            return meal;
        }

        public bool ReleaseClaim(string obligationId, string guardSimId)
        {
            JailMealObligation meal = FindMeal(obligationId);
            if (meal == null || meal.Status != JailMealStatus.Claimed
                || !string.Equals(meal.GuardSimId, guardSimId, StringComparison.Ordinal)) return false;
            meal.Status = JailMealStatus.Pending;
            meal.GuardSimId = null;
            return true;
        }

        public CustodyAssignment FindActive(string simId)
        {
            for (int i = 0; i < Custody.Count; i++)
                if (Custody[i] != null && Custody[i].Active
                    && string.Equals(Custody[i].SimId, simId, StringComparison.Ordinal)) return Custody[i];
            return null;
        }

        private void ExpireMeals(double cityTime)
        {
            for (int i = 0; i < Meals.Count; i++)
            {
                JailMealObligation meal = Meals[i];
                if (meal == null || cityTime <= meal.Deadline
                    || (meal.Status != JailMealStatus.Pending && meal.Status != JailMealStatus.Claimed)) continue;
                meal.Status = JailMealStatus.Missed;
                CustodyAssignment custody = FindCustody(meal.Custody);
                if (custody != null) custody.MissedMealCount++;
            }
        }

        private InteractiveObjectState FindAvailableJailBed(BuildingId building, RoomId room)
        {
            for (int i = 0; i < _objects.Objects.Count; i++)
            {
                InteractiveObjectState value = _objects.Objects[i];
                if (value == null || !value.Enabled || value.Building != building || value.Room != room) continue;
                int capacity = JailBedCapacity(value);
                if (value.AssignedSimIds.Count < capacity) return value;
            }
            return null;
        }

        private int CountJailBeds(BuildingId building)
        {
            int count = 0;
            for (int i = 0; i < _objects.Objects.Count; i++)
                if (_objects.Objects[i] != null && _objects.Objects[i].Building == building)
                    count += JailBedCapacity(_objects.Objects[i]);
            return count;
        }

        private int JailBedCapacity(InteractiveObjectState value)
        {
            InteractiveObjectDefinition definition = _objects.FindDefinition(value.Definition);
            if (definition == null) return 0;
            int count = 0;
            for (int i = 0; i < definition.Affordances.Count; i++)
                if (definition.Affordances[i] != null
                    && definition.Affordances[i].Sleeping == SleepingArrangement.Jail)
                    count += Math.Max(0, definition.Affordances[i].SlotCount);
            return count;
        }

        private DetentionCellInstance FindCell(BuildingId building)
        {
            for (int i = 0; i < Cells.Count; i++) if (Cells[i].Building == building) return Cells[i];
            return null;
        }

        private BuildingInstance FindManager(BuildingId building)
        {
            for (int i = 0; i < Managers.Count; i++) if (Managers[i].Id == building) return Managers[i];
            return null;
        }

        private CustodyAssignment FindCustody(DetentionId id)
        {
            for (int i = 0; i < Custody.Count; i++) if (Custody[i].Id == id) return Custody[i];
            return null;
        }

        private JailMealObligation FindMeal(string id)
        {
            for (int i = 0; i < Meals.Count; i++)
                if (Meals[i] != null && string.Equals(Meals[i].Id, id, StringComparison.Ordinal)) return Meals[i];
            return null;
        }
    }
}
