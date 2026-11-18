using System;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>Common occasions to start from. Lunar dates are arithmetic; check them against the official calendar.</summary>
    internal static class OccasionPresets
    {
        public readonly struct Preset
        {
            public readonly string Name;
            public readonly Func<Occasion> Create;

            public Preset(string name, Func<Occasion> create)
            {
                Name = name;
                Create = create;
            }
        }

        private const string LunarNote = "Arithmetic Hijri date. Each year, check the official date and adjust the Hijri offset, or switch to explicit dates.";

        public static readonly Preset[] All =
        {
            new Preset("Persian/Nowruz", () =>
            {
                var o = Yearly("nowruz", "Nowruz", CalendarType.Persian, 1, 1, 1, 12, new Color(0.33f, 0.78f, 0.42f), 10);
                o.schedule.leadDays = 7;
                o.schedule.countdown = CountdownTarget.MarchEquinox;
                o.greeting.enabled = true;
                o.greeting.title = "Happy Nowruz!";
                o.notification.title = "Happy Nowruz!";
                o.notification.anchor = NotificationAnchor.Countdown;
                o.notes = "Lead days count down to the equinox (Sal Tahvil).";
                return o;
            }),
            new Preset("Persian/Sizdah Bedar", () => Yearly("sizdah_bedar", "Sizdah Bedar", CalendarType.Persian, 1, 13, 1, 13, new Color(0.55f, 0.86f, 0.35f), 11)),
            new Preset("Persian/Chaharshanbe Suri", () =>
            {
                var o = Explicit("chaharshanbe_suri", "Chaharshanbe Suri", new Color(0.98f, 0.45f, 0.18f), 12);
                o.notes = "The last Tuesday evening before Nowruz, so it moves every year. Add each year's range.";
                return o;
            }),
            new Preset("Persian/Yalda", () =>
            {
                var o = Yearly("yalda", "Yalda Night", CalendarType.Persian, 9, 30, 9, 30, new Color(0.78f, 0.16f, 0.27f), 10);
                o.schedule.leadDays = 3;
                return o;
            }),
            new Preset("Islamic/Ramadan", () =>
            {
                var o = Yearly("ramadan", "Ramadan", CalendarType.Hijri, 9, 1, 9, 30, new Color(0.2f, 0.58f, 0.68f), 20);
                o.notes = LunarNote;
                return o;
            }),
            new Preset("Islamic/Eid al-Fitr", () =>
            {
                var o = Yearly("eid_al_fitr", "Eid al-Fitr", CalendarType.Hijri, 10, 1, 10, 2, new Color(0.95f, 0.75f, 0.2f), 21);
                o.notes = LunarNote;
                return o;
            }),
            new Preset("Islamic/Tasua and Ashura (exclusive)", () =>
            {
                var o = Yearly("tasua_ashura", "Tasua & Ashura", CalendarType.Hijri, 1, 9, 1, 10, new Color(0.26f, 0.26f, 0.3f), 100);
                o.exclusive = true;
                o.notes = "Exclusive: hides festive icons and themes while active. " + LunarNote;
                return o;
            }),
            new Preset("Global/Halloween", () =>
            {
                var o = Yearly("halloween", "Halloween", CalendarType.Gregorian, 10, 31, 10, 31, new Color(1f, 0.55f, 0.1f), 10);
                o.schedule.leadDays = 7;
                return o;
            }),
            new Preset("Global/Christmas and New Year", () => Yearly("christmas_new_year", "Christmas & New Year", CalendarType.Gregorian, 12, 20, 1, 2, new Color(0.85f, 0.24f, 0.24f), 10)),
            new Preset("Global/Valentine's Day", () => Yearly("valentines", "Valentine's Day", CalendarType.Gregorian, 2, 14, 2, 14, new Color(0.95f, 0.4f, 0.6f), 10)),
        };

        public static Occasion Blank(string id) => Yearly(id, "New occasion", CalendarType.Gregorian, 1, 1, 1, 1, new Color(0.36f, 0.6f, 0.98f), 0);

        private static Occasion Yearly(string id, string name, CalendarType calendar, int startMonth, int startDay, int endMonth, int endDay, Color color, int priority)
        {
            return new Occasion
            {
                id = id,
                displayName = name,
                color = color,
                priority = priority,
                schedule = new OccasionSchedule
                {
                    mode = ScheduleMode.Yearly,
                    calendar = calendar,
                    startMonth = startMonth,
                    startDay = startDay,
                    endMonth = endMonth,
                    endDay = endDay,
                },
            };
        }

        private static Occasion Explicit(string id, string name, Color color, int priority)
        {
            var occasion = Yearly(id, name, CalendarType.Gregorian, 1, 1, 1, 1, color, priority);
            occasion.schedule.mode = ScheduleMode.Explicit;
            return occasion;
        }
    }
}
