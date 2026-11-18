using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>Window > OccasionKit: occasions and their timeline, the icon set, a simulator, validation and settings.</summary>
    internal sealed partial class OccasionKitWindow : EditorWindow
    {
        private enum Tab
        {
            Occasions = 0,
            Icons = 1,
            Simulator = 2,
            Validation = 3,
            Settings = 4,
        }

        [SerializeField] private Tab _tab;
        [SerializeField] private OccasionCatalog _catalog;
        [SerializeField] private int _selected = -1;
        [SerializeField] private int _selectedIcon = -1;
        [SerializeField] private int _year;
        [SerializeField] private CalendarType _timelineCalendar;

        private SerializedObject _catalogObject;
        private SerializedObject _settingsObject;
        private List<ValidationIssue> _issues = new List<ValidationIssue>();
        private double _nextValidation;
        private int _errorCount;
        private int _warningCount;

        [MenuItem("Window/OccasionKit", priority = 2100)]
        public static OccasionKitWindow Open()
        {
            var window = GetWindow<OccasionKitWindow>();
            window.titleContent = new GUIContent("OccasionKit", EditorGUIUtility.IconContent("d_UnityEditor.AnimationWindow").image);
            window.minSize = new Vector2(780, 500);
            window.Show();
            return window;
        }

        public static void Open(OccasionCatalog catalog, int index = -1)
        {
            var window = Open();
            window._tab = Tab.Occasions;
            window.SelectCatalog(catalog);

            if (index >= 0)
                window.SelectOccasion(index);
        }

        public static void OpenIcons()
        {
            Open()._tab = Tab.Icons;
        }

        private OccasionKitSettings Settings { get => OccasionKitSettings.Instance; }

        private void OnEnable()
        {
            wantsMouseMove = true;

            if (_year == 0)
                _year = CalendarUtility.GetYear(_timelineCalendar, DateTime.Now);

            EditorBridge.TimelineCalendar = _timelineCalendar;

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _nextValidation = 0;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            OccasionSystem.Changed -= Repaint;
            IconPreviewCache.Clear();
        }

        private void OnFocus()
        {
            CatalogDatabase.Invalidate();
            _nextValidation = 0;
        }

        private void OnUndoRedo()
        {
            _catalogObject?.Update();
            _settingsObject?.Update();
            _nextValidation = 0;
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            OccasionSystem.Changed -= Repaint;

            if (change == PlayModeStateChange.EnteredPlayMode)
                OccasionSystem.Changed += Repaint;

            Repaint();
        }

        private void Update()
        {
            // Countdowns and runtime state in the simulator.
            if (_tab == Tab.Simulator && EditorApplication.isPlaying && EditorApplication.timeSinceStartup > _nextSimulatorRepaint)
            {
                _nextSimulatorRepaint = EditorApplication.timeSinceStartup + 0.5;
                Repaint();
            }
        }

        private double _nextSimulatorRepaint;

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            EnsureCatalog();
            EnsureSerializedObjects();
            ValidateIfDue();

            DrawToolbar();

            if (Settings == null && _tab != Tab.Validation)
            {
                DrawWelcome();
                return;
            }

            switch (_tab)
            {
                case Tab.Occasions: DrawOccasionsTab(); break;
                case Tab.Icons: DrawIconsTab(); break;
                case Tab.Simulator: DrawSimulatorTab(); break;
                case Tab.Validation: DrawValidationTab(); break;
                case Tab.Settings: DrawSettingsTab(); break;
            }

            _catalogObject?.ApplyModifiedProperties();
            _settingsObject?.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var validation = "Validation";

                if (_errorCount > 0)
                    validation += $" ({_errorCount})";
                else if (_warningCount > 0)
                    validation += $" ({_warningCount})";

                var labels = new[]
                {
                    new GUIContent("Occasions"),
                    new GUIContent("Icons"),
                    new GUIContent("Simulator"),
                    new GUIContent(validation, _errorCount > 0 ? OKStyles.SeverityIcon(IssueSeverity.Error).image : _warningCount > 0 ? OKStyles.SeverityIcon(IssueSeverity.Warning).image : null),
                    new GUIContent("Settings"),
                };

                _tab = (Tab)GUILayout.Toolbar((int)_tab, labels, EditorStyles.toolbarButton, GUILayout.Width(480));
                GUILayout.FlexibleSpace();

                if (_tab == Tab.Occasions && Settings != null)
                    DrawCatalogPicker();
            }
        }

        private void DrawCatalogPicker()
        {
            var catalogs = CatalogDatabase.All;

            if (catalogs.Count > 0)
            {
                var index = Mathf.Max(0, catalogs.ToList().IndexOf(_catalog));
                var names = catalogs.Select(c => c.name).ToArray();
                var next = EditorGUILayout.Popup(index, names, EditorStyles.toolbarPopup, GUILayout.Width(180));

                if (next != index)
                    SelectCatalog(catalogs[next]);

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Search Icon").image, "Select the catalog asset"), EditorStyles.toolbarButton, GUILayout.Width(26)) && _catalog != null)
                {
                    Selection.activeObject = _catalog;
                    EditorGUIUtility.PingObject(_catalog);
                }
            }

            if (GUILayout.Button(new GUIContent("New Catalog", "Create another OccasionCatalog asset"), EditorStyles.toolbarButton, GUILayout.Width(84)))
                CreateCatalog();
        }

        private void DrawWelcome()
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(420)))
                {
                    GUILayout.Label("OccasionKit", OKStyles.Title);
                    GUILayout.Label("Seasonal occasions with their own launcher icon, theme, greeting and notification. Start by creating the project settings; they hold the icon set and store names and never ship in the app.", OKStyles.CenteredWrap);
                    GUILayout.Space(10);

                    if (GUILayout.Button("Create settings", GUILayout.Height(28)))
                    {
                        Selection.activeObject = OccasionKitSettings.Create();
                        _nextValidation = 0;
                    }

                    GUILayout.Label(OccasionKitSettings.DefaultPath, OKStyles.Muted);
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
        }

        private void EnsureCatalog()
        {
            if (_catalog == null && CatalogDatabase.All.Count > 0)
                SelectCatalog(CatalogDatabase.All[0]);
        }

        private void SelectCatalog(OccasionCatalog catalog)
        {
            if (_catalog == catalog)
                return;

            _catalog = catalog;
            _catalogObject = null;
            _selected = catalog != null && catalog.occasions.Count > 0 ? 0 : -1;
            _occasionList = null;
        }

        private void EnsureSerializedObjects()
        {
            if (_catalog != null && (_catalogObject == null || _catalogObject.targetObject != _catalog))
            {
                _catalogObject = new SerializedObject(_catalog);
                _occasionList = null;
            }
            else if (_catalog == null)
            {
                _catalogObject = null;
            }

            var settings = Settings;

            if (settings != null && (_settingsObject == null || _settingsObject.targetObject != settings))
            {
                _settingsObject = new SerializedObject(settings);
                _iconList = null;
                _storeList = null;
            }
            else if (settings == null)
            {
                _settingsObject = null;
            }

            _catalogObject?.Update();
            _settingsObject?.Update();
        }

        private void ValidateIfDue()
        {
            if (Event.current.type != EventType.Layout || EditorApplication.timeSinceStartup < _nextValidation)
                return;

            _nextValidation = EditorApplication.timeSinceStartup + 1.5;
            _issues = OccasionValidator.Run(Settings, CatalogDatabase.All);
            _errorCount = _issues.Count(i => i.Severity == IssueSeverity.Error);
            _warningCount = _issues.Count(i => i.Severity == IssueSeverity.Warning);
        }

        /// <summary>Changes the catalog directly (with undo), keeping the SerializedObject in sync.</summary>
        private void ModifyCatalog(string undoName, Action action)
        {
            if (_catalog == null)
                return;

            _catalogObject?.ApplyModifiedProperties();
            Undo.RecordObject(_catalog, undoName);
            action();
            EditorUtility.SetDirty(_catalog);
            _catalogObject?.Update();
            _nextValidation = 0;
        }

        private void ModifySettings(string undoName, Action action)
        {
            var settings = Settings;

            if (settings == null)
                return;

            _settingsObject?.ApplyModifiedProperties();
            Undo.RecordObject(settings, undoName);
            action();
            EditorUtility.SetDirty(settings);
            _settingsObject?.Update();
            _nextValidation = 0;
        }

        private void CreateCatalog()
        {
            var folder = _catalog != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(_catalog)) : Settings != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(Settings)) : "Assets";
            var path = EditorUtility.SaveFilePanelInProject("New occasion catalog", "OccasionCatalog", "asset", "Catalogs with the OccasionKit label are loaded and merged at runtime.", folder);

            if (string.IsNullOrEmpty(path))
                return;

            var catalog = CreateInstance<OccasionCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            CatalogDatabase.Invalidate();
            SelectCatalog(catalog);
            _nextValidation = 0;
        }

        private static void SectionGap() => GUILayout.Space(2);
    }
}
