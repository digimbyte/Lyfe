using System;
using System.Collections.Generic;
using Syms.Simulation.People;

namespace Syms.Simulation.Social
{
    public enum RelationshipKind
    {
        Family,
        Partner,
        ExPartner,
        Friend,
        Rival,
        Enemy,
        Neighbour,
        Colleague,
        Patron,
        Dependent,
        Acquaintance,
        Household,
        Roommate
    }

    public enum FamilyRole
    {
        None,
        Parent,
        Child,
        Sibling,
        Spouse,
        Relative
    }

    [Serializable]
    public sealed class SocialRelationship
    {
        public string Id;
        public string FirstSimId;
        public string SecondSimId;
        public List<RelationshipKind> Kinds = new List<RelationshipKind>();
        public FamilyRole FirstFamilyRole;
        public FamilyRole SecondFamilyRole;
        public int Affinity;
        public double StartedAt;
        public double ChangedAt;

        public bool Involves(string simId)
        {
            return string.Equals(FirstSimId, simId, StringComparison.Ordinal)
                || string.Equals(SecondSimId, simId, StringComparison.Ordinal);
        }

        public bool Connects(string firstSimId, string secondSimId)
        {
            return (string.Equals(FirstSimId, firstSimId, StringComparison.Ordinal)
                    && string.Equals(SecondSimId, secondSimId, StringComparison.Ordinal))
                || (string.Equals(FirstSimId, secondSimId, StringComparison.Ordinal)
                    && string.Equals(SecondSimId, firstSimId, StringComparison.Ordinal));
        }

        public bool HasKind(RelationshipKind kind) { return Kinds.Contains(kind); }

        public string GetOtherSimId(string simId)
        {
            if (string.Equals(FirstSimId, simId, StringComparison.Ordinal)) return SecondSimId;
            if (string.Equals(SecondSimId, simId, StringComparison.Ordinal)) return FirstSimId;
            return null;
        }

        public FamilyRole GetFamilyRole(string simId)
        {
            if (string.Equals(FirstSimId, simId, StringComparison.Ordinal)) return FirstFamilyRole;
            if (string.Equals(SecondSimId, simId, StringComparison.Ordinal)) return SecondFamilyRole;
            return FamilyRole.None;
        }

        public void SetAffinity(int value) { Affinity = ClampAffinity(value); }
        public void AdjustAffinity(int delta) { Affinity = ClampAffinity(Affinity + delta); }

        private static int ClampAffinity(int value)
        {
            if (value < -100) return -100;
            return value > 100 ? 100 : value;
        }
    }

    [Serializable]
    public sealed class RelationshipGraph
    {
        public List<SocialRelationship> Relationships = new List<SocialRelationship>();
        [NonSerialized] private Dictionary<string, SocialRelationship> _byPair;
        [NonSerialized] private Dictionary<string, List<SocialRelationship>> _bySim;
        [NonSerialized] private int _indexedCount = -1;

        public SocialRelationship Find(string firstSimId, string secondSimId)
        {
            EnsureIndex();
            SocialRelationship relationship;
            return _byPair.TryGetValue(PairKey(firstSimId, secondSimId), out relationship)
                ? relationship : null;
        }

        public List<SocialRelationship> GetForSim(string simId)
        {
            EnsureIndex();
            List<SocialRelationship> relationships;
            return simId != null && _bySim.TryGetValue(simId, out relationships)
                ? new List<SocialRelationship>(relationships)
                : new List<SocialRelationship>();
        }

        public SocialRelationship GetOrCreate(string firstSimId, string secondSimId, double cityTime)
        {
            ValidatePair(firstSimId, secondSimId);
            SocialRelationship existing = Find(firstSimId, secondSimId);
            if (existing != null) return existing;

            string lower = string.CompareOrdinal(firstSimId, secondSimId) <= 0 ? firstSimId : secondSimId;
            string upper = string.CompareOrdinal(firstSimId, secondSimId) <= 0 ? secondSimId : firstSimId;
            var relationship = new SocialRelationship
            {
                Id = "relationship/" + lower + "/" + upper,
                FirstSimId = firstSimId,
                SecondSimId = secondSimId,
                StartedAt = cityTime,
                ChangedAt = cityTime
            };
            Relationships.Add(relationship);
            _byPair[PairKey(firstSimId, secondSimId)] = relationship;
            AddToSimIndex(firstSimId, relationship);
            AddToSimIndex(secondSimId, relationship);
            _indexedCount = Relationships.Count;
            return relationship;
        }

