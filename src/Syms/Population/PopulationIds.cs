using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;

namespace Syms.Population
{
    [Serializable]
    public struct SimUid : IEquatable<SimUid>
    {
        public string Value;
        public SimUid(string value) { Value = value ?? string.Empty; }
        public bool Equals(SimUid other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is SimUid && Equals((SimUid)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(SimUid left, SimUid right) { return left.Equals(right); }
        public static bool operator !=(SimUid left, SimUid right) { return !left.Equals(right); }
    }

    [Serializable]
    public struct HouseholdUid : IEquatable<HouseholdUid>
    {
        public string Value;
        public HouseholdUid(string value) { Value = value ?? string.Empty; }
        public bool Equals(HouseholdUid other) { return string.Equals(Value, other.Value, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return obj is HouseholdUid && Equals((HouseholdUid)obj); }
        public override int GetHashCode() { return Value == null ? 0 : Value.GetHashCode(); }
        public override string ToString() { return Value ?? string.Empty; }
        public static bool operator ==(HouseholdUid left, HouseholdUid right) { return left.Equals(right); }
        public static bool operator !=(HouseholdUid left, HouseholdUid right) { return !left.Equals(right); }
    }

    /// <summary>Portable deterministic IDs. Never uses runtime-randomized string hashes.</summary>
    public static class StableUid
    {
        public static string Create(string scope, long seed, string stableKey)
        {
            if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("UID scope is required.", nameof(scope));
            if (string.IsNullOrWhiteSpace(stableKey)) throw new ArgumentException("UID stable key is required.", nameof(stableKey));
            string source = seed.ToString(CultureInfo.InvariantCulture) + "|" + scope + "|" + stableKey;
            ulong first = Hash64(source, 14695981039346656037UL);
            ulong second = Hash64(source, 1099511628211UL ^ 0x9e3779b97f4a7c15UL);
            return scope + "_" + first.ToString("x16", CultureInfo.InvariantCulture)
                + second.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static ulong Hash64(long seed, string stableKey)
        {
            string source = seed.ToString(CultureInfo.InvariantCulture) + "|" + (stableKey ?? string.Empty);
            return Hash64(source, 14695981039346656037UL);
        }

        private static ulong Hash64(string value, ulong offset)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            ulong hash = offset;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }

    /// <summary>
    /// Keyed randomness: each result depends on its stable key, not call order.
    /// Adding unrelated content therefore cannot reshuffle existing NPCs.
    /// </summary>
    public sealed class KeyedRandom
    {
        private readonly long _seed;
        public KeyedRandom(long seed) { _seed = seed; }

        public int Index(string key, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            return (int)(StableUid.Hash64(_seed, key) % (ulong)count);
        }

        public int RangeInclusive(string key, int minimum, int maximum)
        {
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            long width = ((long)maximum - minimum) + 1L;
            return minimum + (int)(StableUid.Hash64(_seed, key) % (ulong)width);
        }

        public float Unit(string key)
        {
            ulong value = StableUid.Hash64(_seed, key) >> 40;
            return value / 16777216f;
        }

        public string ChooseStable(string key, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("A stable choice requires at least one value.", nameof(values));
            string selected = values[0] ?? string.Empty;
            ulong selectedScore = StableUid.Hash64(_seed, key + "|" + selected);
            for (int i = 1; i < values.Count; i++)
            {
                string candidate = values[i] ?? string.Empty;
                ulong score = StableUid.Hash64(_seed, key + "|" + candidate);
                if (score > selectedScore
                    || (score == selectedScore && string.CompareOrdinal(candidate, selected) < 0))
                {
                    selected = candidate;
                    selectedScore = score;
                }
            }
            return selected;
        }
    }
}
