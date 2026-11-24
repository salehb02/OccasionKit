using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if OCCASIONKIT_NOTIFICATIONS
using OccasionKit.Notifications;
#endif

namespace OccasionKit.EditorTools
{
    /// <summary>The simulator pane on the right of the Occasions tab. It opens when the timeline's day is changed.</summary>
    internal sealed partial class OccasionKitWindow
    {
        private const int AnyStore = 0;
        private const int NoStore = 1;

        [SerializeField] private bool _simulatorOpen;
        [SerializeField] private Vector2 _simulatorScroll;
        [SerializeField] private int _simulatorStore;
        [SerializeField] private string _simulatorLanguage = "";

        private float SimulatorWidth { get => Mathf.Clamp(position.width * 0.34f, 300f, 440f); }

        private void DrawSimulatorPane()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Simulator", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Close", "Hide the simulator. The simulated time stays, for Play Mode; Now resets it."), EditorStyles.toolbarButton, GUILayout.Width(46)))
                    _simulatorOpen = false;
            }

            var occasions = CatalogDatabase.GetMergedOccasions();
            var time = EditorBridge.SimulatedTime;
            var context = GetSimulatorContext();
            var iconIds = EditorBridge.GetIconIds();
            Func<string, bool> isAvailable = id => iconIds.Contains(id);

            // No horizontal scrollbar: the rows below wrap or clip to the pane's width.
            _simulatorScroll = EditorGUILayout.BeginScrollView(_simulatorScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70;

            GUILayout.Space(4);
            GUILayout.Label($"Every catalog merged ({occasions.Count} occasions), evaluated the way the runtime does.", OKStyles.MutedWrap);

            if (OKStyles.Section("sim.time", "Time and context"))
                DrawSimulatorTime(occasions, time);

            var result = OccasionEvaluator.Evaluate(occasions, time, context, isAvailable);

            if (OKStyles.Section("sim.result", "At this time"))
                DrawEvaluation(result, time);

            if (OKStyles.Section("sim.changes", "Icon changes, next 90 days"))
            {
                var changes = OccasionEvaluator.GetIconChanges(occasions, time, time.AddDays(90), context, isAvailable, OccasionSystem.DefaultIconId);
                GUILayout.Label(EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
                    ? "On iOS each change shows on the next launch or return to the app, with a system alert; the icon can't change while the app is closed."
                    : "What the app hands Android when it goes to the background, so the icon also changes while the app is closed.", OKStyles.MutedWrap);

                if (changes.Count == 0)
                    GUILayout.Label("No changes.", OKStyles.Muted);

                foreach (var change in changes)
                    DrawIconRow(change.IconId, $"{DateFormat.Short(change.Time)}   (in {DateFormat.Span(change.Time - time)})");
            }

#if OCCASIONKIT_NOTIFICATIONS
            if (OKStyles.Section("sim.notifications", "Notifications, next 30 days", false))
            {
                var planned = OccasionNotifications.Plan(occasions, time, 30, context);

                if (planned.Count == 0)
                    GUILayout.Label("None. Enable a notification on an occasion, and call OccasionNotifications.Enable() at startup.", OKStyles.MutedWrap);

                foreach (var notification in planned)
                {
                    GUILayout.Label(DateFormat.Short(notification.FireTime), EditorStyles.miniBoldLabel);
                    GUILayout.Label($"<b>{notification.Title}</b>  {notification.Body}", new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                }
            }
#endif

            if (OKStyles.Section("sim.play", "Play Mode"))
                DrawPlayModeSection();

            EditorGUIUtility.labelWidth = labelWidth;
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSimulatorTime(System.Collections.Generic.List<Occasion> occasions, DateTime time)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Time");
                var text = new LocalDateTime(time).ToString();
                var next = EditorGUILayout.DelayedTextField(text, GUILayout.MinWidth(110));

                if (next != text && LocalDateTime.TryParse(next, out var parsed) && parsed.TryToDateTime(out var parsedTime))
                    SetSimulatedTime(parsedTime);

                var jump = GUILayoutUtility.GetRect(new GUIContent("Jump to"), EditorStyles.popup, GUILayout.Width(76));

                if (EditorGUI.DropdownButton(jump, new GUIContent("Jump to"), FocusType.Passive))
                    ShowJumpMenu(jump, occasions, time);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);

                if (GUILayout.Button("Now", EditorStyles.miniButtonLeft, GUILayout.Width(42)))
                    SetSimulatedTime(DateTime.Now);

                if (GUILayout.Button("−1d", EditorStyles.miniButtonMid, GUILayout.Width(36)))
                    SetSimulatedTime(time.AddDays(-1));

                if (GUILayout.Button("+1d", EditorStyles.miniButtonMid, GUILayout.Width(36)))
                    SetSimulatedTime(time.AddDays(1));

                if (GUILayout.Button("−1h", EditorStyles.miniButtonMid, GUILayout.Width(36)))
                    SetSimulatedTime(time.AddHours(-1));

                if (GUILayout.Button("+1h", EditorStyles.miniButtonRight, GUILayout.Width(36)))
                    SetSimulatedTime(time.AddHours(1));

                GUILayout.FlexibleSpace();
            }

            Indented($"{time:dddd} · {CalendarUtility.FormatDate(CalendarType.Persian, time)} · {CalendarUtility.FormatDate(CalendarType.Hijri, time)} (arithmetic)");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Store");
                var stores = new[] { "Any (ignore targeting)", "No store" }.Concat(Settings.stores).ToArray();
                _simulatorStore = EditorGUILayout.Popup(Mathf.Clamp(_simulatorStore, 0, stores.Length - 1), stores, GUILayout.MinWidth(110));
            }

            using (new EditorGUI.DisabledScope(_simulatorStore == AnyStore))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Language", "ISO code such as fa or en. Empty: this computer's language."));
                _simulatorLanguage = EditorGUILayout.DelayedTextField(_simulatorLanguage, GUILayout.Width(50)).Trim();
                GUILayout.Label(string.IsNullOrEmpty(_simulatorLanguage) ? $"({LanguageCodes.FromDevice()})" : "", OKStyles.Muted);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>A wrapped, muted line lined up with the fields.</summary>
        private static void Indented(string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                GUILayout.Label(text, OKStyles.MutedWrap);
            }
        }

        private OccasionContext GetSimulatorContext()
        {
            if (_simulatorStore == AnyStore)
                return OccasionContext.Any;

            var language = string.IsNullOrEmpty(_simulatorLanguage) ? LanguageCodes.FromDevice() : LanguageCodes.Normalize(_simulatorLanguage);
            var storeIndex = _simulatorStore - 2;
            var store = storeIndex >= 0 && storeIndex < Settings.stores.Count ? Settings.stores[storeIndex] : null;
            return new OccasionContext(store, language);
        }

        /// <summary>Moves the simulated time (to the minute) and the timeline to its year.</summary>
        private void SetSimulatedTime(DateTime time)
        {
            EditorBridge.SimulatedTime = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);
            _year = CalendarUtility.GetYear(_timelineCalendar, time);
            GUI.FocusControl(null);

            if (EditorApplication.isPlaying && EditorBridge.DrivePlayModeClock)
                EditorBridge.ApplyClockToPlayMode();

            Repaint();
        }

        private void ShowJumpMenu(Rect rect, System.Collections.Generic.List<Occasion> occasions, DateTime time)
        {
            var menu = new GenericMenu();

            foreach (var occasion in occasions)
            {
                var instance = ScheduleResolver.GetInstanceAt(occasion, time) ?? ScheduleResolver.GetNextInstance(occasion, time);

                if (instance == null)
                {
                    menu.AddDisabledItem(new GUIContent(occasion.Title + "/No upcoming dates"));
                    continue;
                }

                var name = occasion.Title.Replace("/", "∕");
                menu.AddItem(new GUIContent($"{name}/Window opens  ({DateFormat.Short(instance.WindowStart)})"), false, () => SetSimulatedTime(instance.WindowStart));
                menu.AddItem(new GUIContent($"{name}/Main start  ({DateFormat.Short(instance.MainStart)})"), false, () => SetSimulatedTime(instance.MainStart));
                menu.AddItem(new GUIContent($"{name}/Last minute  ({DateFormat.Short(instance.WindowEnd.AddMinutes(-1))})"), false, () => SetSimulatedTime(instance.WindowEnd.AddMinutes(-1)));
                menu.AddItem(new GUIContent($"{name}/Window closed  ({DateFormat.Short(instance.WindowEnd)})"), false, () => SetSimulatedTime(instance.WindowEnd));

                if (occasion.schedule.countdown == CountdownTarget.MarchEquinox)
                    menu.AddItem(new GUIContent($"{name}/Equinox  ({DateFormat.Short(instance.CountdownTarget)})"), false, () => SetSimulatedTime(instance.CountdownTarget));
            }

            if (occasions.Count == 0)
                menu.AddDisabledItem(new GUIContent("No occasions"));

            menu.DropDown(rect);
        }

        private void DrawEvaluation(Evaluation result, DateTime time)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(72, 72, GUILayout.Width(72), GUILayout.Height(72));
                var variant = Settings.FindIcon(result.IconId);
                var preview = variant != null ? IconPreviewCache.Get(variant, 144, MaskShape.Circle) : null;

                if (preview != null)
                {
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                }
                else
                {
                    OKStyles.ColorChip(rect, OKStyles.LabelColumn);
                    GUI.Label(rect, "default", OKStyles.Badge);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(10);
                    GUILayout.Label(result.IconId == null ? "Launcher icon: default" : $"Launcher icon: {result.IconId}", EditorStyles.boldLabel);
                    GUILayout.Label(result.IconSource != null ? $"from {result.IconSource.Occasion.Title} (priority {result.IconSource.Occasion.priority})" : "No active occasion has an icon; Player Settings icon.", OKStyles.MutedWrap);
                }
            }

            GUILayout.Space(4);
            GUILayout.Label(result.Active.Count == 0 ? "No active occasions." : "Active, highest priority first", EditorStyles.miniBoldLabel);

            foreach (var instance in result.Active)
            {
                var phase = instance.GetPhase(time);
                var countdown = instance.GetCountdown(time);
                var detail = $"{phase} · priority {instance.Occasion.priority}{(instance.Occasion.exclusive ? " · exclusive" : "")} · {DateFormat.Range(instance.WindowStart, instance.WindowEnd)}";

                if (countdown > TimeSpan.Zero)
                    detail += $" · countdown {DateFormat.Span(countdown)}";

                DrawOccasionRow(instance.Occasion, detail, true);
            }

            if (result.Suppressed.Count > 0)
            {
                GUILayout.Space(2);
                GUILayout.Label("In their window but not active", EditorStyles.miniBoldLabel);

                foreach (var suppressed in result.Suppressed)
                {
                    var reason = suppressed.Reason == SuppressionReason.Exclusive ? $"suppressed by {suppressed.By.Title} (exclusive)"
                        : suppressed.Reason == SuppressionReason.Targeting ? "filtered out by store or language targeting"
                        : "disabled";

                    DrawOccasionRow(suppressed.Instance.Occasion, reason, false);
                }
            }
        }

        /// <summary>The name, with the detail wrapped under it to fit the pane.</summary>
        private static void DrawOccasionRow(Occasion occasion, string detail, bool active)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(8)))
                {
                    GUILayout.Space(5);
                    OKStyles.ColorChip(GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(8)), active ? occasion.color : OKStyles.Disabled);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(occasion.Title, active ? EditorStyles.boldLabel : EditorStyles.label);
                    GUILayout.Label(detail, OKStyles.MutedWrap);
                }
            }
        }

        private void DrawIconRow(string iconId, string detail)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                var rect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18), GUILayout.Height(18));
                var variant = Settings.FindIcon(iconId);
                var preview = variant != null ? IconPreviewCache.Get(variant, 36, MaskShape.Circle) : null;

                if (preview != null)
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);

                GUILayout.Label(iconId, EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label(detail, OKStyles.MutedWrap);
            }
        }

        private void DrawPlayModeSection()
        {
            var drive = EditorGUILayout.ToggleLeft(new GUIContent("Use the simulator time in Play Mode", "OccasionClock starts at the simulator time and keeps running. Changing the time above moves it."), EditorBridge.DrivePlayModeClock);

            if (drive != EditorBridge.DrivePlayModeClock)
            {
                EditorBridge.DrivePlayModeClock = drive;
                EditorBridge.ApplyClockToPlayMode();
            }

            if (!EditorApplication.isPlaying)
            {
                GUILayout.Label("Enter Play Mode to drive the real runtime. The Editor fakes icon switching and logs it.", OKStyles.MutedWrap);
                return;
            }

            if (!OccasionSystem.IsInitialized)
            {
                GUILayout.Label("OccasionSystem isn't initialized. The game calls OccasionSystem.InitializeAsync() at startup.", OKStyles.MutedWrap);

                if (GUILayout.Button("Initialize now", GUILayout.Width(120)))
                {
                    var context = GetSimulatorContext();
                    _ = OccasionSystem.InitializeAsync(new OccasionConfig { Store = context.Store, CatalogKey = Settings.catalogLabel, Verbose = true });
                }

                return;
            }

            var backend = OccasionSystem.IconBackend;
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 110;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Clock", $"{DateFormat.Short(OccasionClock.Now)}{(OccasionClock.IsSimulated ? "  (simulated)" : "")}");
                EditorGUILayout.LabelField("Context", OccasionSystem.Context?.ToString() ?? "-");
                EditorGUILayout.LabelField("Catalogs", string.Join(", ", OccasionSystem.Catalogs.Select(c => c.name)) + $"  ({OccasionSystem.Occasions.Count} occasions)");
                EditorGUILayout.LabelField("Active", OccasionSystem.Active.Count == 0 ? "-" : string.Join(", ", OccasionSystem.Active.Select(a => a.Id)));
                EditorGUILayout.LabelField("Target icon", OccasionSystem.TargetIcon);
                EditorGUILayout.LabelField("Shown icon", backend?.CurrentIcon ?? "-");

                var seasonal = EditorGUILayout.Toggle(new GUIContent("Seasonal icons", "The player setting OccasionSystem.SeasonalIconsEnabled."), OccasionSystem.SeasonalIconsEnabled);

                if (seasonal != OccasionSystem.SeasonalIconsEnabled)
                    OccasionSystem.SeasonalIconsEnabled = seasonal;

                var greetings = OccasionSystem.GetPendingGreetings();
                EditorGUILayout.LabelField("Pending greetings", greetings.Count == 0 ? "-" : string.Join(", ", greetings.Select(g => g.Key)));

                var scheduled = EditorAppIconBackend.Scheduled;
                EditorGUILayout.LabelField("Scheduled", scheduled.Count == 0 ? "- (filled when the app goes to the background)" : string.Join("   ", scheduled.Select(c => c.ToString())), EditorStyles.wordWrappedLabel);
            }

            EditorGUIUtility.labelWidth = labelWidth;

            // Two rows so the buttons fit the pane.
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                    OccasionSystem.Refresh();

                if (GUILayout.Button(new GUIContent("Go to background", "What happens when the app is paused: apply the icon, schedule changes, notify add-ons.")))
                    OccasionSystem.OnBackground();

                if (GUILayout.Button("Back to foreground"))
                    OccasionSystem.OnForeground();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset greetings"))
                    OccasionSystem.ResetGreetings();

                if (GUILayout.Button("Reset shown icon"))
                    EditorAppIconBackend.ResetSimulatedIcon();
            }
        }
    }
}