        public void Clear()
        {
            Relationships.Clear();
            _byPair = null;
            _bySim = null;
            _indexedCount = -1;
        }

        public void Upsert(SocialRelationship relationship)
        {
            if (relationship == null) throw new ArgumentNullException(nameof(relationship));
            ValidatePair(relationship.FirstSimId, relationship.SecondSimId);
            SocialRelationship existing = Find(relationship.FirstSimId, relationship.SecondSimId);
            if (existing != null)
            {
                int index = Relationships.IndexOf(existing);
                Relationships[index] = relationship;
                _byPair = null;
                _bySim = null;
                _indexedCount = -1;
                EnsureIndex();
            }
            else
            {
                Relationships.Add(relationship);
                _byPair[PairKey(relationship.FirstSimId, relationship.SecondSimId)] = relationship;
                AddToSimIndex(relationship.FirstSimId, relationship);
                AddToSimIndex(relationship.SecondSimId, relationship);
            }
            _indexedCount = Relationships.Count;
        }

        public SocialRelationship SetPartners(string firstSimId, string secondSimId, double cityTime)
        {
            ValidatePair(firstSimId, secondSimId);
            EndOtherPartnerships(firstSimId, secondSimId, cityTime);
            EndOtherPartnerships(secondSimId, firstSimId, cityTime);
            SocialRelationship relationship = GetOrCreate(firstSimId, secondSimId, cityTime);
            RemoveKind(relationship, RelationshipKind.ExPartner);
            AddKind(relationship, RelationshipKind.Partner);
            relationship.ChangedAt = cityTime;
            return relationship;
        }

        public bool EndPartnership(string firstSimId, string secondSimId, double cityTime)
        {
            SocialRelationship relationship = Find(firstSimId, secondSimId);
            if (relationship == null || !relationship.HasKind(RelationshipKind.Partner)) return false;
            RemoveKind(relationship, RelationshipKind.Partner);
            AddKind(relationship, RelationshipKind.ExPartner);
            relationship.ChangedAt = cityTime;
            return true;
        }

        public bool EndMarriage(string firstSimId, string secondSimId, double cityTime)
        {
            SocialRelationship relationship = Find(firstSimId, secondSimId);
            if (relationship == null) return false;
            bool wasMarriage = relationship.HasKind(RelationshipKind.Partner)
                && relationship.GetFamilyRole(firstSimId) == FamilyRole.Spouse
                && relationship.GetFamilyRole(secondSimId) == FamilyRole.Spouse;
            if (!wasMarriage) return false;

            RemoveKind(relationship, RelationshipKind.Partner);
            RemoveKind(relationship, RelationshipKind.Family);
            AddKind(relationship, RelationshipKind.ExPartner);
            relationship.FirstFamilyRole = FamilyRole.None;
            relationship.SecondFamilyRole = FamilyRole.None;
            relationship.ChangedAt = cityTime;
            return true;
        }

        public SocialRelationship SetFamily(
            string firstSimId,
            FamilyRole firstRole,
            string secondSimId,
            FamilyRole secondRole,
            double cityTime)
        {
            ValidateFamilyRoles(firstRole, secondRole);
            SocialRelationship relationship = GetOrCreate(firstSimId, secondSimId, cityTime);
            AddKind(relationship, RelationshipKind.Family);
            if (string.Equals(relationship.FirstSimId, firstSimId, StringComparison.Ordinal))
            {
                relationship.FirstFamilyRole = firstRole;
                relationship.SecondFamilyRole = secondRole;
            }
            else
            {
                relationship.FirstFamilyRole = secondRole;
                relationship.SecondFamilyRole = firstRole;
            }
            relationship.ChangedAt = cityTime;
            return relationship;
        }

        public SocialRelationship SetAffinity(string firstSimId, string secondSimId, int affinity, double cityTime)
        {
            SocialRelationship relationship = GetOrCreate(firstSimId, secondSimId, cityTime);
            relationship.SetAffinity(affinity);
            relationship.ChangedAt = cityTime;
            return relationship;
        }

        public SocialRelationship AddKind(
            string firstSimId,
            string secondSimId,
            RelationshipKind kind,
            double cityTime)
        {
            SocialRelationship relationship = GetOrCreate(firstSimId, secondSimId, cityTime);
            AddKind(relationship, kind);
            relationship.ChangedAt = cityTime;
            return relationship;
        }

