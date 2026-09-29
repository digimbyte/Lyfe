using System;
using System.Collections.Generic;

namespace Syms.Simulation.Jobs
{
    [Serializable]
    public sealed class WorkSchedule
    {
        public List<WorkShift> Shifts = new List<WorkShift>();

        public bool IsWorking(DayOfWeek day, int minuteOfDay)
        {
            for (int i = 0; i < Shifts.Count; i++)
            {
                if (Shifts[i] != null && Shifts[i].Contains(day, minuteOfDay))
                    return true;
            }

            return false;
        }
    }
}

