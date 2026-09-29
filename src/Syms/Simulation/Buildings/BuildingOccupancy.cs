using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.World;
using Syms.Simulation.Objects;

namespace Syms.Simulation.Buildings
{
    [Serializable]
    public sealed class ResidenceInstance
    {
        public BuildingId Building;
        public BuildingId ManagedBy;
        public PlaceId Place;
        public WorldAddress Address;
        /// <summary>Every authored room owned by this leased child building.</summary>
        public List<RoomId> Rooms = new List<RoomId>();
        public BuildingOccupancyKind Occupancy;
        /// <summary>Derived from authored usable bed slots in this building.</summary>
        public int ResidentCapacity;
        public float RatePerDay;
        public bool PopulationEligible = true;
    }

    public enum OccupancyLeaseStatus { Active, Ended, Cancelled }

    [Serializable]
    public sealed class OccupancyLease
    {
        public string Id;
        public BuildingId Residence;
        public BuildingId ManagedBy;
        public string HouseholdId;
        public List<string> ResidentSimIds = new List<string>();
        /// <summary>Index-aligned with ResidentSimIds; bed ownership is authoritative.</summary>
        public List<ObjectId> AssignedBeds = new List<ObjectId>();
        public double StartsAt;
        public double EndsAt;
        public float AgreedRatePerDay;
        public OccupancyLeaseStatus Status = OccupancyLeaseStatus.Active;

        public bool Overlaps(double startsAt, double endsAt)
        {
            return Status == OccupancyLeaseStatus.Active && startsAt < EndsAt && endsAt > StartsAt;
        }
    }

    public interface IOccupancyLeaseService
    {
        bool TryLease(BuildingId residence, string householdId, IList<string> residents,
            double startsAt, double endsAt, out OccupancyLease lease);
        bool End(string leaseId, double cityTime);
        OccupancyLease FindActive(BuildingId residence, double cityTime);
    }

    [Serializable]
    public sealed class OccupancyLeaseService : IOccupancyLeaseService
    {
        public List<ResidenceInstance> Residences = new List<ResidenceInstance>();
        public List<OccupancyLease> Leases = new List<OccupancyLease>();
        public long NextLeaseSequence;
        [NonSerialized] private ObjectStateStore _objects;

        public void Configure(ObjectStateStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public ResidenceInstance FindResidence(BuildingId id)
        {
            for (int i = 0; i < Residences.Count; i++)
                if (Residences[i] != null && Residences[i].Building == id) return Residences[i];
            return null;
        }

        public OccupancyLease FindActive(BuildingId residence, double cityTime)
        {
            ResidenceInstance residenceData = FindResidence(residence);
            for (int i = 0; i < Leases.Count; i++)
            {
                OccupancyLease value = Leases[i];
                if (value == null || value.Residence != residence
                    || value.Status != OccupancyLeaseStatus.Active || cityTime < value.StartsAt) continue;
                if (residenceData != null && residenceData.Occupancy != BuildingOccupancyKind.LodgingResidence)
                    return value;
                if (cityTime < value.EndsAt) return value;
            }
            return null;
        }

        public OccupancyLease Find(string leaseId)
        {
            for (int i = 0; i < Leases.Count; i++)
                if (Leases[i] != null && string.Equals(Leases[i].Id, leaseId, StringComparison.Ordinal))
                    return Leases[i];
            return null;
        }

        public bool TryLease(BuildingId residenceId, string householdId, IList<string> residents,
            double startsAt, double endsAt, out OccupancyLease lease)
        {
            lease = null;
            ResidenceInstance residence = FindResidence(residenceId);
            if (residence == null || endsAt <= startsAt
                || residence.Occupancy == BuildingOccupancyKind.DetentionCell) return false;
            for (int i = 0; i < Leases.Count; i++)
                if (Leases[i] != null && Leases[i].Residence == residenceId
                    && Leases[i].Status == OccupancyLeaseStatus.Active
                    && (residence.Occupancy != BuildingOccupancyKind.LodgingResidence
                        || Leases[i].Overlaps(startsAt, endsAt))) return false;
            int count = residents == null ? 0 : residents.Count;
            if (count <= 0 || count > residence.ResidentCapacity) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < residents.Count; i++)
                if (string.IsNullOrWhiteSpace(residents[i]) || !unique.Add(residents[i])) return false;
            var beds = new List<InteractiveObjectState>();
            for (int i = 0; i < residents.Count; i++)
            {
                InteractiveObjectState bed = FindAvailableBed(residence);
                if (bed == null)
                {
                    for (int undo = 0; undo < beds.Count; undo++) beds[undo].AssignedSimIds.Remove(residents[undo]);
                    return false;
                }
                bed.AssignedSimIds.Add(residents[i]);
                beds.Add(bed);
            }
            lease = new OccupancyLease
            {
                Id = "lease." + residenceId.Value + "." + (++NextLeaseSequence),
                Residence = residenceId,
                ManagedBy = residence.ManagedBy,
                HouseholdId = householdId ?? string.Empty,
                ResidentSimIds = new List<string>(residents),
                StartsAt = startsAt,
                EndsAt = endsAt,
                AgreedRatePerDay = residence.RatePerDay
            };
            for (int i = 0; i < beds.Count; i++) lease.AssignedBeds.Add(beds[i].Id);
            Leases.Add(lease);
            _objects.Touch();
            return true;
        }

