using System;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    [CustomEditor(typeof(OccasionCatalog))]
    internal sealed class OccasionCatalogEditor : UnityEditor.Editor
    {
        private readonly TimelineView _timeline = new TimelineView();
        private bool _showRaw;

        public override bool RequiresConstantRepaint() => false;

        public override void OnInspectorGUI()
        {
            var catalog = (OccasionCatalog)target;
            var settings = OccasionKitSettings.Instance;
            var label = settings != null ? settings.catalogLabel : OccasionSystem.DefaultCatalogLabel;

            GUILayout.Label($"{catalog.occasions.Count} occasions", OKStyles.Title);
            GUILayout.Label(AddressablesHelper.Describe(catalog, label), OKStyles.Subtitle);

            if (GUILayout.Button("Open in OccasionKit", GUILayout.Height(26)))
                OccasionKitWindow.Open(catalog);

            GUILayout.Space(6);
            var calendar = EditorBridge.TimelineCalendar;
            var year = CalendarUtility.GetYear(calendar, DateTime.Now);
            var rect = GUILayoutUtility.GetRect(10, TimelineView.GetHeight(catalog.occasions.Count), GUILayout.ExpandWidth(true));
            var clicked = _timeline.Draw(rect, calendar, year, catalog.occasions, -1, null);

            if (clicked >= 0)
                OccasionKitWindow.Open(catalog, clicked);

            if (Event.current.type == EventType.MouseMove)
                Repaint();

            GUILayout.Space(6);
            _showRaw = EditorGUILayout.Foldout(_showRaw, "Raw data", true);

            if (_showRaw)
                DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(OccasionKitSettings))]
    internal sealed class OccasionKitSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GUILayout.Label("OccasionKit settings", OKStyles.Title);
            GUILayout.Label("Icon set and store names. Editor only; never shipped.", OKStyles.Subtitle);

            if (GUILayout.Button("Open OccasionKit", GUILayout.Height(26)))
                OccasionKitWindow.OpenIcons();

            GUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
