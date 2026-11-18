using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal static class OKStyles
    {
        private static GUIStyle _title;
        private static GUIStyle _subtitle;
        private static GUIStyle _sectionHeader;
        private static GUIStyle _muted;
        private static GUIStyle _mutedWrap;
        private static GUIStyle _rowTitle;
        private static GUIStyle _rowLabel;
        private static GUIStyle _monthLabel;
        private static GUIStyle _barLabel;
        private static GUIStyle _tooltip;
        private static GUIStyle _badge;
        private static GUIStyle _centeredWrap;
        private static GUIStyle _code;
        private static Texture2D _stripes;

        private static bool Pro { get => EditorGUIUtility.isProSkin; }

        public static GUIStyle Title { get => _title ?? (_title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, margin = new RectOffset(4, 4, 6, 2) }); }
        public static GUIStyle Subtitle { get => _subtitle ?? (_subtitle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = MutedText } }); }
        public static GUIStyle SectionHeader { get => _sectionHeader ?? (_sectionHeader = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold }); }
        public static GUIStyle Muted { get => _muted ?? (_muted = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = MutedText } }); }
        public static GUIStyle MutedWrap { get => _mutedWrap ?? (_mutedWrap = new GUIStyle(Muted) { wordWrap = true }); }
        public static GUIStyle RowTitle { get => _rowTitle ?? (_rowTitle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip }); }
        public static GUIStyle RowLabel { get => _rowLabel ?? (_rowLabel = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, fontSize = 11 }); }
        public static GUIStyle MonthLabel { get => _monthLabel ?? (_monthLabel = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, normal = { textColor = MutedText } }); }
        public static GUIStyle BarLabel { get => _barLabel ?? (_barLabel = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, padding = new RectOffset(3, 2, 0, 0) }); }
        public static GUIStyle Tooltip { get => _tooltip ?? (_tooltip = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, richText = true, wordWrap = true, padding = new RectOffset(8, 8, 6, 6) }); }
        public static GUIStyle Badge { get => _badge ?? (_badge = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset(4, 4, 0, 0) }); }
        public static GUIStyle CenteredWrap { get => _centeredWrap ?? (_centeredWrap = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter }); }
        public static GUIStyle Code { get => _code ?? (_code = new GUIStyle(EditorStyles.textArea) { font = EditorStyles.standardFont, fontSize = 11, wordWrap = false, richText = false }); }

        /// <summary>Forms stop growing past this width so labels and their fields stay close on wide windows.</summary>
        public const float FormMaxWidth = 720f;

        public static Color MutedText { get => Pro ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.38f, 0.38f, 0.38f); }
        public static Color SectionBand { get => Pro ? new Color(1, 1, 1, 0.045f) : new Color(0, 0, 0, 0.055f); }
        public static Color Hover { get => Pro ? new Color(1, 1, 1, 0.035f) : new Color(0, 0, 0, 0.04f); }
        public static Color BarOutline { get => Pro ? new Color(1, 1, 1, 0.45f) : new Color(0, 0, 0, 0.45f); }
        public static Color TimelineBackground { get => Pro ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.84f, 0.84f, 0.84f); }
        public static Color LabelColumn { get => Pro ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.78f, 0.78f, 0.78f); }
        public static Color MonthShade { get => Pro ? new Color(1, 1, 1, 0.035f) : new Color(0, 0, 0, 0.045f); }
        public static Color GridLine { get => Pro ? new Color(1, 1, 1, 0.08f) : new Color(0, 0, 0, 0.13f); }
        public static Color SecondaryLine { get => Pro ? new Color(0.55f, 0.8f, 0.6f, 0.22f) : new Color(0.1f, 0.45f, 0.2f, 0.3f); }
        public static Color Selection { get => Pro ? new Color(0.24f, 0.49f, 0.91f, 0.22f) : new Color(0.24f, 0.49f, 0.91f, 0.18f); }
        public static Color Disabled { get => Pro ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f); }
        public static readonly Color Today = new Color(0.95f, 0.33f, 0.31f);
        public static readonly Color Simulated = new Color(0.2f, 0.82f, 0.95f);
        public static readonly Color Active = new Color(0.34f, 0.8f, 0.45f);
        public static readonly Color Warning = new Color(1f, 0.76f, 0.2f);
        public static readonly Color Error = new Color(0.95f, 0.35f, 0.3f);

        /// <summary>Diagonal stripes for exclusive occasions.</summary>
        public static Texture2D Stripes
        {
            get
            {
                if (_stripes != null)
                    return _stripes;

                const int size = 8;
                _stripes = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                        _stripes.SetPixel(x, y, (x + y) % size < 3 ? new Color(0, 0, 0, 0.38f) : Color.clear);
                }

                _stripes.Apply();
                return _stripes;
            }
        }

        public static Color ContrastText(Color background)
        {
            return Luminance(background) > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        public static void Line(float thickness = 1f, float padding = 4f)
        {
            var rect = GUILayoutUtility.GetRect(1, thickness + padding * 2, GUILayout.ExpandWidth(true));
            rect.y += padding;
            rect.height = thickness;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, GridLine);
        }

        /// <summary>A shaded, bold foldout header whose state is remembered for the editor session.</summary>
        public static bool Section(string key, string title, bool defaultOpen = true)
        {
            var sessionKey = "OccasionKit.Section." + key;
            var open = SessionState.GetBool(sessionKey, defaultOpen);
            GUILayout.Space(6);
            var rect = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, SectionBand);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), GridLine);
            }

            var next = EditorGUI.Foldout(new Rect(rect.x + 4, rect.y + 1, rect.width - 4, rect.height - 2), open, title, true, SectionHeader);

            if (next != open)
                SessionState.SetBool(sessionKey, next);

            GUILayout.Space(2);
            return next;
        }

        /// <summary>Starts a column capped at <see cref="FormMaxWidth"/>; pair with <see cref="EndForm"/>.</summary>
        public static void BeginForm(float maxWidth = FormMaxWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
        }

        public static void EndForm()
        {
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        public static float Luminance(Color color) => 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;

        public static void ColorChip(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, color);
        }

        public static GUIContent SeverityIcon(IssueSeverity severity)
        {
            switch (severity)
            {
                case IssueSeverity.Error: return EditorGUIUtility.IconContent("console.erroricon.sml");
                case IssueSeverity.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                default: return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }
    }
}
