using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if OCCASIONKIT_NOTIFICATIONS
using OccasionKit.Notifications;
#endif

namespace OccasionKit.EditorTools
{
    internal sealed partial class OccasionKitWindow
    {
        private const int AnyStore = 0;
        private const int NoStore = 1;

        [SerializeField] private Vector2 _simulatorScroll;
        [SerializeField] private Vector2 _simulatorTimelineScroll;
        [SerializeField] private int _simulatorStore;
        [SerializeField] private string _simulatorLanguage = "";

        private void DrawSimulatorTab()
        {
            var occasions = CatalogDatabase.GetMergedOccasions();
            DrawSimulatorTimeline(occasions);

            var time = EditorBridge.SimulatedTime;
            var context = GetSimulatorContext();
            var iconIds = EditorBridge.GetIconIds();
            Func<string, bool> isAvailable = id => iconIds.Contains(id);

            _simulatorScroll = EditorGUILayout.BeginScrollView(_simulatorScroll);
            OKStyles.BeginForm();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 110;

            GUILayout.Space(4);
            GUILayout.Label($"Every catalog merged ({occasions.Count} occasions), evaluated the way the runtime does.", OKStyles.Subtitle);

            if (OKStyles.Section("sim.time", "Time and context"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Time");
                    var text = new LocalDateTime(time).ToString();
                    var next = EditorGUILayout.DelayedTextField(text, GUILayout.Width(130));

                    if (next != text && LocalDateTime.TryParse(next, out var parsed) && parsed.TryToDateTime(out var parsedTime))
                        SetSimulatedTime(parsedTime);

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

                    var jump = GUILayoutUtility.GetRect(new GUIContent("Jump to"), EditorStyles.popup, GUILayout.Width(90));

                    if (EditorGUI.DropdownButton(jump, new GUIContent("Jump to"), FocusType.Passive))
                        ShowJumpMenu(jump, occasions, time);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField(" ", $"{time:dddd} · {CalendarUtility.FormatDate(CalendarType.Persian, time)} · {CalendarUtility.FormatDate(CalendarType.Hijri, time)} (arithmetic)", OKStyles.Muted);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Store");
                    var stores = new[] { "Any (ignore targeting)", "No store" }.Concat(Settings.stores).ToArray();
                    _simulatorStore = EditorGUILayout.Popup(Mathf.Clamp(_simulatorStore, 0, stores.Length - 1), stores, GUILayout.Width(180));

                    using (new EditorGUI.DisabledScope(_simulatorStore == AnyStore))
                    {
                        GUILayout.Label(new GUIContent("Language", "ISO code such as fa or en. Empty: this computer's language."), GUILayout.Width(62));
                        _simulatorLanguage = EditorGUILayout.DelayedTextField(_simulatorLanguage, GUILayout.Width(50)).Trim();
                        GUILayout.Label(string.IsNullOrEmpty(_simulatorLanguage) ? $"({LanguageCodes.FromDevice()})" : "", OKStyles.Muted);
                    }

                    GUILayout.FlexibleSpace();
                }
            }

            var result = OccasionEvaluator.Evaluate(occasions, time, context, isAvailable);

            if (OKStyles.Section("sim.result", "At this time"))
                DrawEvaluation(result, time);

            if (OKStyles.Section("sim.changes", "Icon changes, next 90 days"))
            {
                var changes = OccasionEvaluator.GetIconChanges(occasions, time, time.AddDays(90), context, isAvailable, OccasionSystem.DefaultIconId);
                GUILayout.Label("What the app hands Android when it goes to the background, so the icon also changes while the app is closed.", OKStyles.MutedWrap);

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
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(DateFormat.Short(notification.FireTime), GUILayout.Width(160));
                        GUILayout.Label($"<b>{notification.Title}</b>  {notification.Body}", new GUIStyle(EditorStyles.label) { richText = true });
                    }
                }
            }
#endif

