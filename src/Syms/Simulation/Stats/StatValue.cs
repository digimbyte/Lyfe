using System;

namespace Syms.Simulation.Stats
{
    [Serializable]
    public sealed class StatValue
    {
        public SimStat Stat;
        public float Value;

        public StatValue(SimStat stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }
}

