using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.World;

namespace Syms.Simulation.Objects
{
    [Serializable]
    public sealed class ObjectStateStore
    {
        public List<InteractiveObjectDefinition> Definitions = new List<InteractiveObjectDefinition>();
        public List<InteractiveObjectState> Objects = new List<InteractiveObjectState>();
        public List<ObjectAffordanceSlot> Slots = new List<ObjectAffordanceSlot>();
        public long Revision;

        [NonSerialized] private Dictionary<ObjectDefinitionId, InteractiveObjectDefinition> _definitions;
        [NonSerialized] private Dictionary<ObjectId, InteractiveObjectState> _objects;
        [NonSerialized] private Dictionary<ObjectId, List<ObjectAffordanceSlot>> _slotsByObject;
        [NonSerialized] private Dictionary<RoomId, List<InteractiveObjectState>> _objectsByRoom;
        [NonSerialized] private Dictionary<BuildingId, List<InteractiveObjectState>> _objectsByBuilding;
        [NonSerialized] private Dictionary<string, List<InteractiveObjectState>> _objectsByOwner;
        [NonSerialized] private int _indexedDefinitionCount = -1;
        [NonSerialized] private int _indexedObjectCount = -1;
        [NonSerialized] private int _indexedSlotCount = -1;

        public InteractiveObjectDefinition FindDefinition(ObjectDefinitionId id)
        {
            EnsureIndexes();
            InteractiveObjectDefinition definition;
            return _definitions.TryGetValue(id, out definition) ? definition : null;
        }

        public InteractiveObjectState Find(ObjectId id)
        {
            EnsureIndexes();
            InteractiveObjectState value;
            return _objects.TryGetValue(id, out value) ? value : null;
        }

        public IReadOnlyList<InteractiveObjectState> FindByRoom(RoomId room)
        {
            EnsureIndexes();
            List<InteractiveObjectState> values;
            if (_objectsByRoom.TryGetValue(room, out values)) return values;
            return Array.Empty<InteractiveObjectState>();
        }

        public IReadOnlyList<InteractiveObjectState> FindByBuilding(BuildingId building)
        {
            EnsureIndexes();
            List<InteractiveObjectState> values;
            if (_objectsByBuilding.TryGetValue(building, out values)) return values;
            return Array.Empty<InteractiveObjectState>();
        }

        public IReadOnlyList<ObjectAffordanceSlot> GetSlots(ObjectId objectId)
        {
            EnsureIndexes();
            List<ObjectAffordanceSlot> values;
            if (_slotsByObject.TryGetValue(objectId, out values)) return values;
            return Array.Empty<ObjectAffordanceSlot>();
        }

        public IReadOnlyList<InteractiveObjectState> FindByOwner(OwnershipKind kind, string ownerId)
        {
            EnsureIndexes();
            List<InteractiveObjectState> values;
            if (_objectsByOwner.TryGetValue(OwnerKey(kind, ownerId), out values)) return values;
            return Array.Empty<InteractiveObjectState>();
        }

        public bool HasAvailableSlot(ObjectId objectId, string affordanceLocalId, double cityTime)
        {
            IReadOnlyList<ObjectAffordanceSlot> slots = GetSlots(objectId);
            for (int i = 0; i < slots.Count; i++)
                if (string.Equals(slots[i].AffordanceLocalId, affordanceLocalId, StringComparison.Ordinal)
                    && slots[i].IsAvailable(cityTime)) return true;
            return false;
        }

        public bool HasAffordance(RoomId room, ActivityKind activity)
        {
            IReadOnlyList<InteractiveObjectState> values = FindByRoom(room);
            for (int i = 0; i < values.Count; i++)
            {
                InteractiveObjectDefinition definition = FindDefinition(values[i].Definition);
                if (definition == null) continue;
                for (int affordanceIndex = 0; affordanceIndex < definition.Affordances.Count; affordanceIndex++)
                    if (definition.Affordances[affordanceIndex] != null
                        && definition.Affordances[affordanceIndex].Activity == activity) return true;
            }
            return false;
        }

