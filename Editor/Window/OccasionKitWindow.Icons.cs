using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal sealed partial class OccasionKitWindow
    {
        private static readonly (MaskShape shape, string label)[] PreviewShapes =
        {
            (MaskShape.Circle, "Circle"),
            (MaskShape.Squircle, "Squircle"),
            (MaskShape.RoundedSquare, "Rounded"),
            (MaskShape.Teardrop, "Teardrop"),
        };

        [SerializeField] private Vector2 _iconListScroll;
        [SerializeField] private Vector2 _iconDetailScroll;
        [SerializeField] private bool _showSafeZone = true;

        private ReorderableList _iconList;

        private void DrawIconsTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    _iconListScroll = EditorGUILayout.BeginScrollView(_iconListScroll);

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label("default", EditorStyles.boldLabel);
                        GUILayout.Label("The app icon from Player Settings. Shown when no occasion with an icon is active.", OKStyles.MutedWrap);

                        if (GUILayout.Button("Player Settings", EditorStyles.miniButton, GUILayout.Width(100)))
                            SettingsService.OpenProjectSettings("Project/Player");
                    }

                    GetIconList().DoLayoutList();
                    EditorGUILayout.EndScrollView();
                }

                var separator = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(separator, OKStyles.GridLine);

                using (new EditorGUILayout.VerticalScope())
                {
                    _iconDetailScroll = EditorGUILayout.BeginScrollView(_iconDetailScroll);
                    OKStyles.BeginForm();
                    DrawIconDetails();
                    DrawReleasedIcons();
                    OKStyles.EndForm();
                    GUILayout.Space(12);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private ReorderableList GetIconList()
        {
            if (_iconList == null || _iconList.serializedProperty.serializedObject != _settingsObject)
            {
                _iconList = new ReorderableList(_settingsObject, _settingsObject.FindProperty(nameof(OccasionKitSettings.icons)), true, true, true, true)
                {
                    elementHeight = 40,
                    drawHeaderCallback = rect => GUI.Label(rect, $"Launcher icons ({Settings.icons.Count})", EditorStyles.boldLabel),
                    drawElementCallback = DrawIconElement,
                    onSelectCallback = list =>
                    {
                        _selectedIcon = list.index;
                        GUI.FocusControl(null);
                    },
                    onAddCallback = list => AddIcon(),
                    onRemoveCallback = list => RemoveIcon(list.index),
                    onReorderCallbackWithDetails = (list, from, to) => _selectedIcon = to,
                };
            }

            _iconList.index = _selectedIcon;
            return _iconList;
        }

        private void DrawIconElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var settings = Settings;

            if (index >= settings.icons.Count)
                return;

            var icon = settings.icons[index];
            var thumb = new Rect(rect.x, rect.y + 4, 32, 32);
            var preview = IconPreviewCache.Get(icon, 64, MaskShape.Squircle);

            if (preview != null)
                GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit);
            else
                GUI.Label(thumb, OKStyles.SeverityIcon(IssueSeverity.Warning));

            var users = CatalogDatabase.Enumerate().Count(e => e.occasion.iconId == icon.id && !string.IsNullOrEmpty(icon.id));
            GUI.Label(new Rect(rect.x + 40, rect.y + 3, rect.width - 40, 18), string.IsNullOrEmpty(icon.Title) ? "(no id)" : icon.Title, OKStyles.RowTitle);
            GUI.Label(new Rect(rect.x + 40, rect.y + 21, rect.width - 40, 15), $"{icon.id} · {(users == 0 ? "unused" : users == 1 ? "1 occasion" : $"{users} occasions")}", OKStyles.Muted);
        }

        private void AddIcon()
        {
            var settings = Settings;
            var id = "icon";

            for (var n = 2; settings.FindIcon(id) != null; n++)
                id = $"icon_{n}";

            ModifySettings("Add icon", () => settings.icons.Add(new AppIconVariant { id = id, displayName = "New icon" }));
            _selectedIcon = settings.icons.Count - 1;
            _iconList = null;
            GUIUtility.ExitGUI();
        }

        private void RemoveIcon(int index)
        {
            var settings = Settings;

            if (index < 0 || index >= settings.icons.Count)
                return;

            var icon = settings.icons[index];
            var users = CatalogDatabase.Enumerate().Where(e => e.occasion.iconId == icon.id).ToList();

            if (users.Count > 0 && !EditorUtility.DisplayDialog("Remove icon", $"\"{icon.id}\" is used by {string.Join(", ", users.Select(u => u.occasion.Title))}. Those occasions will stop changing the icon.", "Remove", "Cancel"))
                return;

            ModifySettings("Remove icon", () => settings.icons.RemoveAt(index));
            _selectedIcon = Mathf.Min(index, settings.icons.Count - 1);
            _iconList = null;
            GUIUtility.ExitGUI();
        }

        private void DrawIconDetails()
        {
            var settings = Settings;

            if (_selectedIcon < 0 || _selectedIcon >= settings.icons.Count)
            {
                GUILayout.Space(40);
                GUILayout.Label(settings.icons.Count == 0
                    ? "No icons yet. Add one with +: a foreground layer (432×432 px or larger) and a background color or layer."
                    : "Select an icon.", OKStyles.CenteredWrap);
                return;
            }

            var icon = settings.icons[_selectedIcon];
            var property = _settingsObject.FindProperty(nameof(OccasionKitSettings.icons)).GetArrayElementAtIndex(_selectedIcon);
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 140;

            GUILayout.Label(icon.Title, OKStyles.Title);
            GUILayout.Label($"Android alias com.occasionkit.Icon_{icon.id}  ·  iOS alternate icon occasionkit_{icon.id}", OKStyles.Subtitle);
            DrawIssues(i => i.IconIndex == _selectedIcon);

            if (OKStyles.Section("icon.layers", "Layers"))
            {
                DrawIconIdField(icon, property.FindPropertyRelative(nameof(AppIconVariant.id)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.displayName)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.foreground)));
                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.background)));

                if (icon.background == null)
                    EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.backgroundColor)));

                EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.monochrome)), new GUIContent("Monochrome (themed)"));

                if ((int)PlayerSettings.Android.minSdkVersion < 26)
                    EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(AppIconVariant.legacy)), new GUIContent("Legacy (API < 26)"));
            }

            if (OKStyles.Section("icon.preview", "Preview"))
                DrawIconPreviews(icon);

            if (OKStyles.Section("icon.users", "Used by"))
            {
                var usedBy = CatalogDatabase.Enumerate().Where(e => e.occasion.iconId == icon.id && !string.IsNullOrEmpty(icon.id)).ToList();

                if (usedBy.Count == 0)
                    GUILayout.Label("No occasion uses this icon.", OKStyles.Muted);

                foreach (var (catalog, index, occasion) in usedBy)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        OKStyles.ColorChip(GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(16)), occasion.color);
                        GUILayout.Label($"{occasion.Title}  ·  {catalog.name}");

                        if (GUILayout.Button("Show", EditorStyles.miniButton, GUILayout.Width(46)))
                        {
                            _tab = Tab.Occasions;
                            SelectCatalog(catalog);
                            SelectOccasion(index);
                        }
                    }
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private void DrawIconIdField(AppIconVariant icon, SerializedProperty id)
        {
            var next = EditorGUILayout.DelayedTextField(new GUIContent("Id", "Becomes the Android alias and resource names and the iOS alternate icon name. Lowercase a-z, 0-9, _."), id.stringValue).Trim();

            if (next == id.stringValue)
                return;

            if (!OccasionValidator.IsValidIconId(next))
            {
                ShowNotification(new GUIContent("Use lowercase a-z, 0-9 and _, starting with a letter"));
                return;
            }

            if (Settings.FindIcon(next) != null)
            {
                ShowNotification(new GUIContent($"Icon \"{next}\" already exists"));
                return;
            }

            var old = id.stringValue;
            id.stringValue = next;
            var users = CatalogDatabase.Enumerate().Where(e => e.occasion.iconId == old && !string.IsNullOrEmpty(old)).ToList();

            if (users.Count > 0 && EditorUtility.DisplayDialog("Rename icon", $"Update {users.Count} occasion(s) that use \"{old}\" to \"{next}\"?", "Update", "Keep"))
            {
                foreach (var catalog in users.Select(u => u.catalog).Distinct())
                    Undo.RecordObject(catalog, "Rename icon");

                foreach (var (catalog, _, occasion) in users)
                {
                    occasion.iconId = next;
                    EditorUtility.SetDirty(catalog);
                }

                _catalogObject?.Update();
            }
        }

        private void DrawIconPreviews(AppIconVariant icon)
        {
            if (icon.foreground == null)
            {
                EditorGUILayout.HelpBox("Assign a foreground layer to see previews.", MessageType.None);
                return;
            }

            const float size = 84f;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);

                foreach (var (shape, label) in PreviewShapes)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(size + 8)))
                    {
                        var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                        var preview = IconPreviewCache.Get(icon, (int)size * 2, shape);

                        if (preview != null)
                            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);

                        GUILayout.Label(label, OKStyles.Muted, GUILayout.Width(size));
                    }
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                const float full = 160f;
                var rect = GUILayoutUtility.GetRect(full, full, GUILayout.Width(full), GUILayout.Height(full));
                var layer = IconPreviewCache.GetFullLayer(icon, (int)full * 2);

                if (layer != null)
                    GUI.DrawTexture(rect, layer, ScaleMode.ScaleToFit);

                if (Event.current.type == EventType.Repaint && _showSafeZone)
                {
                    var viewport = rect.width * IconLayers.ViewportDp / IconLayers.LayerDp;
                    var v = new Rect(rect.center.x - viewport / 2, rect.center.y - viewport / 2, viewport, viewport);
                    var outline = new Color(1f, 0.25f, 0.6f, 0.9f);
                    EditorGUI.DrawRect(new Rect(v.x, v.y, v.width, 1), outline);
                    EditorGUI.DrawRect(new Rect(v.x, v.yMax - 1, v.width, 1), outline);
                    EditorGUI.DrawRect(new Rect(v.x, v.y, 1, v.height), outline);
                    EditorGUI.DrawRect(new Rect(v.xMax - 1, v.y, 1, v.height), outline);

                    Handles.color = new Color(0.2f, 0.9f, 1f, 0.95f);
                    Handles.DrawWireDisc(rect.center, Vector3.forward, rect.width * IconLayers.SafeZoneDp / IconLayers.LayerDp / 2);
                    Handles.color = Color.white;
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Full 108 dp layer", EditorStyles.boldLabel);
                    GUILayout.Label("Pink square: the 72 dp a launcher shows.\nCyan circle: the 66 dp safe zone no mask cuts into. Keep logos and text inside it.", OKStyles.MutedWrap);
                    _showSafeZone = GUILayout.Toggle(_showSafeZone, "Show guides");
                }
            }
        }

        private void DrawReleasedIcons()
        {
            if (!OKStyles.Section("icon.released", "Released icons", false))
                return;

            GUILayout.Label("Android keeps the alias a user had enabled across updates. If an update removes it, the app loses its launcher icon, so every icon that went into a build keeps its alias for good. Removed icons stay as retired aliases that show the default icon. Recorded in " + ReleasedIconRegistry.FilePath + "; commit it.", OKStyles.MutedWrap);

            var released = ReleasedIconRegistry.LoadCached();

            if (released.Count == 0)
            {
                GUILayout.Label("No Android build has included an icon yet.", OKStyles.Muted);
                return;
            }

            foreach (var id in released)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var inSet = Settings.FindIcon(id) != null;
                    GUILayout.Label(id, EditorStyles.boldLabel, GUILayout.Width(160));
                    GUILayout.Label(inSet ? "in the icon set" : "retired: alias kept, shows the default icon", OKStyles.Muted);
                    GUILayout.FlexibleSpace();

                    if (!inSet && GUILayout.Button("Forget", EditorStyles.miniButton, GUILayout.Width(56)) &&
                        EditorUtility.DisplayDialog("Forget released icon", $"Only forget \"{id}\" if no build with it ever reached users. Otherwise users who had it lose their launcher icon after the next update.", "Forget", "Cancel"))
                    {
                        ReleasedIconRegistry.Remove(id);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
    }
}
