using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>Edits an <see cref="OccasionSchedule"/> with calendar-aware month and day pickers and a live preview.</summary>
    internal static class ScheduleGUI
    {
        private static readonly string[] ModeLabels = { "Yearly", "Explicit dates" };
        private static readonly string[] CalendarLabels = { "Gregorian", "Persian (Jalali)", "Hijri (lunar)" };
        private static readonly GUIContent[] CountdownLabels = { new GUIContent("Main start"), new GUIContent("March equinox (Nowruz moment)") };
        private static readonly Dictionary<CalendarType, string[]> MonthOptions = new Dictionary<CalendarType, string[]>();
        private static readonly Dictionary<int, string[]> DayOptions = new Dictionary<int, string[]>();

        public static void Draw(SerializedProperty schedule, Occasion occasion)
        {
            var mode = schedule.FindPropertyRelative(nameof(OccasionSchedule.mode));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Repeats");
                var next = GUILayout.Toolbar(mode.enumValueIndex, ModeLabels);

                if (next != mode.enumValueIndex)
                    mode.enumValueIndex = next;
            }

            if (mode.enumValueIndex == (int)ScheduleMode.Yearly)
                DrawYearly(schedule);
            else
                DrawExplicit(schedule);

            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Lead / tail days", "The window opens this many days before the main start and stays open this many days after the main end."));
                var lead = schedule.FindPropertyRelative(nameof(OccasionSchedule.leadDays));
                var tail = schedule.FindPropertyRelative(nameof(OccasionSchedule.tailDays));
                var oldWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 34;
                SetInt(lead, Mathf.Max(0, EditorGUILayout.IntField("Lead", lead.intValue, GUILayout.MinWidth(70), GUILayout.MaxWidth(120))));
                GUILayout.Space(8);
                SetInt(tail, Mathf.Max(0, EditorGUILayout.IntField("Tail", tail.intValue, GUILayout.MinWidth(70), GUILayout.MaxWidth(120))));
                EditorGUIUtility.labelWidth = oldWidth;
                GUILayout.FlexibleSpace();
            }

            var countdown = schedule.FindPropertyRelative(nameof(OccasionSchedule.countdown));
            var countdownIndex = EditorGUILayout.Popup(new GUIContent("Countdown to", "What OccasionInstance.CountdownTarget points at, e.g. for a \"Nowruz in 2h 14m\" banner."), countdown.enumValueIndex, CountdownLabels);

            if (countdownIndex != countdown.enumValueIndex)
                countdown.enumValueIndex = countdownIndex;

            DrawPreview(occasion);
        }

        private static void DrawYearly(SerializedProperty schedule)
        {
            var calendar = schedule.FindPropertyRelative(nameof(OccasionSchedule.calendar));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Calendar");
                var next = GUILayout.Toolbar(calendar.enumValueIndex, CalendarLabels);

                if (next != calendar.enumValueIndex)
                    calendar.enumValueIndex = next;
            }

            var type = (CalendarType)calendar.enumValueIndex;
            var startMonth = schedule.FindPropertyRelative(nameof(OccasionSchedule.startMonth));
            var startDay = schedule.FindPropertyRelative(nameof(OccasionSchedule.startDay));
            var endMonth = schedule.FindPropertyRelative(nameof(OccasionSchedule.endMonth));
            var endDay = schedule.FindPropertyRelative(nameof(OccasionSchedule.endDay));

            DateRow("Starts", type, startMonth, startDay, schedule.FindPropertyRelative(nameof(OccasionSchedule.startMinute)));
            DateRow("Ends (inclusive)", type, endMonth, endDay, schedule.FindPropertyRelative(nameof(OccasionSchedule.endMinute)));

            if (endMonth.intValue < startMonth.intValue || (endMonth.intValue == startMonth.intValue && endDay.intValue < startDay.intValue))
                EditorGUILayout.LabelField(" ", "Ends in the next year.", OKStyles.Muted);

            if (type == CalendarType.Hijri)
            {
                var offset = schedule.FindPropertyRelative(nameof(OccasionSchedule.hijriOffsetDays));
                SetInt(offset, EditorGUILayout.IntSlider(new GUIContent("Hijri offset (days)", "Arithmetic Hijri dates can be a day or two off the official calendar. Shift them here."), offset.intValue, -3, 3));
            }
        }

        private static void DateRow(string label, CalendarType type, SerializedProperty month, SerializedProperty day, SerializedProperty minute)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);

                var m = Mathf.Clamp(month.intValue, 1, 12);
                var newMonth = EditorGUILayout.Popup(m - 1, GetMonthOptions(type), GUILayout.MinWidth(90), GUILayout.MaxWidth(180)) + 1;
                var maxDay = CalendarUtility.GetMaxDaysInMonth(type, newMonth);
                var d = Mathf.Clamp(day.intValue, 1, maxDay);
                var newDay = EditorGUILayout.Popup(d - 1, GetDayOptions(maxDay), GUILayout.Width(46)) + 1;

                GUILayout.Label("at", GUILayout.Width(16));

                var text = OccasionSchedule.FormatTime(minute.intValue);
                var newText = EditorGUILayout.DelayedTextField(text, GUILayout.Width(48));

                if (newText != text && OccasionSchedule.TryParseTime(newText, out var minutes))
                    SetInt(minute, minutes);

                GUILayout.FlexibleSpace();
                SetInt(month, newMonth);
                SetInt(day, newDay);
            }
        }

        private static void DrawExplicit(SerializedProperty schedule)
        {
            var ranges = schedule.FindPropertyRelative(nameof(OccasionSchedule.ranges));
            EditorGUILayout.LabelField(" ", "Gregorian, device time, \"yyyy-MM-dd HH:mm\". End is exclusive.", OKStyles.Muted);

            for (var i = 0; i < ranges.arraySize; i++)
            {
                var range = ranges.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(i == 0 ? "Ranges" : " ");
                    DateField(range.FindPropertyRelative(nameof(DateRange.start)));
                    GUILayout.Label("→", GUILayout.Width(16));
                    DateField(range.FindPropertyRelative(nameof(DateRange.end)));

                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), EditorStyles.iconButton, GUILayout.Width(20)))
                    {
                        ranges.DeleteArrayElementAtIndex(i);
                        ranges.serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(ranges.arraySize == 0 ? "Ranges" : " ");

                if (GUILayout.Button(new GUIContent(ranges.arraySize == 0 ? "Add range" : "Add range (+1 year)", "A new range; copies the last one a year later."), GUILayout.Width(150)))
                    AddRange(ranges);

                GUILayout.FlexibleSpace();
            }
        }

        private static void AddRange(SerializedProperty ranges)
        {
            DateTime start, end;

            if (ranges.arraySize > 0)
            {
                var last = ranges.GetArrayElementAtIndex(ranges.arraySize - 1);
                var lastStart = Read(last.FindPropertyRelative(nameof(DateRange.start)));
                var lastEnd = Read(last.FindPropertyRelative(nameof(DateRange.end)));

                if (lastStart.TryToDateTime(out var s) && lastEnd.TryToDateTime(out var e))
                {
                    start = s.AddYears(1);
                    end = e.AddYears(1);
                }
                else
                {
                    start = DateTime.Today.AddDays(1);
                    end = start.AddDays(1);
                }
            }
            else
            {
                start = DateTime.Today.AddDays(1);
                end = start.AddDays(1);
            }

            ranges.arraySize++;
            var range = ranges.GetArrayElementAtIndex(ranges.arraySize - 1);
            Write(range.FindPropertyRelative(nameof(DateRange.start)), new LocalDateTime(start));
            Write(range.FindPropertyRelative(nameof(DateRange.end)), new LocalDateTime(end));
        }

        private static void DateField(SerializedProperty property)
        {
            var value = Read(property);
            var valid = value.TryToDateTime(out _);
            var text = valid ? value.ToString() : "";
            var previous = GUI.backgroundColor;

            if (!valid)
                GUI.backgroundColor = OKStyles.Error;

            var next = EditorGUILayout.DelayedTextField(text, GUILayout.MinWidth(110));
            GUI.backgroundColor = previous;

            if (next != text && LocalDateTime.TryParse(next, out var parsed))
                Write(property, parsed);
        }

        private static void DrawPreview(Occasion occasion)
        {
            if (occasion == null)
                return;

            // Only the current or next window.
            var now = DateTime.Now;
            var instance = ScheduleResolver.GetInstances(occasion, now, now.AddYears(3)).FirstOrDefault();

            GUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (instance == null)
                {
                    EditorGUILayout.LabelField("No upcoming dates.", EditorStyles.boldLabel);
                    return;
                }

                var live = instance.Contains(now);

                // One fixed-height row so the bold "when" and the plain range share a baseline.
                var row = GUILayoutUtility.GetRect(1, 16, GUILayout.ExpandWidth(true));
                var previous = GUI.contentColor;

                if (live)
                    GUI.contentColor = OKStyles.Active;

                // The range starts at the same x as the Detail() lines below it.
                GUI.Label(new Rect(row.x, row.y, 76, row.height), live ? "● Now" : "In " + DateFormat.Span(instance.WindowStart - now), EditorStyles.miniBoldLabel);
                GUI.contentColor = previous;
                GUI.Label(new Rect(row.x + 76, row.y, row.width - 76, row.height), DateFormat.Range(instance.MainStart, instance.MainEnd), EditorStyles.miniLabel);

                var schedule = occasion.schedule;

                if (schedule.mode == ScheduleMode.Yearly && schedule.calendar != CalendarType.Gregorian)
                    Detail(DateFormat.CalendarRange(schedule.calendar, instance.MainStart, instance.MainEnd));

                if (instance.WindowStart != instance.MainStart || instance.WindowEnd != instance.MainEnd)
                    Detail("Window " + DateFormat.Range(instance.WindowStart, instance.WindowEnd));

                if (schedule.countdown == CountdownTarget.MarchEquinox)
                    Detail("Equinox " + DateFormat.Short(instance.CountdownTarget));
            }
        }

        private static void Detail(string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(76);
                GUILayout.Label(text, OKStyles.Muted);
            }
        }

        private static string[] GetMonthOptions(CalendarType type)
        {
            if (!MonthOptions.TryGetValue(type, out var options))
            {
                options = CalendarUtility.GetMonthNames(type).Select((name, i) => $"{i + 1}. {name}").ToArray();
                MonthOptions[type] = options;
            }

            return options;
        }

        private static string[] GetDayOptions(int days)
        {
            if (!DayOptions.TryGetValue(days, out var options))
            {
                options = Enumerable.Range(1, days).Select(d => d.ToString()).ToArray();
                DayOptions[days] = options;
            }

            return options;
        }

        private static void SetInt(SerializedProperty property, int value)
        {
            if (property.intValue != value)
                property.intValue = value;
        }

        private static LocalDateTime Read(SerializedProperty property) => new LocalDateTime
        {
            year = property.FindPropertyRelative(nameof(LocalDateTime.year)).intValue,
            month = property.FindPropertyRelative(nameof(LocalDateTime.month)).intValue,
            day = property.FindPropertyRelative(nameof(LocalDateTime.day)).intValue,
            hour = property.FindPropertyRelative(nameof(LocalDateTime.hour)).intValue,
            minute = property.FindPropertyRelative(nameof(LocalDateTime.minute)).intValue,
        };

        private static void Write(SerializedProperty property, LocalDateTime value)
        {
            property.FindPropertyRelative(nameof(LocalDateTime.year)).intValue = value.year;
            property.FindPropertyRelative(nameof(LocalDateTime.month)).intValue = value.month;
            property.FindPropertyRelative(nameof(LocalDateTime.day)).intValue = value.day;
            property.FindPropertyRelative(nameof(LocalDateTime.hour)).intValue = value.hour;
            property.FindPropertyRelative(nameof(LocalDateTime.minute)).intValue = value.minute;
        }
    }
}
