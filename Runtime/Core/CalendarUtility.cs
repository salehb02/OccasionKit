using System;
using System.Collections.Generic;
using System.Globalization;

namespace OccasionKit
{
    public static class CalendarUtility
    {
        private static readonly Calendar Gregorian = new GregorianCalendar();
        private static readonly Calendar Persian = new PersianCalendar();
        private static readonly Calendar Hijri = new HijriCalendar();

        private static readonly string[] GregorianMonths =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };

        private static readonly string[] PersianMonths =
        {
            "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar",
            "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand",
        };

        private static readonly string[] HijriMonths =
        {
            "Muharram", "Safar", "Rabi al-Awwal", "Rabi al-Thani", "Jumada al-Awwal", "Jumada al-Thani",
            "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah",
        };

        public static Calendar Get(CalendarType type)
        {
            switch (type)
            {
                case CalendarType.Persian: return Persian;
                case CalendarType.Hijri: return Hijri;
                default: return Gregorian;
            }
        }

        public static IReadOnlyList<string> GetMonthNames(CalendarType type)
        {
            switch (type)
            {
                case CalendarType.Persian: return PersianMonths;
                case CalendarType.Hijri: return HijriMonths;
                default: return GregorianMonths;
            }
        }

        public static string GetMonthName(CalendarType type, int month)
        {
            var names = GetMonthNames(type);
            return month >= 1 && month <= names.Count ? names[month - 1] : $"Month {month}";
        }

        /// <summary>The most days a month can have in any year (Esfand 30, February 29, Hijri months 30).</summary>
        public static int GetMaxDaysInMonth(CalendarType type, int month)
        {
            month = Math.Max(1, Math.Min(12, month));

            switch (type)
            {
                case CalendarType.Persian: return month <= 6 ? 31 : 30;
                case CalendarType.Hijri: return 30;
                default: return month == 2 ? 29 : DateTime.DaysInMonth(2001, month);
            }
        }

        public static int GetYear(CalendarType type, DateTime date)
        {
            var calendar = Get(type);
            return calendar.GetYear(Clamp(calendar, date));
        }

        public static int GetMonth(CalendarType type, DateTime date)
        {
            var calendar = Get(type);
            return calendar.GetMonth(Clamp(calendar, date));
        }

        public static int GetDay(CalendarType type, DateTime date)
        {
            var calendar = Get(type);
            return calendar.GetDayOfMonth(Clamp(calendar, date));
        }

        /// <summary>Midnight of a date in <paramref name="type"/>. A day past the month's end is clamped (Esfand 30 in a common year → Esfand 29).</summary>
        public static bool TryToDateTime(CalendarType type, int year, int month, int day, out DateTime result)
        {
            result = default;

            if (month < 1 || month > 12 || day < 1)
                return false;

            var calendar = Get(type);

            try
            {
                var days = calendar.GetDaysInMonth(year, month);
                result = calendar.ToDateTime(year, month, Math.Min(day, days), 0, 0, 0, 0);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>"1 Farvardin 1406", "31 October 2026".</summary>
        public static string FormatDate(CalendarType type, DateTime date)
        {
            var calendar = Get(type);
            date = Clamp(calendar, date);
            return $"{calendar.GetDayOfMonth(date)} {GetMonthName(type, calendar.GetMonth(date))} {calendar.GetYear(date)}";
        }

        private static DateTime Clamp(Calendar calendar, DateTime date)
        {
            if (date < calendar.MinSupportedDateTime)
                return calendar.MinSupportedDateTime;

            return date > calendar.MaxSupportedDateTime ? calendar.MaxSupportedDateTime : date;
        }
    }
}
