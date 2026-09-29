using System;
using System.Collections.Generic;
using Syms.Simulation.World;

namespace Syms.Simulation.Activities
{
    public enum FacilityKind
    {
        Generic,
        Bed,
        Toilet,
        Desk,
        ServiceCounter,
        Seat,
        Shower,
        Forge,
        Oven,
        Workbench,
        Stable,
        Field,
        Pulpit,
        Classroom,
        Table,
        Hearth,
        WashBasin,
        Bath,
        Crib,
        Wardrobe,
        Storage,
        Shelf,
        DisplayCase,
        BarCounter,
        KitchenRange,
        DiningTable,
        Stage,
        Piano,
        LibraryShelf,
        MedicalCot,
        SurgeryTable,
        MorgueSlab,
        GravePlot,
        PrisonCell,
        Gallows,
        MarketStall,
        Dock,
        Loom,
        Mill,
        BreweryVat,
        Distillery,
        LaundryTub,
        ChapelAltar,
        GardenPlot,
        PlaygroundEquipment,
        SportsField,
        ReceptionDesk,
        WaitingBench,
        CarriageBay
    }

    [Serializable]
    public struct CapacityKey : IEquatable<CapacityKey>
    {
        public PlaceId Place;
        public FacilityKind Facility;

        public CapacityKey(PlaceId place, FacilityKind facility) { Place = place; Facility = facility; }
        public bool Equals(CapacityKey other) { return Place == other.Place && Facility == other.Facility; }
        public override bool Equals(object obj) { return obj is CapacityKey && Equals((CapacityKey)obj); }
        public override int GetHashCode() { return Place.GetHashCode() ^ ((int)Facility << 8); }
    }

    [Serializable]
    public sealed class CapacityReservation
    {
        public string Id;
        public CapacityKey Key;
        public string SimId;
        public double ExpiresAt;
    }

    [Serializable]
    public sealed class CapacityDefinition
    {
        public CapacityKey Key;
        public int Capacity;
    }

    public interface ICapacityService
    {
        void SetCapacity(CapacityKey key, int capacity);
        bool HasAvailability(CapacityKey key);
        bool TryReserve(CapacityKey key, string simId, double expiresAt, out CapacityReservation reservation);
        bool Release(string reservationId);
        void ReleaseExpired(double cityTime);
    }

    [Serializable]
    public sealed class CapacityService : ICapacityService
    {
        public List<CapacityDefinition> Definitions = new List<CapacityDefinition>();
        public List<CapacityReservation> Reservations = new List<CapacityReservation>();
        public long NextId;
        [NonSerialized] private Dictionary<CapacityKey, CapacityDefinition> _capacities;
        [NonSerialized] private Dictionary<string, CapacityReservation> _reservations;

        public void SetCapacity(CapacityKey key, int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            EnsureIndexes();
            CapacityDefinition definition;
            if (!_capacities.TryGetValue(key, out definition))
            {
                definition = new CapacityDefinition { Key = key };
                Definitions.Add(definition);
                _capacities.Add(key, definition);
            }
            definition.Capacity = capacity;
        }

        public bool TryReserve(CapacityKey key, string simId, double expiresAt, out CapacityReservation reservation)
        {
            EnsureIndexes();
            CapacityDefinition definition;
            if (!_capacities.TryGetValue(key, out definition) || definition.Capacity <= CountActive(key))
            {
                reservation = null;
                return false;
            }

            reservation = new CapacityReservation
            {
                Id = "reservation." + (++NextId).ToString(),
                Key = key,
                SimId = simId ?? string.Empty,
                ExpiresAt = expiresAt
            };
            _reservations.Add(reservation.Id, reservation);
            Reservations.Add(reservation);
            return true;
        }

        public bool HasAvailability(CapacityKey key)
        {
            EnsureIndexes();
            CapacityDefinition definition;
            return _capacities.TryGetValue(key, out definition) && definition.Capacity > CountActive(key);
        }

        public bool Release(string reservationId)
        {
            EnsureIndexes();
            CapacityReservation reservation;
            if (reservationId == null || !_reservations.TryGetValue(reservationId, out reservation)) return false;
            _reservations.Remove(reservationId);
            Reservations.Remove(reservation);
            return true;
        }

        public void ReleaseExpired(double cityTime)
        {
            EnsureIndexes();
            var expired = new List<string>();
            foreach (KeyValuePair<string, CapacityReservation> pair in _reservations)
                if (pair.Value.ExpiresAt <= cityTime) expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++) Release(expired[i]);
        }

        private int CountActive(CapacityKey key)
        {
            int count = 0;
            foreach (CapacityReservation reservation in _reservations.Values)
                if (reservation.Key.Equals(key)) count++;
            return count;
        }

        private void EnsureIndexes()
        {
            if (_capacities != null && _capacities.Count == Definitions.Count
                && _reservations != null && _reservations.Count == Reservations.Count) return;
            _capacities = new Dictionary<CapacityKey, CapacityDefinition>();
            _reservations = new Dictionary<string, CapacityReservation>(StringComparer.Ordinal);
            for (int i = 0; i < Definitions.Count; i++)
                if (Definitions[i] != null) _capacities[Definitions[i].Key] = Definitions[i];
            for (int i = 0; i < Reservations.Count; i++)
                if (Reservations[i] != null) _reservations[Reservations[i].Id] = Reservations[i];
        }
    }
}
