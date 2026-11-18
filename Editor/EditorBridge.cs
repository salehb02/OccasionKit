using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;

namespace OccasionKit.EditorTools
{
    /// <summary>Connects the runtime to editor-only data: the icon set for the simulated backend, and the simulator clock.</summary>
    [InitializeOnLoad]
    internal static class EditorBridge
    {
        private static string Key(string name) => $"OccasionKit.{PlayerSettings.productGUID}.{name}";

        static EditorBridge()
        {
            EditorAppIconBackend.IconsProvider = GetIconIds;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static IReadOnlyList<string> GetIconIds()
        {
            var ids = new List<string> { OccasionSystem.DefaultIconId };
            var settings = OccasionKitSettings.Instance;

            if (settings != null)
                ids.AddRange(settings.icons.Where(i => i != null && i.foreground != null && OccasionValidator.IsValidIconId(i.id)).Select(i => i.id).Distinct());

            return ids;
        }

        /// <summary>When on, Play Mode starts with the simulator's time on <see cref="OccasionClock"/>.</summary>
        public static bool DrivePlayModeClock
        {
            get => EditorPrefs.GetBool(Key("DrivePlayMode"), false);
            set => EditorPrefs.SetBool(Key("DrivePlayMode"), value);
        }

        /// <summary>The calendar picked for the OccasionKit timelines, so the catalog inspector uses it too.</summary>
        public static CalendarType TimelineCalendar
        {
            get => (CalendarType)EditorPrefs.GetInt(Key("TimelineCalendar"), (int)CalendarType.Gregorian);
            set => EditorPrefs.SetInt(Key("TimelineCalendar"), (int)value);
        }

        public static DateTime SimulatedTime
        {
            get
            {
                var text = EditorPrefs.GetString(Key("SimulatedTime"), "");
                return DateTime.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : DateTime.Now;
            }
            set => EditorPrefs.SetString(Key("SimulatedTime"), value.ToString("o", CultureInfo.InvariantCulture));
        }

        public static void ApplyClockToPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (DrivePlayModeClock)
                OccasionClock.Simulate(SimulatedTime);
            else
                OccasionClock.ClearSimulation();

            OccasionSystem.Refresh();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (DrivePlayModeClock)
                    OccasionClock.Simulate(SimulatedTime);
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                OccasionClock.ClearSimulation();
            }
        }
    }
}
