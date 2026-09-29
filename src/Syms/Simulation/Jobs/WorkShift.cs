using System;

namespace Syms.Simulation.Jobs
{
    [Serializable]
    public sealed class WorkShift
    {
        public DayOfWeek Day;
        public int StartMinute;
        public int DurationMinutes;

        public WorkShift(DayOfWeek day, int startMinute, int durationMinutes)
        {
            if (startMinute < 0 || startMinute >= 24 * 60)
                throw new ArgumentOutOfRangeException(nameof(startMinute));
            if (durationMinutes <= 0 || durationMinutes > 24 * 60)
                throw new ArgumentOutOfRangeException(nameof(durationMinutes));

            Day = day;
            StartMinute = startMinute;
            DurationMinutes = durationMinutes;
        }

        public bool Contains(DayOfWeek day, int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= 24 * 60)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));

            int weekMinutes = 7 * 24 * 60;
            int shiftStart = ((int)Day * 24 * 60) + StartMinute;
            int query = ((int)day * 24 * 60) + minuteOfDay;
            int elapsed = (query - shiftStart + weekMinutes) % weekMinutes;
            return elapsed < DurationMinutes;
        }
    }
}

