using System;
using Syms.Simulation.People;

namespace Syms.Simulation.Stats
{
    /// <summary>
    /// Tunable analytical hardship rates. Values are per simulated day so the
    /// same result is produced whether time advances in minutes or long gaps.
    /// </summary>
    [Serializable]
    public sealed class EconomicPressureSettings
    {
        public float UnemployedPovertyGainPerDay = 3f;
        public float HomelessPovertyGainPerDay = 5f;
        public float HomelessStressGainPerDay = 8f;
        public float HomelessAnxietyGainPerDay = 5f;
        public float HomelessComfortLossPerDay = 18f;
        public float HomelessHygieneLossPerDay = 8f;
        public float HomelessHealthLossPerDay = 2f;
        public float PovertyStressGainPerDayAtMaximum = 6f;
        public float PovertyAnxietyGainPerDayAtMaximum = 4f;
        public float PovertyHealthLossPerDayAtMaximum = 3f;
    }

    public sealed class EconomicPressureModel
    {
        public static void Advance(
            Sim sim,
            double elapsedSeconds,
            EconomicPressureSettings settings)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (elapsedSeconds <= 0d) return;

            float days = (float)(elapsedSeconds / 86400d);
            bool homeless = sim.Residence == null || sim.Residence.IsHomeless;
            bool unemployed = sim.Employment == null || !sim.Employment.IsEmployed;
            float povertyGainPerDay = 0f;

            if (unemployed)
                povertyGainPerDay += settings.UnemployedPovertyGainPerDay;
            if (homeless)
                povertyGainPerDay += settings.HomelessPovertyGainPerDay;

            float startingPoverty = sim.Stats.Get(SimStat.Poverty);
            float averagePoverty = AverageBoundedValue(
                startingPoverty, povertyGainPerDay, days, SimStats.DefaultMaximum);
            sim.Stats.Adjust(SimStat.Poverty, povertyGainPerDay * days);
            float povertyFraction = averagePoverty / SimStats.DefaultMaximum;
            sim.Stats.Adjust(SimStat.Stress, settings.PovertyStressGainPerDayAtMaximum * povertyFraction * days);
            sim.Stats.Adjust(SimStat.Anxiety, settings.PovertyAnxietyGainPerDayAtMaximum * povertyFraction * days);
            sim.Stats.Adjust(SimStat.Health, -settings.PovertyHealthLossPerDayAtMaximum * povertyFraction * days);

            if (!homeless) return;
            sim.Stats.Adjust(SimStat.Stress, settings.HomelessStressGainPerDay * days);
            sim.Stats.Adjust(SimStat.Anxiety, settings.HomelessAnxietyGainPerDay * days);
            sim.Stats.Adjust(SimStat.Comfort, -settings.HomelessComfortLossPerDay * days);
            sim.Stats.Adjust(SimStat.Hygiene, -settings.HomelessHygieneLossPerDay * days);
            sim.Stats.Adjust(SimStat.Health, -settings.HomelessHealthLossPerDay * days);
        }

        private static float AverageBoundedValue(float start, float ratePerDay, float days, float maximum)
        {
            if (days <= 0f || ratePerDay <= 0f || start >= maximum) return Math.Min(start, maximum);
            float timeToMaximum = (maximum - start) / ratePerDay;
            if (timeToMaximum >= days) return start + (ratePerDay * days * 0.5f);

            float risingArea = (start + maximum) * 0.5f * timeToMaximum;
            float cappedArea = maximum * (days - timeToMaximum);
            return (risingArea + cappedArea) / days;
        }
    }
}
