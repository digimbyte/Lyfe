using System;

namespace Syms.Simulation.People
{
    public enum Gender
    {
        Unspecified,
        Woman,
        Man,
        NonBinary,
        Custom
    }

    public enum LifeStage
    {
        Infant,
        Child,
        Adolescent,
        Adult,
        Elder
    }

    [Serializable]
    public sealed class LifeStageThresholds
    {
        public int ChildAge = 5;
        public int AdolescentAge = 13;
        public int AdultAge = 18;
        public int ElderAge = 65;

        public LifeStage GetStage(int ageYears)
        {
            if (ageYears < 0) throw new ArgumentOutOfRangeException(nameof(ageYears));
            if (ageYears < ChildAge) return LifeStage.Infant;
            if (ageYears < AdolescentAge) return LifeStage.Child;
            if (ageYears < AdultAge) return LifeStage.Adolescent;
            if (ageYears < ElderAge) return LifeStage.Adult;
            return LifeStage.Elder;
        }
    }

    [Serializable]
    public sealed class Demographics
    {
        public int AgeYears;
        public double AgeProgress;
        public Gender Gender = Gender.Unspecified;
        public string CustomGenderId;

        public LifeStage GetLifeStage(LifeStageThresholds thresholds)
        {
            if (thresholds == null) throw new ArgumentNullException(nameof(thresholds));
            return thresholds.GetStage(AgeYears);
        }

        public void SetAge(int years, double progress)
        {
            if (years < 0) throw new ArgumentOutOfRangeException(nameof(years));
            if (double.IsNaN(progress) || double.IsInfinity(progress))
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (progress < 0d) throw new ArgumentOutOfRangeException(nameof(progress));
            int completedYears = (int)Math.Floor(progress);
            AgeYears = years + completedYears;
            AgeProgress = progress - completedYears;
        }
    }
}