        public void ClearDerivedKinds(IReadOnlyList<RelationshipKind> kinds, double cityTime)
        {
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            for (int i = Relationships.Count - 1; i >= 0; i--)
            {
                SocialRelationship relationship = Relationships[i];
                if (relationship == null)
                {
                    Relationships.RemoveAt(i);
                    continue;
                }

                bool changed = false;
                for (int kindIndex = 0; kindIndex < kinds.Count; kindIndex++)
                    changed |= relationship.Kinds.Remove(kinds[kindIndex]);
                if (!changed) continue;
                relationship.ChangedAt = cityTime;

                // A meaningful opinion survives the context that introduced the
                // Sims; neutral, context-only records are discarded to stay sparse.
                if (relationship.Kinds.Count == 0 && relationship.Affinity != 0)
                    AddKind(relationship, RelationshipKind.Acquaintance);
                if (relationship.Kinds.Count == 0)
                    Relationships.RemoveAt(i);
            }
            _byPair = null;
            _bySim = null;
            _indexedCount = -1;
        }

        private void EndOtherPartnerships(string simId, string intendedPartnerId, double cityTime)
        {
            List<SocialRelationship> relationships = GetForSim(simId);
            for (int i = 0; i < relationships.Count; i++)
            {
                SocialRelationship relationship = relationships[i];
                if (relationship == null || !relationship.Involves(simId)
                    || relationship.Connects(simId, intendedPartnerId)
                    || !relationship.HasKind(RelationshipKind.Partner)) continue;
                RemoveKind(relationship, RelationshipKind.Partner);
                AddKind(relationship, RelationshipKind.ExPartner);
                relationship.ChangedAt = cityTime;
            }
        }

        private static void ValidatePair(string firstSimId, string secondSimId)
        {
            if (string.IsNullOrWhiteSpace(firstSimId)) throw new ArgumentException("First Sim ID is required.", nameof(firstSimId));
            if (string.IsNullOrWhiteSpace(secondSimId)) throw new ArgumentException("Second Sim ID is required.", nameof(secondSimId));
            if (string.Equals(firstSimId, secondSimId, StringComparison.Ordinal))
                throw new ArgumentException("A Sim cannot have a relationship with itself.");
        }

        private static void ValidateFamilyRoles(FamilyRole first, FamilyRole second)
        {
            bool valid = (first == FamilyRole.Parent && second == FamilyRole.Child)
                || (first == FamilyRole.Child && second == FamilyRole.Parent)
                || (first == FamilyRole.Sibling && second == FamilyRole.Sibling)
                || (first == FamilyRole.Spouse && second == FamilyRole.Spouse)
                || (first == FamilyRole.Relative && second == FamilyRole.Relative);
            if (!valid) throw new ArgumentException("Family roles must form a valid reciprocal pair.");
        }

        private static void AddKind(SocialRelationship relationship, RelationshipKind kind)
        {
            if (!relationship.Kinds.Contains(kind)) relationship.Kinds.Add(kind);
        }

        private static void RemoveKind(SocialRelationship relationship, RelationshipKind kind)
        {
            relationship.Kinds.Remove(kind);
        }

        private void EnsureIndex()
        {
            if (_byPair != null && _indexedCount == Relationships.Count) return;
            _byPair = new Dictionary<string, SocialRelationship>(StringComparer.Ordinal);
            _bySim = new Dictionary<string, List<SocialRelationship>>(StringComparer.Ordinal);
            for (int i = 0; i < Relationships.Count; i++)
            {
                SocialRelationship relationship = Relationships[i];
                if (relationship == null) continue;
                _byPair[PairKey(relationship.FirstSimId, relationship.SecondSimId)] = relationship;
                AddToSimIndex(relationship.FirstSimId, relationship);
                AddToSimIndex(relationship.SecondSimId, relationship);
            }
            _indexedCount = Relationships.Count;
        }

        private static string PairKey(string firstSimId, string secondSimId)
        {
            return string.CompareOrdinal(firstSimId, secondSimId) <= 0
                ? firstSimId + "\u001f" + secondSimId
                : secondSimId + "\u001f" + firstSimId;
        }

        private void AddToSimIndex(string simId, SocialRelationship relationship)
        {
            List<SocialRelationship> relationships;
            if (!_bySim.TryGetValue(simId, out relationships))
            {
                relationships = new List<SocialRelationship>();
                _bySim.Add(simId, relationships);
            }
            relationships.Add(relationship);
        }
    }
}
