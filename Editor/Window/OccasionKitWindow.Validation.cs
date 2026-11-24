using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal sealed partial class OccasionKitWindow
    {
        private const string IntegrationSnippet =
@"using OccasionKit;

// Once at startup, after Addressables catalogs are updated.
await OccasionSystem.InitializeAsync(new OccasionConfig { Store = ""GooglePlay"" });

// React to occasions starting and ending (also raised once after init).
OccasionSystem.Changed += () => snow.SetActive(OccasionSystem.IsActive(""yalda""));

// Themed content of the highest-priority active occasion.
var active = OccasionSystem.GetActiveTheme();
if (active != null)
{
    var handle = OccasionSystem.LoadThemeAsync<MyTheme>(active.Occasion);
    // ... Addressables.Release(handle) when done
}

// Greetings, once per occurrence.
foreach (var greeting in OccasionSystem.GetPendingGreetings())
{
    var (title, message) = OccasionSystem.GetGreetingText(greeting);   // in the player's language
    ShowPopup(title, message);
    OccasionSystem.MarkGreetingShown(greeting);
}

// Countdown banner: ""Nowruz in 2d 4h"".
var nowruz = OccasionSystem.GetActive(""nowruz"");
var left = nowruz?.GetCountdown(OccasionClock.Now);

// Player setting.
OccasionSystem.SeasonalIconsEnabled = toggle.isOn;

// Optional, with com.unity.mobile.notifications installed.
OccasionKit.Notifications.OccasionNotifications.Enable(""Events"");";

        [SerializeField] private Vector2 _validationScroll;
        [SerializeField] private Vector2 _settingsScroll;

        private ReorderableList _storeList;

        private void DrawValidationTab()
        {
            var infoCount = _issues.Count(i => i.Severity == IssueSeverity.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                SeverityFilter(IssueSeverity.Error, _errorCount, "Errors");
                SeverityFilter(IssueSeverity.Warning, _warningCount, "Warnings");
                SeverityFilter(IssueSeverity.Info, infoCount, "Notes");
                GUILayout.Space(8);
                GUILayout.Label("Icon errors fail Android and iOS builds. Catalog problems don't block a build, but the runtime skips what it can't use.", OKStyles.Muted);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Check again", "Rescan catalogs and settings now"), EditorStyles.toolbarButton, GUILayout.Width(84)))
                {
                    CatalogDatabase.Invalidate();
                    _nextValidation = 0;
                }
            }

            if (_issues.Count == 0)
            {
                DrawEmpty("Everything checks out", "No errors, warnings or notes in the settings or any catalog.", null, null);
                return;
            }

            var shown = _issues.Where(i => ShowSeverity(i.Severity)).OrderByDescending(i => i.Severity).ToList();
            _validationScroll = EditorGUILayout.BeginScrollView(_validationScroll);
            GUILayout.Space(2);

            if (shown.Count == 0)
                GUILayout.Label($"{_issues.Count} hidden by the filters above.", OKStyles.Muted);

            foreach (var issue in shown)
                DrawIssue(issue, true);

            EditorGUILayout.EndScrollView();
        }

        private static string SeverityKey(IssueSeverity severity) => "OccasionKit.Validation.Show." + severity;

        private static bool ShowSeverity(IssueSeverity severity) => SessionState.GetBool(SeverityKey(severity), true);

        private static void SeverityFilter(IssueSeverity severity, int count, string label)
        {
            var on = ShowSeverity(severity);
            var content = new GUIContent($" {count}", OKStyles.SeverityIcon(severity).image, $"Show {label.ToLowerInvariant()}");
            var next = GUILayout.Toggle(on, content, EditorStyles.toolbarButton, GUILayout.MinWidth(40));

            if (next != on)
                SessionState.SetBool(SeverityKey(severity), next);
        }

        private void DrawSettingsTab()
        {
            var settings = Settings;
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
            OKStyles.BeginForm();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 140;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Settings", OKStyles.Title);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }

            GUILayout.Label(AssetDatabase.GetAssetPath(settings) + "  ·  editor only, never shipped", OKStyles.Subtitle);

            if (OKStyles.Section("settings.general", "Catalog loading"))
            {
                EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(OccasionKitSettings.catalogLabel)), new GUIContent("Addressables label"));
                EditorGUILayout.LabelField(" ", "Pass the same key as OccasionConfig.CatalogKey if you change it.", OKStyles.Muted);
            }

            if (OKStyles.Section("settings.stores", "Stores"))
            {
                GUILayout.Label("Names for store targeting. At runtime pass the current one as OccasionConfig.Store (case-insensitive).", OKStyles.MutedWrap);
                GetStoreList().DoLayoutList();
            }

            if (OKStyles.Section("settings.catalogs", "Catalogs"))
                DrawCatalogs(settings);

            if (OKStyles.Section("settings.android", "Android"))
            {
                GUILayout.Label(
                    "Each Android build adds one <activity-alias> per icon and moves the launcher entry from the main activity to them; only \"default\" starts enabled. " +
                    "The icon switches when the app goes to the background, and upcoming changes are scheduled with AlarmManager so the icon also changes while the app is closed. " +
                    "Some launchers drop home-screen shortcuts when the alias changes; the app drawer entry always follows.",
                    OKStyles.MutedWrap);
                EditorGUILayout.LabelField("Min API level", $"{(int)PlayerSettings.Android.minSdkVersion}{((int)PlayerSettings.Android.minSdkVersion >= 26 ? "  (adaptive icons only)" : "  (legacy PNGs are built too)")}");
                EditorGUILayout.LabelField("Released icons", $"{ReleasedIconRegistry.LoadCached().Count} in {ReleasedIconRegistry.FilePath}");
            }

            if (OKStyles.Section("settings.ios", "iOS"))
            {
                GUILayout.Label(
                    "Each iOS build adds one alternate app icon per icon to the Xcode project's asset catalog (Xcode 14 or newer); the primary icon stays the Player Settings icon. " +
                    "iOS only lets an app change its icon while it's open, and shows an alert each time, so the icon switches shortly after launch or when the player comes back to the app. " +
                    "It can't change while the app is closed.",
                    OKStyles.MutedWrap);
            }

            if (OKStyles.Section("settings.integration", "Integration", false))
            {
                var height = OKStyles.Code.CalcHeight(new GUIContent(IntegrationSnippet), Mathf.Min(position.width, OKStyles.FormMaxWidth) - 40);
                EditorGUILayout.SelectableLabel(IntegrationSnippet, OKStyles.Code, GUILayout.Height(height));

                if (GUILayout.Button("Copy", GUILayout.Width(80)))
                {
                    EditorGUIUtility.systemCopyBuffer = IntegrationSnippet;
                    ShowNotification(new GUIContent("Copied"));
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
            OKStyles.EndForm();
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private ReorderableList GetStoreList()
        {
            if (_storeList == null || _storeList.serializedProperty.serializedObject != _settingsObject)
            {
                _storeList = new ReorderableList(_settingsObject, _settingsObject.FindProperty(nameof(OccasionKitSettings.stores)), true, false, true, true)
                {
                    elementHeight = EditorGUIUtility.singleLineHeight + 4,
                    drawElementCallback = (rect, index, active, focused) =>
                    {
                        rect.y += 2;
                        rect.height = EditorGUIUtility.singleLineHeight;
                        var element = _storeList.serializedProperty.GetArrayElementAtIndex(index);
                        element.stringValue = EditorGUI.DelayedTextField(rect, element.stringValue);
                    },
                    onAddCallback = list =>
                    {
                        var property = list.serializedProperty;
                        property.arraySize++;
                        property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = "Store";
                    },
                };
            }

            return _storeList;
        }

        private void DrawCatalogs(OccasionKitSettings settings)
        {
            var catalogs = CatalogDatabase.All;

            if (catalogs.Count == 0)
                GUILayout.Label("No catalogs yet.", OKStyles.Muted);

            foreach (var catalog in catalogs)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label($"{catalog.name}  ·  {catalog.occasions.Count} occasions", EditorStyles.boldLabel);
                        GUILayout.Label(AddressablesHelper.Describe(catalog, settings.catalogLabel), OKStyles.Muted);
                    }

                    GUILayout.FlexibleSpace();

                    var entry = AddressablesHelper.GetEntry(catalog);

                    if (entry == null)
                    {
                        var rect = GUILayoutUtility.GetRect(new GUIContent("Make addressable"), EditorStyles.miniPullDown, GUILayout.Width(120));

                        if (EditorGUI.DropdownButton(rect, new GUIContent("Make addressable"), FocusType.Passive, EditorStyles.miniPullDown))
                            AddressablesHelper.ShowMakeAddressableMenu(catalog, settings.catalogLabel);
                    }
                    else if (!AddressablesHelper.HasLabel(entry, settings.catalogLabel) && GUILayout.Button("Add label", EditorStyles.miniButton, GUILayout.Width(80)))
                    {
                        AddressablesHelper.AddLabel(entry, settings.catalogLabel);
                        _nextValidation = 0;
                    }

                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        _tab = Tab.Occasions;
                        SelectCatalog(catalog);
                    }
                }
            }

            if (GUILayout.Button("New catalog", GUILayout.Width(110)))
                CreateCatalog();
        }
    }
}
