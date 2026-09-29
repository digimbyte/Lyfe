using System;
using System.Collections.Generic;

namespace Syms.Simulation.Stats
{
    /// <summary>
    /// Unity-serializable numeric state. Most values use a 0-100 scale; money is
    /// unbounded and poverty is derived by higher-level economic systems.
    /// </summary>
    [Serializable]
    public sealed class SimStats
    {
        public const float DefaultMinimum = 0f;
        public const float DefaultMaximum = 100f;

        public List<StatValue> Values = new List<StatValue>();

        public SimStats()
        {
            EnsureDefaults();
        }

        public float Get(SimStat stat)
        {
            StatValue entry = Find(stat);
            return entry == null ? 0f : entry.Value;
        }

        public void Set(SimStat stat, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A stat must be a finite number.");
            }

            StatValue entry = Find(stat);
            if (entry == null)
            {
                entry = new StatValue(stat, 0f);
                Values.Add(entry);
            }

            entry.Value = stat == SimStat.Money
                ? value
                : Clamp(value, DefaultMinimum, DefaultMaximum);
        }

        public float Adjust(SimStat stat, float delta)
        {
            Set(stat, Get(stat) + delta);
            return Get(stat);
        }

        public void EnsureDefaults()
        {
            Array stats = Enum.GetValues(typeof(SimStat));
            foreach (SimStat stat in stats)
            {
                if (Find(stat) == null)
                {
                    Values.Add(new StatValue(stat, GetInitialValue(stat)));
                }
            }
        }

        private StatValue Find(SimStat stat)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                if (Values[i] != null && Values[i].Stat == stat)
                {
                    return Values[i];
                }
            }

            return null;
        }

        private static float GetInitialValue(SimStat stat)
        {
            switch (stat)
            {
                case SimStat.Health:
                case SimStat.Energy:
                case SimStat.Hygiene:
                case SimStat.Social:
                case SimStat.Fun:
                case SimStat.Comfort:
                    return 100f;
                case SimStat.Money:
                    return 0f;
                default:
                    return 0f;
            }
        }

        public float GetUrgency(SimStat stat)
        {
            float value = Get(stat);
            switch (stat)
            {
                case SimStat.Health:
                case SimStat.Energy:
                case SimStat.Hygiene:
                case SimStat.Social:
                case SimStat.Fun:
                case SimStat.Comfort:
                    return (DefaultMaximum - value) / DefaultMaximum;
                case SimStat.Money:
                    return 0f;
                default:
                    return value / DefaultMaximum;
            }
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