        public bool End(string leaseId, double cityTime)
        {
            for (int i = 0; i < Leases.Count; i++)
            {
                OccupancyLease lease = Leases[i];
                if (lease == null || lease.Status != OccupancyLeaseStatus.Active
                    || !string.Equals(lease.Id, leaseId, StringComparison.Ordinal)) continue;
                lease.EndsAt = Math.Min(lease.EndsAt, cityTime);
                lease.Status = OccupancyLeaseStatus.Ended;
                for (int resident = 0; resident < lease.ResidentSimIds.Count; resident++)
                {
                    if (resident >= lease.AssignedBeds.Count) break;
                    InteractiveObjectState bed = _objects.Find(lease.AssignedBeds[resident]);
                    if (bed != null) bed.AssignedSimIds.Remove(lease.ResidentSimIds[resident]);
                }
                _objects.Touch();
                return true;
            }
            return false;
        }

        public List<OccupancyLease> Expire(double cityTime)
        {
            var expired = new List<OccupancyLease>();
            for (int i = 0; i < Leases.Count; i++)
            {
                OccupancyLease lease = Leases[i];
                if (lease == null || lease.Status != OccupancyLeaseStatus.Active
                    || lease.EndsAt > cityTime) continue;
                ResidenceInstance residence = FindResidence(lease.Residence);
                if (residence == null || residence.Occupancy != BuildingOccupancyKind.LodgingResidence) continue;
                if (End(lease.Id, lease.EndsAt)) expired.Add(lease);
            }
            return expired;
        }

        private InteractiveObjectState FindAvailableBed(ResidenceInstance residence)
        {
            if (_objects == null) throw new InvalidOperationException("Occupancy leases require the authored object store.");
            for (int i = 0; i < _objects.Objects.Count; i++)
            {
                InteractiveObjectState value = _objects.Objects[i];
                if (value == null || !value.Enabled || value.Building != residence.Building) continue;
                InteractiveObjectDefinition definition = _objects.FindDefinition(value.Definition);
                if (definition == null) continue;
                int capacity = 0;
                for (int a = 0; a < definition.Affordances.Count; a++)
                {
                    ObjectAffordanceDefinition affordance = definition.Affordances[a];
                    if (affordance != null && IsLeaseBed(affordance.Sleeping, residence.Occupancy))
                        capacity += Math.Max(0, affordance.SlotCount);
                }
                if (value.AssignedSimIds.Count < capacity) return value;
            }
            return null;
        }

        private static bool IsLeaseBed(SleepingArrangement arrangement, BuildingOccupancyKind occupancy)
        {
            if (occupancy == BuildingOccupancyKind.LodgingResidence)
                return arrangement == SleepingArrangement.Hotel;
            if (occupancy == BuildingOccupancyKind.InstitutionalResidence)
                return arrangement == SleepingArrangement.Institutional || arrangement == SleepingArrangement.Bunk
                    || arrangement == SleepingArrangement.PrivateSingle;
            return arrangement == SleepingArrangement.PrivateSingle || arrangement == SleepingArrangement.SharedBed
                || arrangement == SleepingArrangement.Bunk;
        }
    }
}
