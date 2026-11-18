using System;
using System.Collections.Generic;

namespace OccasionKit
{
    /// <summary>Turns an occasion's schedule into concrete occurrences.</summary>
    public static class ScheduleResolver
    {
        private static readonly Comparison<OccasionInstance> ByWindowStart = (a, b) => a.WindowStart.CompareTo(b.WindowStart);

        /// <summary>Adds the occurrences whose window overlaps [<paramref name="from"/>, <paramref name="to"/>), sorted by window start.</summary>
        public static void GetInstances(Occasion occasion, DateTime from, DateTime to, List<OccasionInstance> results)
        {
            if (occasion?.schedule == null || to <= from)
                return;

            var schedule = occasion.schedule;
            var before = results.Count;

            if (schedule.mode == ScheduleMode.Explicit)
            {
                if (schedule.ranges == null)
                    return;

                foreach (var range in schedule.ranges)
                {
                    if (range == null || !range.start.TryToDateTime(out var start) || !range.end.TryToDateTime(out var end) || end <= start)
                        continue;

                    AddIfOverlaps(occasion, start, end, from, to, results);
                }
            }
            else
            {
                // A window can start in the previous calendar year (lead days, wrap) and reach into the next one.
                var firstYear = CalendarUtility.GetYear(schedule.calendar, from) - 1;
                var lastYear = CalendarUtility.GetYear(schedule.calendar, to) + 1;

                for (var year = firstYear; year <= lastYear; year++)
                {
                    if (TryGetYearly(schedule, year, out var start, out var end))
                        AddIfOverlaps(occasion, start, end, from, to, results);
                }
            }

            if (results.Count - before > 1)
                results.Sort(before, results.Count - before, Comparer<OccasionInstance>.Create(ByWindowStart));
        }

        public static List<OccasionInstance> GetInstances(Occasion occasion, DateTime from, DateTime to)
        {
            var results = new List<OccasionInstance>();
            GetInstances(occasion, from, to, results);
            return results;
        }

        /// <summary>The occurrence whose window contains <paramref name="time"/>, or null.</summary>
        public static OccasionInstance GetInstanceAt(Occasion occasion, DateTime time)
        {
            var results = new List<OccasionInstance>(2);
            GetInstances(occasion, time, time.AddTicks(1), results);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>The first occurrence whose window starts after <paramref name="after"/>, or null.</summary>
        public static OccasionInstance GetNextInstance(Occasion occasion, DateTime after, int searchDays = 800)
        {
            var results = new List<OccasionInstance>();
            GetInstances(occasion, after, after.AddDays(searchDays), results);

            foreach (var instance in results)
            {
                if (instance.WindowStart > after)
                    return instance;
            }

            return null;
        }

        /// <summary>The main window of a yearly schedule for one year of its calendar.</summary>
        public static bool TryGetYearly(OccasionSchedule schedule, int year, out DateTime mainStart, out DateTime mainEnd)
        {
            mainStart = mainEnd = default;

            if (!CalendarUtility.TryToDateTime(schedule.calendar, year, schedule.startMonth, schedule.startDay, out var startDate))
                return false;

            var wraps = schedule.endMonth < schedule.startMonth || (schedule.endMonth == schedule.startMonth && schedule.endDay < schedule.startDay);

            if (!CalendarUtility.TryToDateTime(schedule.calendar, wraps ? year + 1 : year, schedule.endMonth, schedule.endDay, out var endDate))
                return false;

            var offset = schedule.calendar == CalendarType.Hijri ? schedule.hijriOffsetDays : 0;
            mainStart = startDate.AddDays(offset).AddMinutes(ClampMinutes(schedule.startMinute));
            mainEnd = endDate.AddDays(offset).AddMinutes(ClampMinutes(schedule.endMinute));
            return mainEnd > mainStart;
        }

        public static OccasionInstance CreateInstance(Occasion occasion, DateTime mainStart, DateTime mainEnd)
        {
            var schedule = occasion.schedule;
            var windowStart = mainStart.AddDays(-Math.Max(0, schedule.leadDays));
            var windowEnd = mainEnd.AddDays(Math.Max(0, schedule.tailDays));
            var countdown = schedule.countdown == CountdownTarget.MarchEquinox ? Equinox.NearestMarchLocal(mainStart) : mainStart;
            return new OccasionInstance(occasion, windowStart, mainStart, mainEnd, windowEnd, countdown);
        }

        private static void AddIfOverlaps(Occasion occasion, DateTime mainStart, DateTime mainEnd, DateTime from, DateTime to, List<OccasionInstance> results)
        {
            var instance = CreateInstance(occasion, mainStart, mainEnd);

            if (instance.WindowEnd > from && instance.WindowStart < to)
                results.Add(instance);
        }

        private static int ClampMinutes(int minutes) => Math.Max(0, Math.Min(OccasionSchedule.MinutesPerDay, minutes));
    }
}