            if (OKStyles.Section("sim.play", "Play Mode"))
                DrawPlayModeSection();

            EditorGUIUtility.labelWidth = labelWidth;
            OKStyles.EndForm();
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The merged occasions on a year timeline. Click or drag across it to move the simulated day; click a name to edit it.</summary>
        private void DrawSimulatorTimeline(System.Collections.Generic.List<Occasion> occasions)
        {
            var time = EditorBridge.SimulatedTime;
            var year = CalendarUtility.GetYear(_timelineCalendar, time);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Animation.PrevKey").image, "A year earlier"), EditorStyles.toolbarButton, GUILayout.Width(26)))
                    SetSimulatedTime(AddYears(time, -1));

                GUILayout.Label(year.ToString(), EditorStyles.toolbarButton, GUILayout.Width(44));

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Animation.NextKey").image, "A year later"), EditorStyles.toolbarButton, GUILayout.Width(26)))
                    SetSimulatedTime(AddYears(time, 1));

                DrawCalendarPicker();
                GUILayout.Space(6);
                GUILayout.Label("Click or drag the timeline to set the day", OKStyles.Muted);
                GUILayout.FlexibleSpace();
                DrawTimelineLegend();
            }

            var reveal = false;
            var clicked = DrawTimeline(occasions, year, -1, time, ref _simulatorTimelineScroll, ref reveal, true);

            if (_timeline.Scrubbed.HasValue)
            {
                var next = _timeline.Scrubbed.Value.Add(time.TimeOfDay);

                if (next != time)
                {
                    SetSimulatedTime(next);
                    Repaint();
                }
            }

            if (clicked >= 0 && clicked < occasions.Count)
                EditOccasion(occasions[clicked]);
        }

        /// <summary>The same day a year away in the picked calendar (1 Farvardin stays 1 Farvardin).</summary>
        private DateTime AddYears(DateTime time, int years)
        {
            try
            {
                return CalendarUtility.Get(_timelineCalendar).AddYears(time, years);
            }
            catch (ArgumentException)
            {
                return time;
            }
        }

        private void EditOccasion(Occasion occasion)
        {
            foreach (var (catalog, index, candidate) in CatalogDatabase.Enumerate())
            {
                if (candidate != occasion)
                    continue;

                _tab = Tab.Occasions;
                SelectCatalog(catalog);
                SelectOccasion(index);
                GUI.FocusControl(null);
                Repaint();
                return;
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

        private void SetSimulatedTime(DateTime time)
        {
            EditorBridge.SimulatedTime = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);
            GUI.FocusControl(null);

            if (EditorApplication.isPlaying && EditorBridge.DrivePlayModeClock)
                EditorBridge.ApplyClockToPlayMode();
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
                    GUILayout.Label(result.IconSource != null ? $"from {result.IconSource.Occasion.Title} (priority {result.IconSource.Occasion.priority})" : "No active occasion has an icon; Player Settings icon.", OKStyles.Muted);
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

        private static void DrawOccasionRow(Occasion occasion, string detail, bool active)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                OKStyles.ColorChip(GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(16)), active ? occasion.color : OKStyles.Disabled);
                GUILayout.Label(occasion.Title, active ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(170));
                GUILayout.Label(detail, OKStyles.Muted);
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

                GUILayout.Label(iconId, EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label(detail, OKStyles.Muted);
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                    OccasionSystem.Refresh();

                if (GUILayout.Button(new GUIContent("Go to background", "What happens when the app is paused: apply the icon, schedule changes, notify add-ons.")))
                    OccasionSystem.OnBackground();

                if (GUILayout.Button("Back to foreground"))
                    OccasionSystem.OnForeground();

                if (GUILayout.Button("Reset greetings"))
                    OccasionSystem.ResetGreetings();

                if (GUILayout.Button("Reset shown icon"))
                    EditorAppIconBackend.ResetSimulatedIcon();
            }
        }
    }
}
