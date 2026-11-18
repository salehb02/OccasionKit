using System;
using System.Globalization;

namespace OccasionKit.EditorTools
{
    internal static class DateFormat
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Short(DateTime time) => time.ToString("ddd d MMM yyyy HH:mm", Invariant);

        public static string Day(DateTime time) => time.ToString("d MMM yyyy", Invariant);

        /// <summary>An exclusive end at midnight reads as 24:00 of the day before, the way it was entered.</summary>
        public static string End(DateTime time) =>
            time.TimeOfDay == TimeSpan.Zero ? time.AddDays(-1).ToString("ddd d MMM yyyy", Invariant) + " 24:00" : Short(time);

        public static string Range(DateTime start, DateTime end) => $"{Short(start)}  →  {End(end)}";

        /// <summary>"1 Farvardin 1406 → 13 Farvardin 1406" using the inclusive last day.</summary>
        public static string CalendarRange(CalendarType calendar, DateTime start, DateTime end)
        {
            var lastDay = end.TimeOfDay == TimeSpan.Zero ? end.AddDays(-1) : end;
            return $"{CalendarUtility.FormatDate(calendar, start)}  →  {CalendarUtility.FormatDate(calendar, lastDay)}";
        }

        /// <summary>"115 days", "2d 4h", "5h 20m", "12m": hours only matter within a week.</summary>
        public static string Span(TimeSpan span)
        {
            if (span <= TimeSpan.Zero)
                return "now";

            var days = (int)span.TotalDays;

            if (days >= 7 || (days >= 1 && span.Hours == 0))
                return days == 1 ? "1 day" : $"{days} days";

            if (days >= 1)
                return $"{days}d {span.Hours}h";

            var hours = (int)span.TotalHours;

            if (hours >= 1)
                return span.Minutes == 0 ? $"{hours}h" : $"{hours}h {span.Minutes}m";

            return $"{Math.Max(1, (int)span.TotalMinutes)}m";
        }
    }
}
