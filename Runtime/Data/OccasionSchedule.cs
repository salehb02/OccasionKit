using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OccasionKit
{
    public enum CalendarType
    {
        Gregorian = 0,
        /// <summary>Solar Hijri (Jalali). Nowruz, Yalda, Sizdah Bedar.</summary>
        Persian = 1,
        /// <summary>Arithmetic lunar Hijri. Can be a day or two off the official, sighting-based calendar; use the offset or explicit dates.</summary>
        Hijri = 2,
    }

    public enum ScheduleMode
    {
        /// <summary>Every year on the same month/day of the chosen calendar.</summary>
        Yearly = 0,
        /// <summary>Only on the listed ranges (Gregorian, device time). For announced lunar dates and one-off events.</summary>
        Explicit = 1,
    }

    public enum CountdownTarget
    {
        /// <summary>Counts down to the start of the main window (lead days excluded).</summary>
        MainStart = 0,
        /// <summary>Counts down to the March equinox closest to the main start (the exact Nowruz moment).</summary>
        MarchEquinox = 1,
    }

    /// <summary>A Gregorian wall-clock date and time on the device. Stored as plain numbers so it reads and diffs well.</summary>
    [Serializable]
    public struct LocalDateTime
    {
        public int year;
        public int month;
        public int day;
        public int hour;
        public int minute;

        private static readonly Regex Pattern = new Regex(@"^\s*(\d{4})-(\d{1,2})-(\d{1,2})(?:[ T]+(\d{1,2}):(\d{2}))?\s*$");

        public LocalDateTime(DateTime time)
        {
            year = time.Year;
            month = time.Month;
            day = time.Day;
            hour = time.Hour;
            minute = time.Minute;
        }

        public bool TryToDateTime(out DateTime result)
        {
            result = default;

            if (year < 1 || year > 9998 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
                return false;

            if (hour < 0 || hour > 24 || minute < 0 || minute > 59 || (hour == 24 && minute != 0))
                return false;

            result = new DateTime(year, month, day).AddMinutes(hour * 60 + minute);
            return true;
        }

        /// <summary>Parses "yyyy-MM-dd HH:mm" or "yyyy-MM-dd". "24:00" is accepted as the end of the day.</summary>
        public static bool TryParse(string text, out LocalDateTime value)
        {
            value = default;
            var match = Pattern.Match(text ?? string.Empty);

            if (!match.Success)
                return false;

            value.year = int.Parse(match.Groups[1].Value);
            value.month = int.Parse(match.Groups[2].Value);
            value.day = int.Parse(match.Groups[3].Value);

            if (match.Groups[4].Success)
            {
                value.hour = int.Parse(match.Groups[4].Value);
                value.minute = int.Parse(match.Groups[5].Value);
            }

            return value.TryToDateTime(out _);
        }

        public override string ToString() => $"{year:0000}-{month:00}-{day:00} {hour:00}:{minute:00}";
    }

    [Serializable]
    public class DateRange
    {
        public LocalDateTime start;
        public LocalDateTime end;
    }

    [Serializable]
    public class OccasionSchedule
    {
        public const int MinutesPerDay = 1440;

        public ScheduleMode mode = ScheduleMode.Yearly;
        public CalendarType calendar = CalendarType.Gregorian;

        // Yearly. Dates are inclusive days of the chosen calendar; an end before the start wraps into the next year.
        public int startMonth = 1;
        public int startDay = 1;
        /// <summary>Minutes after midnight on the start day.</summary>
        public int startMinute;
        public int endMonth = 1;
        public int endDay = 1;
        /// <summary>Minutes after midnight on the end day. 1440 means the end of that day.</summary>
        public int endMinute = MinutesPerDay;
        /// <summary>Shifts Hijri dates to match the official calendar of the year.</summary>
        public int hijriOffsetDays;

        // Explicit.
        public List<DateRange> ranges = new List<DateRange>();

        /// <summary>Days the window opens before the main start (teasers, "coming soon").</summary>
        public int leadDays;
        /// <summary>Days the window stays open after the main end.</summary>
        public int tailDays;
        public CountdownTarget countdown = CountdownTarget.MainStart;

        public static string FormatTime(int minutes)
        {
            minutes = Math.Max(0, Math.Min(MinutesPerDay, minutes));
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        /// <summary>Parses "HH:mm" (or "H"), 00:00 to 24:00.</summary>
        public static bool TryParseTime(string text, out int minutes)
        {
            minutes = 0;
            var parts = (text ?? string.Empty).Trim().Split(':');

            if (parts.Length < 1 || parts.Length > 2 || !int.TryParse(parts[0], out var h))
                return false;

            var m = 0;

            if (parts.Length == 2 && !int.TryParse(parts[1], out m))
                return false;

            if (h < 0 || h > 24 || m < 0 || m > 59 || (h == 24 && m != 0))
                return false;

            minutes = h * 60 + m;
            return true;
        }
    }
}
