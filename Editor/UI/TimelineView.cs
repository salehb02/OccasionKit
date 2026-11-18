using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>
    /// A year of occasions as bars: lead/tail days faded, main window solid, exclusive ones striped.
    /// The year and its month columns follow a Gregorian, Persian or Hijri calendar.
    /// </summary>
    internal sealed class TimelineView
    {
        public const float HeaderHeight = 32f;
        public const float RowHeight = 20f;
        public const float LabelWidth = 160f;

        private static readonly string[] GregorianShort = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        private static readonly string[] PersianShort = { "Far", "Ord", "Kho", "Tir", "Mor", "Sha", "Meh", "Aba", "Aza", "Dey", "Bah", "Esf" };
        private static readonly string[] HijriShort = { "Muh", "Saf", "Rab I", "Rab II", "Jum I", "Jum II", "Raj", "Sha", "Ram", "Shaw", "Dhu Q", "Dhu H" };
        private static readonly int ScrubHash = "OccasionKit.TimelineScrub".GetHashCode();
        private static GUIStyle _pill;

        private readonly List<OccasionInstance> _instances = new List<OccasionInstance>();
        private readonly struct Month
        {
            public readonly DateTime Start;
            public readonly DateTime End;
            public readonly int Number;

            public Month(DateTime start, DateTime end, int number)
            {
                Start = start;
                End = end;
                Number = number;
            }
        }

        // The months of the shown year, and the Gregorian months under a Persian or Hijri year.
        private readonly List<Month> _months = new List<Month>();
        private readonly List<Month> _reference = new List<Month>();
        private CalendarType _monthsCalendar;
        private int _monthsYear;

        private Rect _bars;
        private CalendarType _calendar;
        private DateTime _start;
        private double _totalDays;

        /// <summary>With scrubbing on: the day clicked or dragged to during the last <see cref="Draw"/>, otherwise null.</summary>
        public DateTime? Scrubbed { get; private set; }

        public static float GetHeight(int rows) => HeaderHeight + Mathf.Max(rows, 1) * RowHeight + 4f;

        /// <summary>Top of a row in timeline coordinates, for scrolling it into view.</summary>
        public static float GetRowTop(int row) => HeaderHeight + row * RowHeight;

        private static GUIStyle Pill { get => _pill ?? (_pill = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 0, 0) }); }

        /// <summary>First day of <paramref name="year"/> in <paramref name="calendar"/>, and of the year after.</summary>
        public static bool TryGetYear(CalendarType calendar, int year, out DateTime start, out DateTime end)
        {
            end = default;
            return CalendarUtility.TryToDateTime(calendar, year, 1, 1, out start) && CalendarUtility.TryToDateTime(calendar, year + 1, 1, 1, out end);
        }

        /// <param name="calendar">The calendar the year and the month columns are in.</param>
        /// <param name="year">A year in <paramref name="calendar"/>.</param>
        /// <param name="scrollY">How far an enclosing scroll view has scrolled, so the month header stays on top.</param>
        /// <param name="scrub">Click or drag over the bars to pick a day (<see cref="Scrubbed"/>). Only clicks on a name select its row.</param>
        /// <returns>The row clicked this event, or -1.</returns>
        public int Draw(Rect rect, CalendarType calendar, int year, IReadOnlyList<Occasion> occasions, int selected, DateTime? marker, float scrollY = 0f, bool scrub = false)
        {
            var e = Event.current;
            var repaint = e.type == EventType.Repaint;

            if (!TryGetYear(calendar, year, out _start, out var end))
            {
                calendar = CalendarType.Gregorian;
                year = DateTime.Now.Year;
                TryGetYear(calendar, year, out _start, out end);
            }

            _calendar = calendar;
            _totalDays = (end - _start).TotalDays;
            _bars = new Rect(rect.x + LabelWidth, rect.y, rect.width - LabelWidth, rect.height);
            Scrubbed = null;

            var headerTop = Mathf.Clamp(rect.y + scrollY, rect.y, Mathf.Max(rect.y, rect.yMax - HeaderHeight));
            var bodyTop = headerTop + HeaderHeight;
            var mouseInBody = e.mousePosition.y >= bodyTop && rect.Contains(e.mousePosition);

            CollectMonths(year, end);

            // Scrubbing takes the mouse before rows do, so a drag can start on a bar.
            var scrubId = scrub ? GUIUtility.GetControlID(ScrubHash, FocusType.Passive) : 0;
            var scrubArea = new Rect(_bars.x, headerTop, _bars.width, rect.yMax - headerTop);

            if (scrub)
                HandleScrub(scrubId, scrubArea, e, end);

            var scrubbing = scrub && GUIUtility.hotControl == scrubId;

            if (repaint)
            {
                EditorGUI.DrawRect(rect, OKStyles.TimelineBackground);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, LabelWidth, rect.height), OKStyles.LabelColumn);
                DrawColumns(rect.y, rect.height, 0f);
            }

            var clicked = -1;
            OccasionInstance hovered = null;

            for (var i = 0; i < occasions.Count; i++)
            {
                var occasion = occasions[i];

                if (occasion == null)
                    continue;

                var row = new Rect(rect.x, rect.y + GetRowTop(i), rect.width, RowHeight);
                var rowHovered = mouseInBody && row.Contains(e.mousePosition);
                var label = new Rect(row.x, row.y, LabelWidth, row.height);

                if (repaint)
                {
                    if (i == selected)
                        EditorGUI.DrawRect(row, OKStyles.Selection);
                    else if (rowHovered && !scrubbing)
                        EditorGUI.DrawRect(scrub ? label : row, OKStyles.Hover);
                }

                if (scrub)
                    EditorGUIUtility.AddCursorRect(label, MouseCursor.Link);

                var color = occasion.enabled ? occasion.color : OKStyles.Disabled;
                OKStyles.ColorChip(new Rect(row.x + 7, row.y + 6, 8, 8), color);

                var previous = GUI.contentColor;

                if (!occasion.enabled)
                    GUI.contentColor = new Color(1, 1, 1, 0.5f);

                GUI.Label(new Rect(row.x + 20, row.y + 1, LabelWidth - 24, RowHeight - 2), occasion.Title, OKStyles.RowLabel);
                GUI.contentColor = previous;

                _instances.Clear();
                ScheduleResolver.GetInstances(occasion, _start, end, _instances);

                var lowContrast = Mathf.Abs(OKStyles.Luminance(color) - OKStyles.Luminance(OKStyles.TimelineBackground)) < 0.2f;

                foreach (var instance in _instances)
                {
                    var barTop = row.y + 4;
                    var barHeight = RowHeight - 8;
                    var faded = new Color(color.r, color.g, color.b, lowContrast ? 0.6f : 0.35f);

                    var lead = Rect.MinMaxRect(X(instance.WindowStart), barTop, X(instance.MainStart), barTop + barHeight);
                    var main = Rect.MinMaxRect(X(instance.MainStart), barTop, X(instance.MainEnd), barTop + barHeight);
                    var tail = Rect.MinMaxRect(X(instance.MainEnd), barTop, X(instance.WindowEnd), barTop + barHeight);

                    // Keep one-day occasions visible at any window width.
                    if (main.width < 3 && instance.MainEnd > _start && instance.MainStart < end)
                    {
                        main.xMin = Mathf.Min(main.xMin, _bars.xMax - 3);
                        main.width = 3;
                    }

                    if (repaint)
                    {
                        if (lead.width > 0)
                            EditorGUI.DrawRect(lead, faded);

                        if (tail.width > 0)
                            EditorGUI.DrawRect(tail, faded);

                        if (main.width > 0)
                        {
                            EditorGUI.DrawRect(main, color);

                            if (occasion.exclusive)
                                GUI.DrawTextureWithTexCoords(main, OKStyles.Stripes, new Rect(0, 0, main.width / 8f, main.height / 8f));

                            if (lowContrast)
                                OKStyles.Outline(main, OKStyles.BarOutline);

                            var text = new GUIContent(occasion.Title);

                            if (OKStyles.BarLabel.CalcSize(text).x < main.width)
                            {
                                var textColor = OKStyles.BarLabel.normal.textColor;
                                OKStyles.BarLabel.normal.textColor = OKStyles.ContrastText(color);
                                OKStyles.BarLabel.Draw(main, text, false, false, false, false);
                                OKStyles.BarLabel.normal.textColor = textColor;
                            }
                        }
                    }

                    var whole = Rect.MinMaxRect(Mathf.Min(lead.xMin, main.xMin) - 2, barTop - 2, Mathf.Max(tail.xMax, main.xMax) + 2, barTop + barHeight + 2);

                    if (mouseInBody && whole.Contains(e.mousePosition))
                        hovered = instance;
                }

                if (e.type == EventType.MouseDown && e.button == 0 && rowHovered && (!scrub || label.Contains(e.mousePosition)))
                {
                    clicked = i;
                    e.Use();
                }
            }

            if (repaint)
            {
                DrawHeader(rect, headerTop, year, end);

                var lineTop = bodyTop - 4;
                var now = DateTime.Now;

                if (now >= _start && now < end)
                    EditorGUI.DrawRect(new Rect(X(now) - 1, lineTop, 2, rect.yMax - lineTop), OKStyles.Today);

                if (marker.HasValue && marker.Value >= _start && marker.Value < end)
                {
                    var x = X(marker.Value);
                    EditorGUI.DrawRect(new Rect(x - 1, lineTop, 2, rect.yMax - lineTop), OKStyles.Simulated);

                    if (scrub)
                        DrawPill(x, headerTop + 16, DayLabel(marker.Value, false), OKStyles.Simulated);
                }

                if (scrub && !scrubbing && scrubArea.Contains(e.mousePosition))
                {
                    var day = DayAt(e.mousePosition.x, end);
                    var x = X(day.AddHours(12));
                    EditorGUI.DrawRect(new Rect(x, lineTop, 1, rect.yMax - lineTop), new Color(OKStyles.Simulated.r, OKStyles.Simulated.g, OKStyles.Simulated.b, 0.45f));
                    DrawPill(x, headerTop + 1, DayLabel(day, true), OKStyles.LabelColumn);
                }

                if (hovered != null && !scrubbing)
                    DrawTooltip(rect, e.mousePosition, hovered);
            }

            return clicked;
        }

        private float X(DateTime time)
        {
            var days = (time - _start).TotalDays;
            return _bars.x + (float)(Math.Max(0, Math.Min(_totalDays, days)) / _totalDays) * _bars.width;
        }

        private DateTime DayAt(float x, DateTime end)
        {
            var t = Mathf.Clamp01((x - _bars.x) / _bars.width);
            var day = _start.AddDays(Math.Floor(t * _totalDays));
            return day >= end ? end.AddDays(-1) : day;
        }

        private void HandleScrub(int id, Rect area, Event e, DateTime end)
        {
            EditorGUIUtility.AddCursorRect(area, MouseCursor.SlideArrow, id);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && area.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        Scrubbed = DayAt(e.mousePosition.x, end);
                        e.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        Scrubbed = DayAt(e.mousePosition.x, end);
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;
            }
        }

        /// <summary>Month shading and grid lines between <paramref name="top"/> and top + height; Gregorian month lines start <paramref name="referenceInset"/> lower.</summary>
        private void DrawColumns(float top, float height, float referenceInset)
        {
            for (var i = 0; i < _months.Count; i++)
            {
                var x0 = X(_months[i].Start);
                var x1 = X(_months[i].End);

                if (i % 2 == 1)
                    EditorGUI.DrawRect(new Rect(x0, top, x1 - x0, height), OKStyles.MonthShade);

                EditorGUI.DrawRect(new Rect(x0, top, 1, height), OKStyles.GridLine);
            }

            foreach (var month in _reference)
            {
                if (month.Start > _start)
                    EditorGUI.DrawRect(new Rect(X(month.Start), top + referenceInset, 1, height - referenceInset), OKStyles.SecondaryLine);
            }
        }

        /// <summary>Drawn after the rows so it covers rows scrolled underneath it.</summary>
        private void DrawHeader(Rect rect, float top, int year, DateTime end)
        {
            var header = new Rect(rect.x, top, rect.width, HeaderHeight);
            EditorGUI.DrawRect(header, OKStyles.TimelineBackground);
            EditorGUI.DrawRect(new Rect(rect.x, top, LabelWidth, HeaderHeight), OKStyles.LabelColumn);
            DrawColumns(top, HeaderHeight, 16f);

            GUI.Label(new Rect(rect.x + 6, top + 2, LabelWidth - 8, 14), year.ToString(), EditorStyles.boldLabel);

            DrawMonthLabels(_months, _calendar, top + 2);

            if (_reference.Count > 0)
            {
                var firstYear = _start.Year;
                var lastYear = end.AddDays(-1).Year;
                GUI.Label(new Rect(rect.x + 6, top + 16, LabelWidth - 8, 14), firstYear == lastYear ? firstYear.ToString() : $"{firstYear} / {lastYear}", OKStyles.MonthLabel);
                DrawMonthLabels(_reference, CalendarType.Gregorian, top + 16);
            }

            EditorGUI.DrawRect(new Rect(rect.x, top + HeaderHeight - 1, rect.width, 1), OKStyles.GridLine);
        }

        private static readonly GUIContent MonthText = new GUIContent();

        /// <summary>
        /// Full month names when every whole month has room for its name, otherwise short ones, so a row never mixes the two.
        /// A month cut off at the start of the year gets its short name from the left edge.
        /// </summary>
        private void DrawMonthLabels(List<Month> months, CalendarType calendar, float y)
        {
            var full = true;

            foreach (var month in months)
            {
                MonthText.text = CalendarUtility.GetMonthName(calendar, month.Number);

                if (month.Start >= _start && OKStyles.MonthLabel.CalcSize(MonthText).x > X(month.End) - X(month.Start) - 3)
                {
                    full = false;
                    break;
                }
            }

            foreach (var month in months)
            {
                var x0 = X(month.Start);
                var width = X(month.End) - x0 - 3;

                if (width < 12)
                    continue;

                MonthText.text = full && month.Start >= _start ? CalendarUtility.GetMonthName(calendar, month.Number) : ShortMonthName(calendar, month.Number);
                GUI.Label(new Rect(x0 + 3, y, width, 14), MonthText, OKStyles.MonthLabel);
            }
        }

        private static string ShortMonthName(CalendarType calendar, int month)
        {
            var names = calendar == CalendarType.Persian ? PersianShort : calendar == CalendarType.Hijri ? HijriShort : GregorianShort;
            return names[Mathf.Clamp(month, 1, 12) - 1];
        }

        private void CollectMonths(int year, DateTime end)
        {
            if (_months.Count > 0 && _monthsCalendar == _calendar && _monthsYear == year)
                return;

            _monthsCalendar = _calendar;
            _monthsYear = year;
            _months.Clear();
            _reference.Clear();

            for (var month = 1; month <= 12; month++)
            {
                if (!CalendarUtility.TryToDateTime(_calendar, year, month, 1, out var monthStart))
                    continue;

                var monthEnd = end;

                if (month < 12)
                    CalendarUtility.TryToDateTime(_calendar, year, month + 1, 1, out monthEnd);

                _months.Add(new Month(monthStart, monthEnd, month));
            }

            if (_calendar == CalendarType.Gregorian)
                return;

            for (var month = new DateTime(_start.Year, _start.Month, 1); month < end; month = month.AddMonths(1))
                _reference.Add(new Month(month, month.AddMonths(1), month.Month));
        }

        /// <summary>"21 Mar", or "1 Farvardin" on a Persian timeline; with the weekday: "Sat 21 Mar", "Sat 1 Farvardin · 21 Mar".</summary>
        private string DayLabel(DateTime day, bool weekday)
        {
            var gregorian = day.ToString(weekday ? "ddd d MMM" : "d MMM", CultureInfo.InvariantCulture);

            if (_calendar == CalendarType.Gregorian)
                return gregorian;

            var local = $"{CalendarUtility.GetDay(_calendar, day)} {CalendarUtility.GetMonthName(_calendar, CalendarUtility.GetMonth(_calendar, day))}";
            return weekday ? $"{day.ToString("ddd", CultureInfo.InvariantCulture)} {local} · {day.ToString("d MMM", CultureInfo.InvariantCulture)}" : local;
        }

        private void DrawPill(float x, float y, string text, Color color)
        {
            var content = new GUIContent(text);
            var width = Pill.CalcSize(content).x + 2;
            var pill = new Rect(Mathf.Clamp(x - width / 2, _bars.x, _bars.xMax - width), y, width, 14);
            EditorGUI.DrawRect(pill, color);

            var textColor = Pill.normal.textColor;
            Pill.normal.textColor = OKStyles.ContrastText(color);
            Pill.Draw(pill, content, false, false, false, false);
            Pill.normal.textColor = textColor;
        }

        private static void DrawTooltip(Rect area, Vector2 mouse, OccasionInstance instance)
        {
            var occasion = instance.Occasion;
            var text = new StringBuilder();
            text.Append("<b>").Append(occasion.Title).Append("</b>");

            if (!occasion.enabled)
                text.Append("  (disabled)");

            text.Append('\n').Append(DateFormat.Range(instance.MainStart, instance.MainEnd));

            if (occasion.schedule.mode == ScheduleMode.Yearly && occasion.schedule.calendar != CalendarType.Gregorian)
                text.Append('\n').Append(DateFormat.CalendarRange(occasion.schedule.calendar, instance.MainStart, instance.MainEnd));

            if (instance.WindowStart != instance.MainStart || instance.WindowEnd != instance.MainEnd)
                text.Append("\nWindow: ").Append(DateFormat.Range(instance.WindowStart, instance.WindowEnd));

            if (occasion.schedule.countdown == CountdownTarget.MarchEquinox)
                text.Append("\nEquinox: ").Append(DateFormat.Short(instance.CountdownTarget));

            text.Append($"\nPriority {occasion.priority}");

            if (occasion.exclusive)
                text.Append(" · exclusive");

            if (occasion.HasIcon)
                text.Append(" · icon \"").Append(occasion.iconId).Append('"');

            var content = new GUIContent(text.ToString());
            const float width = 300f;
            var height = OKStyles.Tooltip.CalcHeight(content, width);
            var x = Mathf.Min(mouse.x + 14, area.xMax - width - 4);
            var y = mouse.y + 18 + height > area.yMax ? mouse.y - height - 6 : mouse.y + 18;
            var box = new Rect(Mathf.Max(area.x, x), Mathf.Max(area.y, y), width, height);

            EditorGUI.DrawRect(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), new Color(0, 0, 0, 0.35f));
            EditorGUI.DrawRect(box, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.96f, 0.96f, 0.96f));
            OKStyles.Tooltip.Draw(box, content, false, false, false, false);
        }
    }
}
