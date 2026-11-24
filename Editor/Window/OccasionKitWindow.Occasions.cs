using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal sealed partial class OccasionKitWindow
    {
        private const float ListWidth = 290f;

        [SerializeField] private Vector2 _listScroll;
        [SerializeField] private Vector2 _detailScroll;
        [SerializeField] private Vector2 _timelineScroll;

        private readonly TimelineView _timeline = new TimelineView();
        private ReorderableList _occasionList;

        // Timeline row -> catalog index, and the occasions in that order.
        private readonly List<int> _timelineOrder = new List<int>();
        private readonly List<Occasion> _timelineRows = new List<Occasion>();

        // Scroll the list and the timeline to the selection once it changes from somewhere else.
        [NonSerialized] private bool _revealInList;
        [NonSerialized] private bool _revealInTimeline;
        [NonSerialized] private float _listViewHeight;

        private void DrawOccasionsTab()
        {
            if (_catalog == null)
            {
                DrawEmpty("No occasion catalog yet", "A catalog is an addressable list of occasions. The runtime loads every catalog with the OccasionKit label and merges them.", "Create catalog", CreateCatalog);
                return;
            }

            // Read once so every event of this pass lays out the same; opening or closing shows on the next pass.
            var showSimulator = _simulatorOpen;

            HandleOccasionCommands();
            DrawTimelineSection();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    RevealInList();
                    _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                    GetOccasionList().DoLayoutList();
                    EditorGUILayout.EndScrollView();

                    if (Event.current.type == EventType.Repaint)
                        _listViewHeight = GUILayoutUtility.GetLastRect().height;
                }

                var separator = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(separator, OKStyles.GridLine);

                using (new EditorGUILayout.VerticalScope())
                {
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    OKStyles.BeginForm();
                    DrawOccasionDetails();
                    OKStyles.EndForm();
                    GUILayout.Space(12);
                    EditorGUILayout.EndScrollView();
                }

                if (showSimulator)
                {
                    var simulatorSeparator = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));

                    if (Event.current.type == EventType.Repaint)
                        EditorGUI.DrawRect(simulatorSeparator, OKStyles.GridLine);

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(SimulatorWidth)))
                        DrawSimulatorPane();
                }
            }
        }

        private void SelectOccasion(int index)
        {
            _selected = index;
            _revealInList = true;
            _revealInTimeline = true;
        }

        private void RevealInList()
        {
            if (!_revealInList || Event.current.type != EventType.Layout || _listViewHeight <= 0 || _selected < 0)
                return;

            _revealInList = false;
            var list = GetOccasionList();
            var top = list.headerHeight + 4 + _selected * list.elementHeight;
            var bottom = top + list.elementHeight + 8;

            if (top < _listScroll.y)
                _listScroll.y = Mathf.Max(0, top - 4);
            else if (bottom > _listScroll.y + _listViewHeight)
                _listScroll.y = bottom - _listViewHeight;
        }

        /// <summary>Duplicate (Ctrl+D) and Delete for the selected occasion, only while the list or nothing has keyboard focus.</summary>
        private void HandleOccasionCommands()
        {
            var e = Event.current;

            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand)
                return;

            if (EditorGUIUtility.editingTextField || _selected < 0 || _selected >= _catalog.occasions.Count)
                return;

            if (GUIUtility.keyboardControl != 0 && (_occasionList == null || !_occasionList.HasKeyboardControl()))
                return;

            var execute = e.type == EventType.ExecuteCommand;

            switch (e.commandName)
            {
                case "Duplicate":
                    e.Use();

                    if (execute)
                        DuplicateOccasion(_selected);

                    break;

                case "Delete":
                case "SoftDelete":
                    e.Use();

                    if (execute)
                        RemoveOccasion(_selected);

                    break;
            }
        }

        private void DrawTimelineSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Animation.PrevKey").image, "Previous year"), EditorStyles.toolbarButton, GUILayout.Width(26)))
                    _year--;

                GUILayout.Label(_year.ToString(), EditorStyles.toolbarButton, GUILayout.Width(44));

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Animation.NextKey").image, "Next year"), EditorStyles.toolbarButton, GUILayout.Width(26)))
                    _year++;

                var thisYear = CalendarUtility.GetYear(_timelineCalendar, DateTime.Now);

                using (new EditorGUI.DisabledScope(_year == thisYear))
                {
                    if (GUILayout.Button("This year", EditorStyles.toolbarButton, GUILayout.Width(66)))
                        _year = thisYear;
                }

                DrawCalendarPicker();
                GUILayout.Space(6);

                var open = GUILayout.Toggle(_simulatorOpen, new GUIContent("Simulator", "What's active, the launcher icon and Play Mode at a simulated time. Click or drag the timeline's bars to pick the day; click a name to edit it."), EditorStyles.toolbarButton, GUILayout.Width(70));

                if (open != _simulatorOpen)
                    _simulatorOpen = open;

                GUILayout.FlexibleSpace();
                DrawTimelineLegend();
            }

            SortTimelineRows(_catalog.occasions);
            DateTime? marker = ShowMarker ? EditorBridge.SimulatedTime : (DateTime?)null;
            var selectedRow = _timelineOrder.IndexOf(_selected);
            var clicked = DrawTimeline(_timelineRows, _year, selectedRow, marker, ref _timelineScroll, ref _revealInTimeline, true);

            // Changing the day opens the simulator pane on the right.
            if (_timeline.Scrubbed.HasValue)
            {
                var time = EditorBridge.SimulatedTime;
                var next = _timeline.Scrubbed.Value.Add(time.TimeOfDay);
                _simulatorOpen = true;

                if (next != time)
                    SetSimulatedTime(next);

                Repaint();
            }

            if (clicked >= 0 && clicked < _timelineOrder.Count)
            {
                SelectOccasion(_timelineOrder[clicked]);
                _revealInTimeline = false;
                GUI.FocusControl(null);
                Repaint();
            }
        }

        /// <summary>The catalog's occasions in timeline order: highest priority first, ties in catalog order.</summary>
        private void SortTimelineRows(List<Occasion> occasions)
        {
            _timelineOrder.Clear();

            for (var i = 0; i < occasions.Count; i++)
                _timelineOrder.Add(i);

            _timelineOrder.Sort((a, b) =>
            {
                var byPriority = Priority(occasions[b]).CompareTo(Priority(occasions[a]));
                return byPriority != 0 ? byPriority : a.CompareTo(b);
            });

            _timelineRows.Clear();

            foreach (var index in _timelineOrder)
                _timelineRows.Add(occasions[index]);
        }

        private static int Priority(Occasion occasion) => occasion != null ? occasion.priority : int.MinValue;

        private void DrawTimelineLegend()
        {
            Legend(OKStyles.Today, "Today");

            if (ShowMarker)
                Legend(OKStyles.Simulated, "Simulator");

            Legend(new Color(0.5f, 0.7f, 1f, 0.35f), "Lead / tail");
            Legend(new Color(0.5f, 0.7f, 1f), "Main");
            GUILayout.Label("Striped: exclusive", OKStyles.Muted);
            GUILayout.Space(4);
        }

        /// <summary>The year timeline (<paramref name="year"/> is in the picked calendar), scrolling with a fixed month header when it's taller than about a third of the window.</summary>
        private int DrawTimeline(IReadOnlyList<Occasion> occasions, int year, int selected, DateTime? marker, ref Vector2 scroll, ref bool reveal, bool scrub)
        {
            var height = TimelineView.GetHeight(occasions.Count);
            var maxHeight = Mathf.Max(140f, position.height * 0.38f);

            if (height <= maxHeight)
            {
                reveal = false;
                var rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
                return _timeline.Draw(rect, _timelineCalendar, year, occasions, selected, marker, 0f, scrub);
            }

            if (reveal && Event.current.type == EventType.Layout)
            {
                reveal = false;

                if (selected >= 0)
                {
                    var top = TimelineView.GetRowTop(selected) - TimelineView.HeaderHeight;
                    var bottom = TimelineView.GetRowTop(selected) + TimelineView.RowHeight;

                    if (top < scroll.y)
                        scroll.y = top;
                    else if (bottom > scroll.y + maxHeight)
                        scroll.y = bottom - maxHeight;
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(maxHeight));
            var content = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
            var clicked = _timeline.Draw(content, _timelineCalendar, year, occasions, selected, marker, scroll.y, scrub);
            EditorGUILayout.EndScrollView();
            return clicked;
        }

        /// <summary>Gregorian, Persian or Hijri years and months on the timelines. The Occasions tab keeps showing about the same dates.</summary>
        private void DrawCalendarPicker()
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent("Gregorian"), EditorStyles.toolbarPopup, GUILayout.Width(86));
            var next = (CalendarType)EditorGUI.EnumPopup(rect, _timelineCalendar, EditorStyles.toolbarPopup);
            GUI.Label(rect, new GUIContent("", "Calendar for the timeline's years and months. Hijri is arithmetic and can be a day or two off the sighted calendar."));

            if (next == _timelineCalendar)
                return;

            var anchor = DateTime.Now;

            if (TimelineView.TryGetYear(_timelineCalendar, _year, out var start, out var end) && (anchor < start || anchor >= end))
                anchor = start.AddDays((end - start).TotalDays / 2);

            _timelineCalendar = next;
            _year = CalendarUtility.GetYear(next, anchor);
            EditorBridge.TimelineCalendar = next;
        }

        /// <summary>The simulated day on the timeline, only while the simulator is open.</summary>
        private bool ShowMarker { get => _simulatorOpen; }

        private static void Legend(Color color, string label)
        {
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            rect.y += 5;
            OKStyles.ColorChip(rect, color);
            GUILayout.Label(label, OKStyles.Muted);
        }

        private ReorderableList GetOccasionList()
        {
            if (_occasionList == null || _occasionList.serializedProperty.serializedObject != _catalogObject)
            {
                _occasionList = new ReorderableList(_catalogObject, _catalogObject.FindProperty(nameof(OccasionCatalog.occasions)), true, true, true, true)
                {
                    elementHeight = 40,
                    drawHeaderCallback = rect => GUI.Label(rect, $"{_catalog.name}  ·  {_catalog.occasions.Count} occasions", EditorStyles.boldLabel),
                    drawElementCallback = DrawOccasionElement,
                    onSelectCallback = list =>
                    {
                        _selected = list.index;
                        _revealInTimeline = true;
                        GUI.FocusControl(null);
                    },
                    onAddDropdownCallback = (rect, list) => ShowAddMenu(rect),
                    onRemoveCallback = list => RemoveOccasion(list.index),
                    onReorderCallbackWithDetails = (list, from, to) => _selected = to,
                };
            }

            _occasionList.index = _selected;
            return _occasionList;
        }

        private void DrawOccasionElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= _catalog.occasions.Count)
                return;

            var occasion = _catalog.occasions[index];
            var now = DateTime.Now;

            OKStyles.ColorChip(new Rect(rect.x - 2, rect.y + 5, 4, rect.height - 10), occasion.enabled ? occasion.color : OKStyles.Disabled);

            var iconSize = 30f;
            var right = rect.xMax;
            var variant = Settings != null ? Settings.FindIcon(occasion.iconId) : null;

            if (occasion.HasIcon)
            {
                var iconRect = new Rect(right - iconSize, rect.y + (rect.height - iconSize) / 2, iconSize, iconSize);
                var preview = IconPreviewCache.Get(variant, 60, MaskShape.Squircle);

                if (preview != null)
                    GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);
                else
                    GUI.Label(iconRect, OKStyles.SeverityIcon(IssueSeverity.Warning));

                right -= iconSize + 4;
            }

            var statusRight = right;

            if (occasion.exclusive)
            {
                var badge = new Rect(right - 34, rect.y + 4, 34, 15);
                GUI.Label(badge, new GUIContent("EXCL", "Exclusive: suppresses lower-priority occasions while active"), OKStyles.Badge);
                right -= 36;
            }

            var previous = GUI.contentColor;

            if (!occasion.enabled)
                GUI.contentColor = new Color(1, 1, 1, 0.5f);

            GUI.Label(new Rect(rect.x + 8, rect.y + 3, right - rect.x - 8, 18), occasion.Title, OKStyles.RowTitle);

            var status = GetStatus(occasion, now, out var statusColor);
            var statusStyle = OKStyles.Muted;
            var oldColor = statusStyle.normal.textColor;

            if (statusColor.HasValue)
                statusStyle.normal.textColor = statusColor.Value;

            GUI.Label(new Rect(rect.x + 8, rect.y + 21, statusRight - rect.x - 8, 15), status, statusStyle);
            statusStyle.normal.textColor = oldColor;
            GUI.contentColor = previous;

            var e = Event.current;

            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                var i = index;
                menu.AddItem(new GUIContent("Simulate"), false, () => SimulateOccasion(occasion));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateOccasion(i));
                menu.AddItem(new GUIContent("Copy id"), false, () => EditorGUIUtility.systemCopyBuffer = occasion.id);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, () => RemoveOccasion(i));
                menu.ShowAsContext();
                e.Use();
            }
        }

        private static string GetStatus(Occasion occasion, DateTime now, out Color? color)
        {
            color = null;

            if (!occasion.enabled)
                return $"{occasion.id} · disabled";

            var current = ScheduleResolver.GetInstanceAt(occasion, now);

            if (current != null)
            {
                color = OKStyles.Active;
                var phase = current.GetPhase(now);
                return phase == OccasionPhase.Main
                    ? $"{occasion.id} · active, ends in {DateFormat.Span(current.MainEnd - now)}"
                    : $"{occasion.id} · active ({phase.ToString().ToLowerInvariant()} days)";
            }

            var next = ScheduleResolver.GetNextInstance(occasion, now);

            if (next == null)
            {
                color = OKStyles.Warning;
                return $"{occasion.id} · no upcoming dates";
            }

            return $"{occasion.id} · in {DateFormat.Span(next.WindowStart - now)} · {DateFormat.Day(next.WindowStart)}";
        }

        /// <summary>Opens the simulator pane at the start of the occasion's current or next main window.</summary>
        private void SimulateOccasion(Occasion occasion)
        {
            var now = DateTime.Now;
            var instance = ScheduleResolver.GetInstanceAt(occasion, now) ?? ScheduleResolver.GetNextInstance(occasion, now);

            if (instance == null)
            {
                ShowNotification(new GUIContent($"{occasion.Title} has no upcoming dates"));
                return;
            }

            SetSimulatedTime(instance.MainStart);
            _simulatorOpen = true;
            Repaint();
        }

        private void ShowAddMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Blank occasion"), false, () => AddOccasion(OccasionPresets.Blank("occasion")));
            menu.AddSeparator("");

            foreach (var preset in OccasionPresets.All)
            {
                var create = preset.Create;
                menu.AddItem(new GUIContent(preset.Name), false, () => AddOccasion(create()));
            }

            menu.DropDown(rect);
        }

        private void AddOccasion(Occasion occasion)
        {
            occasion.id = UniqueOccasionId(occasion.id);
            ModifyCatalog("Add occasion", () => _catalog.occasions.Add(occasion));
            SelectOccasion(_catalog.occasions.Count - 1);
            _occasionList = null;
            Repaint();
        }

        private void DuplicateOccasion(int index)
        {
            if (index < 0 || index >= _catalog.occasions.Count)
                return;

            var copy = JsonUtility.FromJson<Occasion>(JsonUtility.ToJson(_catalog.occasions[index]));
            copy.id = UniqueOccasionId(copy.id);
            copy.displayName += " (copy)";
            ModifyCatalog("Duplicate occasion", () => _catalog.occasions.Insert(index + 1, copy));
            SelectOccasion(index + 1);
            _occasionList = null;
            Repaint();
        }

        private void RemoveOccasion(int index)
        {
            if (index < 0 || index >= _catalog.occasions.Count)
                return;

            ModifyCatalog("Remove occasion", () => _catalog.occasions.RemoveAt(index));
            SelectOccasion(Mathf.Min(index, _catalog.occasions.Count - 1));
            _occasionList = null;
            Repaint();

            if (Event.current != null)
                GUIUtility.ExitGUI();
        }

        private string UniqueOccasionId(string id)
        {
            var taken = CatalogDatabase.GetOccasionIds().ToHashSet();

            if (!taken.Contains(id))
                return id;

            for (var n = 2; ; n++)
            {
                var candidate = $"{id}_{n}";

                if (!taken.Contains(candidate))
                    return candidate;
            }
        }

        private void DrawOccasionDetails()
        {
            if (_selected < 0 || _selected >= _catalog.occasions.Count)
            {
                GUILayout.Space(40);
                GUILayout.Label("Select an occasion, or add one with +.", OKStyles.CenteredWrap);
                return;
            }

            var occasion = _catalog.occasions[_selected];
            var property = _catalogObject.FindProperty(nameof(OccasionCatalog.occasions)).GetArrayElementAtIndex(_selected);
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 140;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(occasion.Title, OKStyles.Title);
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(6);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Simulate", "Open the simulator at the start of this occasion's current or next main window"), EditorStyles.miniButton, GUILayout.Width(64)))
                            SimulateOccasion(occasion);

                        GUILayout.Space(6);
                        var enabled = property.FindPropertyRelative(nameof(Occasion.enabled));
                        var next = GUILayout.Toggle(enabled.boolValue, enabled.boolValue ? " Enabled" : " Disabled", GUILayout.Width(76));

                        if (next != enabled.boolValue)
                            enabled.boolValue = next;
                    }
                }
            }

            GUILayout.Label($"{occasion.id}  ·  {_catalog.name}", OKStyles.Subtitle);
            DrawIssues(i => i.Catalog == _catalog && i.OccasionIndex == _selected);

            if (OKStyles.Section("general", "General"))
            {
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.displayName)));
                DrawIdField(property.FindPropertyRelative(nameof(Occasion.id)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.priority)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.exclusive)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.color)), new GUIContent("Timeline color"));
            }

            if (OKStyles.Section("schedule", "Schedule"))
                ScheduleGUI.Draw(property.FindPropertyRelative(nameof(Occasion.schedule)), occasion);

            if (OKStyles.Section("icon", "App icon"))
                DrawIconPicker(property.FindPropertyRelative(nameof(Occasion.iconId)));

            if (OKStyles.Section("theme", "Theme"))
            {
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.theme)));
                EditorGUILayout.LabelField(" ", "Any addressable asset (a ScriptableObject with backgrounds, skins, music…). Load it with OccasionSystem.LoadThemeAsync<T>().", OKStyles.MutedWrap);
            }

            if (OKStyles.Section("targeting", "Targeting"))
            {
                var targeting = property.FindPropertyRelative(nameof(Occasion.targeting));
                DrawStores(targeting.FindPropertyRelative(nameof(OccasionTargeting.stores)));
                DrawLanguages(targeting.FindPropertyRelative(nameof(OccasionTargeting.languages)));
            }

            if (OKStyles.Section("greeting", "Greeting", false))
            {
                var greeting = property.FindPropertyRelative(nameof(Occasion.greeting));
                var enabled = greeting.FindPropertyRelative(nameof(OccasionGreeting.enabled));
                EditorGUILayout.PropertyField(enabled, new GUIContent("Show a greeting", "Once per occurrence. Read OccasionSystem.GetPendingGreetings() and call MarkGreetingShown()."));

                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    EditorGUILayout.PropertyField(greeting.FindPropertyRelative(nameof(OccasionGreeting.title)));
                    EditorGUILayout.PropertyField(greeting.FindPropertyRelative(nameof(OccasionGreeting.message)));
#if OCCASIONKIT_LOCALIZATION
                    DrawLocalized(greeting.FindPropertyRelative(nameof(OccasionGreeting.localizedTitle)), greeting.FindPropertyRelative(nameof(OccasionGreeting.localizedMessage)), "message");
#endif
                    DrawTranslations(greeting.FindPropertyRelative(nameof(OccasionGreeting.translations)), occasion.greeting.translations, occasion, "message");
                    EditorGUILayout.PropertyField(greeting.FindPropertyRelative(nameof(OccasionGreeting.includeLeadAndTail)));
                }
            }

            if (OKStyles.Section("notification", "Notification", false))
                DrawNotification(property.FindPropertyRelative(nameof(Occasion.notification)), occasion);

            if (OKStyles.Section("notes", "Notes", false))
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(Occasion.notes)), GUIContent.none);

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private void DrawIdField(SerializedProperty id)
        {
            var next = EditorGUILayout.DelayedTextField(new GUIContent("Id", "Stable key for code, analytics and saved greetings. Don't rename an occasion that already shipped."), id.stringValue);

            if (next == id.stringValue)
                return;

            next = next.Trim();

            if (next.Length > 0 && next != id.stringValue && CatalogDatabase.GetOccasionIds().Contains(next))
                ShowNotification(new GUIContent($"Id \"{next}\" is already used"));
            else
                id.stringValue = next;
        }

        private void DrawIconPicker(SerializedProperty iconId)
        {
            var settings = Settings;
            var icons = settings.icons.Where(i => i != null && OccasionValidator.IsValidIconId(i.id)).ToList();
            var options = new[] { "None (doesn't change the icon)" }.Concat(icons.Select(i => i.Title == i.id ? i.id : $"{i.Title}  ({i.id})")).ToList();
            var index = string.IsNullOrEmpty(iconId.stringValue) ? 0 : icons.FindIndex(i => i.id == iconId.stringValue) + 1;
            var missing = index == 0 && !string.IsNullOrEmpty(iconId.stringValue);

            if (missing)
            {
                options.Add($"Missing: {iconId.stringValue}");
                index = options.Count - 1;
            }

            var next = EditorGUILayout.Popup(new GUIContent("Launcher icon", "Built into the app. When several active occasions have an icon, the highest priority wins."), index, options.ToArray());

            if (next != index)
                iconId.stringValue = next == 0 ? "" : next <= icons.Count ? icons[next - 1].id : iconId.stringValue;

            var variant = settings.FindIcon(iconId.stringValue);

            if (variant != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth + 2);

                    foreach (var shape in new[] { MaskShape.Circle, MaskShape.Squircle, MaskShape.RoundedSquare, MaskShape.Teardrop })
                    {
                        var rect = GUILayoutUtility.GetRect(52, 52, GUILayout.Width(52));
                        var preview = IconPreviewCache.Get(variant, 104, shape);

                        if (preview != null)
                            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Edit icon", GUILayout.Width(80)))
                    {
                        _tab = Tab.Icons;
                        _selectedIcon = settings.icons.IndexOf(variant);
                    }
                }
            }
            else if (icons.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(" ", "The icon set is empty.", OKStyles.Muted);

                    if (GUILayout.Button("Add icons", GUILayout.Width(80)))
                        _tab = Tab.Icons;
                }
            }
        }

        private void DrawStores(SerializedProperty stores)
        {
            var known = Settings.stores.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var selected = Enumerable.Range(0, stores.arraySize).Select(i => stores.GetArrayElementAtIndex(i).stringValue).ToList();

            EditorGUILayout.LabelField(new GUIContent("Stores", "Matched against OccasionConfig.Store. None selected: every store."), new GUIContent(selected.Count == 0 ? "All stores" : string.Join(", ", selected)));

            if (known.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(" ", "No store names yet.", OKStyles.Muted);

                    if (GUILayout.Button("Add in Settings", GUILayout.Width(110)))
                        _tab = Tab.Settings;
                }

                return;
            }

            var all = known.Concat(selected.Where(s => !known.Contains(s, StringComparer.OrdinalIgnoreCase))).ToList();
            const int perRow = 4;

            for (var row = 0; row < all.Count; row += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth + 2);

                    for (var i = row; i < Mathf.Min(all.Count, row + perRow); i++)
                    {
                        var store = all[i];
                        var isKnown = known.Contains(store, StringComparer.OrdinalIgnoreCase);
                        var index = selected.FindIndex(s => string.Equals(s, store, StringComparison.OrdinalIgnoreCase));
                        var previous = GUI.backgroundColor;

                        if (!isKnown)
                            GUI.backgroundColor = OKStyles.Warning;

                        var on = GUILayout.Toggle(index >= 0, new GUIContent(store, isKnown ? null : "Not in the settings' store list"), EditorStyles.miniButton, GUILayout.MinWidth(70));
                        GUI.backgroundColor = previous;

                        if (on && index < 0)
                        {
                            stores.arraySize++;
                            stores.GetArrayElementAtIndex(stores.arraySize - 1).stringValue = store;
                        }
                        else if (!on && index >= 0)
                        {
                            stores.DeleteArrayElementAtIndex(index);
                            stores.serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawLanguages(SerializedProperty languages)
        {
            var current = string.Join(", ", Enumerable.Range(0, languages.arraySize).Select(i => languages.GetArrayElementAtIndex(i).stringValue));
            var next = EditorGUILayout.DelayedTextField(new GUIContent("Languages", "ISO 639-1 codes, comma-separated: \"fa, en\". \"fa\" also matches \"fa-IR\". Empty: every language. Matched against OccasionConfig.Language (default: the device language)."), current);

            if (next != current)
            {
                var codes = next.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(LanguageCodes.Normalize).Distinct().ToList();
                languages.arraySize = codes.Count;

                for (var i = 0; i < codes.Count; i++)
                    languages.GetArrayElementAtIndex(i).stringValue = codes[i];
            }

            if (languages.arraySize == 0)
                EditorGUILayout.LabelField(" ", "Every language.", OKStyles.Muted);
        }

        private void DrawNotification(SerializedProperty notification, Occasion occasion)
        {
            var enabled = notification.FindPropertyRelative(nameof(OccasionNotification.enabled));
            EditorGUILayout.PropertyField(enabled, new GUIContent("Send a notification", "Local notification, scheduled each time the app goes to the background. Needs com.unity.mobile.notifications and OccasionNotifications.Enable()."));

            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                EditorGUILayout.PropertyField(notification.FindPropertyRelative(nameof(OccasionNotification.title)));
                EditorGUILayout.PropertyField(notification.FindPropertyRelative(nameof(OccasionNotification.body)));
#if OCCASIONKIT_LOCALIZATION
                DrawLocalized(notification.FindPropertyRelative(nameof(OccasionNotification.localizedTitle)), notification.FindPropertyRelative(nameof(OccasionNotification.localizedBody)), "body");
#endif
                DrawTranslations(notification.FindPropertyRelative(nameof(OccasionNotification.translations)), occasion.notification.translations, occasion, "body");
                EditorGUILayout.PropertyField(notification.FindPropertyRelative(nameof(OccasionNotification.anchor)), new GUIContent("Fires at"));
                EditorGUILayout.PropertyField(notification.FindPropertyRelative(nameof(OccasionNotification.offsetMinutes)), new GUIContent("Offset (minutes)"));
            }

#if !OCCASIONKIT_NOTIFICATIONS
            if (enabled.boolValue)
                EditorGUILayout.HelpBox("com.unity.mobile.notifications isn't installed, so this won't be sent.", MessageType.Info);
#endif
        }

#if OCCASIONKIT_LOCALIZATION
        /// <summary>Unity Localization strings. They come before the translations and the default text.</summary>
        /// <param name="text">"message" or "body".</param>
        private static void DrawLocalized(SerializedProperty title, SerializedProperty body, string text)
        {
            EditorGUILayout.PropertyField(title, new GUIContent("Localized title", "From a Unity Localization string table, in the selected locale. Empty: the title above."));
            EditorGUILayout.PropertyField(body, new GUIContent("Localized " + text, $"From a Unity Localization string table, in the selected locale. Empty: the {text} above."));
            EditorGUILayout.LabelField(" ", $"Set strings replace the title and {text} above in every locale.", OKStyles.MutedWrap);
        }
#endif

        private static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>();

        /// <summary>
        /// The title and text per language code. With Unity Localization installed the localized strings take over, so
        /// the list only shows when it already has entries.
        /// </summary>
        /// <param name="text">"message" or "body".</param>
        private void DrawTranslations(SerializedProperty translations, List<OccasionTranslation> list, Occasion occasion, string text)
        {
#if OCCASIONKIT_LOCALIZATION
            if (translations.arraySize == 0)
                return;
#endif
            var label = new GUIContent("Translations", $"The title and {text} per language, matched against OccasionConfig.Language (default: the device language). \"fa\" also covers \"fa-IR\". Empty fields use the default text.");

            if (translations.arraySize > 0)
                EditorGUILayout.LabelField(label, GUIContent.none);

            for (var i = 0; i < translations.arraySize; i++)
            {
                var entry = translations.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Language");
                        var code = entry.FindPropertyRelative(nameof(OccasionTranslation.language));
                        var next = EditorGUILayout.DelayedTextField(code.stringValue, GUILayout.Width(70));

                        if (next != code.stringValue)
                        {
                            next = LanguageCodes.Normalize(next);

                            if (next != code.stringValue)
                                code.stringValue = next;
                        }

                        GUILayout.Label(LanguageName(code.stringValue), OKStyles.Muted);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image, "Remove this translation"), EditorStyles.iconButton, GUILayout.Width(20)))
                        {
                            translations.DeleteArrayElementAtIndex(i);
                            translations.serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(nameof(OccasionTranslation.title)));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(nameof(OccasionTranslation.text)), new GUIContent(char.ToUpperInvariant(text[0]) + text.Substring(1)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(translations.arraySize == 0 ? label : new GUIContent(" "));
                var add = new GUIContent("Add language");
                var rect = GUILayoutUtility.GetRect(add, EditorStyles.popup, GUILayout.Width(110));

                if (EditorGUI.DropdownButton(rect, add, FocusType.Passive))
                    ShowAddTranslationMenu(rect, list, occasion);

                if (translations.arraySize == 0)
                    GUILayout.Label("None: every language gets the text above.", OKStyles.Muted);

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Suggests the occasion's targeted languages and the ones other occasions in the catalog are translated to.</summary>
        private void ShowAddTranslationMenu(Rect rect, List<OccasionTranslation> list, Occasion occasion)
        {
            var taken = new HashSet<string>(list.Where(t => t != null).Select(t => LanguageCodes.Normalize(t.language)));
            var used = _catalog.occasions.Where(o => o != null)
                .SelectMany(o => o.greeting.translations.Concat(o.notification.translations))
                .Where(t => t != null)
                .Select(t => t.language);

            var suggestions = occasion.targeting.languages.Concat(used)
                .Select(LanguageCodes.Normalize)
                .Where(code => code.Length > 0 && !taken.Contains(code))
                .Distinct()
                .ToList();

            if (suggestions.Count == 0)
            {
                AddTranslation(list, "");
                return;
            }

            var menu = new GenericMenu();

            foreach (var code in suggestions)
                menu.AddItem(new GUIContent($"{code}   {LanguageName(code)}"), false, () => AddTranslation(list, code));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Other (type the code)"), false, () => AddTranslation(list, ""));
            menu.DropDown(rect);
        }

        private void AddTranslation(List<OccasionTranslation> list, string code)
        {
            ModifyCatalog("Add translation", () => list.Add(new OccasionTranslation { language = code }));
            Repaint();
        }

        /// <summary>"Persian", "Persian (Iran)", or a hint when the code is empty or unknown.</summary>
        private static string LanguageName(string code)
        {
            code = LanguageCodes.Normalize(code);

            if (code.Length == 0)
                return "Type an ISO code, such as fa";

            if (!LanguageNames.TryGetValue(code, out var name))
            {
                try
                {
                    name = CultureInfo.GetCultureInfo(code).EnglishName;
                }
                catch (ArgumentException)
                {
                    name = "Unknown code";
                }

                LanguageNames[code] = name;
            }

            return name;
        }

        private void DrawIssues(Func<ValidationIssue, bool> filter)
        {
            foreach (var issue in _issues.Where(filter).Where(i => i.Severity != IssueSeverity.Info || _tab == Tab.Validation))
                DrawIssue(issue, false);
        }

        private void DrawIssue(ValidationIssue issue, bool showNavigate)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(OKStyles.SeverityIcon(issue.Severity), GUILayout.Width(20), GUILayout.Height(18));
                GUILayout.Label(issue.Message, EditorStyles.wordWrappedMiniLabel);

                if (showNavigate && (issue.OccasionIndex >= 0 || issue.IconIndex >= 0 || issue.Catalog != null) && GUILayout.Button("Show", EditorStyles.miniButton, GUILayout.Width(46)))
                    Navigate(issue);

                if (issue.Fix != null && GUILayout.Button(issue.FixLabel ?? "Fix", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    issue.Fix(GUILayoutUtility.GetLastRect());
                    _nextValidation = 0;
                    _catalogObject?.Update();
                    _settingsObject?.Update();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void Navigate(ValidationIssue issue)
        {
            if (issue.IconIndex >= 0)
            {
                _tab = Tab.Icons;
                _selectedIcon = issue.IconIndex;
            }
            else if (issue.OccasionIndex >= 0 && issue.Catalog != null)
            {
                _tab = Tab.Occasions;
                SelectCatalog(issue.Catalog);
                SelectOccasion(issue.OccasionIndex);
            }
            else if (issue.Catalog != null)
            {
                Selection.activeObject = issue.Catalog;
                EditorGUIUtility.PingObject(issue.Catalog);
            }

            GUI.FocusControl(null);
        }

        private void DrawEmpty(string title, string body, string button, Action action)
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(400)))
                {
                    GUILayout.Label(title, OKStyles.Title);
                    GUILayout.Label(body, OKStyles.CenteredWrap);
                    GUILayout.Space(8);

                    if (button != null && GUILayout.Button(button, GUILayout.Height(26)))
                        action();
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
        }
    }
}