        public bool TryReserve(
            ObjectId objectId,
            string affordanceLocalId,
            string simId,
            double cityTime,
            double reservedUntil,
            out ObjectAffordanceSlot slot)
        {
            if (string.IsNullOrWhiteSpace(simId)) throw new ArgumentException("A Sim ID is required.", nameof(simId));
            if (reservedUntil <= cityTime) throw new ArgumentOutOfRangeException(nameof(reservedUntil));
            EnsureIndexes();
            List<ObjectAffordanceSlot> slots;
            if (_slotsByObject.TryGetValue(objectId, out slots))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    ObjectAffordanceSlot candidate = slots[i];
                    if (string.Equals(candidate.AffordanceLocalId, affordanceLocalId, StringComparison.Ordinal)
                        && candidate.IsAvailable(cityTime))
                    {
                        candidate.ReservedBySimId = simId;
                        candidate.ReservedUntil = reservedUntil;
                        Revision++;
                        slot = candidate;
                        return true;
                    }
                }
            }
            slot = null;
            return false;
        }

        public bool Release(AffordanceSlotId id, string simId)
        {
            EnsureIndexes();
            for (int i = 0; i < Slots.Count; i++)
            {
                ObjectAffordanceSlot slot = Slots[i];
                if (slot == null || slot.Id != id) continue;
                if (!string.IsNullOrEmpty(simId)
                    && !string.Equals(slot.ReservedBySimId, simId, StringComparison.Ordinal)) return false;
                slot.ReservedBySimId = null;
                slot.ReservedUntil = 0d;
                Revision++;
                return true;
            }
            return false;
        }

        public void ReleaseExpired(double cityTime)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                ObjectAffordanceSlot slot = Slots[i];
                if (slot != null && !string.IsNullOrEmpty(slot.ReservedBySimId) && slot.ReservedUntil <= cityTime)
                {
                    slot.ReservedBySimId = null;
                    slot.ReservedUntil = 0d;
                    Revision++;
                }
            }
        }

        public bool ApplyDamage(ObjectId id, float amount)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.ApplyDamage(amount);
            Revision++;
            return true;
        }

        public bool Repair(ObjectId id, float amount)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.Repair(amount);
            Revision++;
            return true;
        }

        public bool SetCleanliness(ObjectId id, float cleanliness)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.SetCleanliness(cleanliness);
            Revision++;
            return true;
        }

        public bool SetCondition(ObjectId id, ObjectConditionFlags condition, bool enabled)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.SetCondition(condition, enabled);
            Revision++;
            return true;
        }

        public bool SetOwnership(ObjectId id, ObjectOwnership ownership)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.Ownership = ownership ?? new ObjectOwnership();
            _indexedObjectCount = -1;
            Revision++;
            return true;
        }

        public bool SetAssignedSims(ObjectId id, IList<string> simIds)
        {
            InteractiveObjectState value = Find(id);
            if (value == null) return false;
            value.AssignedSimIds.Clear();
            if (simIds != null)
                for (int i = 0; i < simIds.Count; i++) value.AssignedSimIds.Add(simIds[i]);
            Revision++;
            return true;
        }

        public void Touch() { Revision++; }

        public void RebuildSlots()
        {
            Slots.Clear();
            EnsureIndexes(true);
            for (int objectIndex = 0; objectIndex < Objects.Count; objectIndex++)
            {
                InteractiveObjectState value = Objects[objectIndex];
                if (value == null || !value.Enabled) continue;
                InteractiveObjectDefinition definition = FindDefinition(value.Definition);
                if (definition == null) continue;
                for (int affordanceIndex = 0; affordanceIndex < definition.Affordances.Count; affordanceIndex++)
                {
                    ObjectAffordanceDefinition affordance = definition.Affordances[affordanceIndex];
                    if (affordance == null) continue;
                    for (int slotIndex = 0; slotIndex < Math.Max(0, affordance.SlotCount); slotIndex++)
                        Slots.Add(new ObjectAffordanceSlot
                        {
                            Id = new AffordanceSlotId(value.Id.Value + "/affordances/" + affordance.LocalId + "/" + slotIndex),
                            Object = value.Id,
                            AffordanceLocalId = affordance.LocalId,
                            SlotIndex = slotIndex
                        });
                }
            }
            _indexedSlotCount = -1;
            Revision++;
            EnsureIndexes();
        }

        private void EnsureIndexes() { EnsureIndexes(false); }

        private void EnsureIndexes(bool force)
        {
            if (!force && _definitions != null && _objects != null && _slotsByObject != null
                && _objectsByRoom != null && _objectsByBuilding != null && _objectsByOwner != null
                && _indexedDefinitionCount == Definitions.Count
                && _indexedObjectCount == Objects.Count
                && _indexedSlotCount == Slots.Count) return;
            _definitions = new Dictionary<ObjectDefinitionId, InteractiveObjectDefinition>();
            _objects = new Dictionary<ObjectId, InteractiveObjectState>();
            _slotsByObject = new Dictionary<ObjectId, List<ObjectAffordanceSlot>>();
            _objectsByRoom = new Dictionary<RoomId, List<InteractiveObjectState>>();
            _objectsByBuilding = new Dictionary<BuildingId, List<InteractiveObjectState>>();
            _objectsByOwner = new Dictionary<string, List<InteractiveObjectState>>(StringComparer.Ordinal);
            for (int i = 0; i < Definitions.Count; i++)
                if (Definitions[i] != null) _definitions[Definitions[i].Id] = Definitions[i];
            for (int i = 0; i < Objects.Count; i++)
            {
                InteractiveObjectState value = Objects[i];
                if (value == null) continue;
                _objects[value.Id] = value;
                List<InteractiveObjectState> roomObjects;
                if (!_objectsByRoom.TryGetValue(value.Room, out roomObjects))
                {
                    roomObjects = new List<InteractiveObjectState>();
                    _objectsByRoom.Add(value.Room, roomObjects);
                }
                roomObjects.Add(value);
                List<InteractiveObjectState> buildingObjects;
                if (!_objectsByBuilding.TryGetValue(value.Building, out buildingObjects))
                {
                    buildingObjects = new List<InteractiveObjectState>();
                    _objectsByBuilding.Add(value.Building, buildingObjects);
                }
                buildingObjects.Add(value);
                ObjectOwnership ownership = value.Ownership ?? new ObjectOwnership();
                string ownerKey = OwnerKey(ownership.Kind, ownership.OwnerId);
                List<InteractiveObjectState> ownedObjects;
                if (!_objectsByOwner.TryGetValue(ownerKey, out ownedObjects))
                {
                    ownedObjects = new List<InteractiveObjectState>();
                    _objectsByOwner.Add(ownerKey, ownedObjects);
                }
                ownedObjects.Add(value);
            }
            for (int i = 0; i < Slots.Count; i++)
            {
                ObjectAffordanceSlot slot = Slots[i];
                if (slot == null) continue;
                List<ObjectAffordanceSlot> objectSlots;
                if (!_slotsByObject.TryGetValue(slot.Object, out objectSlots))
                {
                    objectSlots = new List<ObjectAffordanceSlot>();
                    _slotsByObject.Add(slot.Object, objectSlots);
                }
                objectSlots.Add(slot);
            }
            _indexedDefinitionCount = Definitions.Count;
            _indexedObjectCount = Objects.Count;
            _indexedSlotCount = Slots.Count;
        }

        private static string OwnerKey(OwnershipKind kind, string ownerId)
        {
            return ((int)kind).ToString() + ":" + (ownerId ?? string.Empty);
        }
    }
}
