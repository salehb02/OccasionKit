using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal static class AddressablesHelper
    {
        public static AddressableAssetSettings Settings { get => AddressableAssetSettingsDefaultObject.Settings; }

        public static AddressableAssetEntry GetEntry(Object asset)
        {
            var settings = Settings;

            if (settings == null || asset == null)
                return null;

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid, true);
        }

        public static bool HasLabel(AddressableAssetEntry entry, string label) => entry != null && entry.labels.Contains(label);

        /// <summary>"Group (remote) · label ✓" style summary for the UI.</summary>
        public static string Describe(Object asset, string label)
        {
            if (Settings == null)
                return "Addressables isn't set up";

            var entry = GetEntry(asset);

            if (entry == null)
                return "Not addressable";

            var group = entry.parentGroup != null ? entry.parentGroup.Name : "?";
            var remote = IsRemote(entry.parentGroup) ? " (remote)" : "";
            return HasLabel(entry, label) ? $"{group}{remote} · label \"{label}\"" : $"{group}{remote} · missing label \"{label}\"";
        }

        public static bool IsRemote(AddressableAssetGroup group)
        {
            var settings = Settings;
            var schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;

            if (schema == null || settings == null)
                return false;

            var name = schema.LoadPath.GetName(settings) ?? "";
            return name.IndexOf("Remote", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>A menu of groups to put the asset in. Remote groups let catalog edits ship without a new build.</summary>
        public static void ShowMakeAddressableMenu(Object asset, string label)
        {
            var settings = Settings;

            if (settings == null)
            {
                if (EditorUtility.DisplayDialog("Addressables", "Addressables isn't set up in this project. Create its settings now?", "Create", "Cancel"))
                    AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);

                return;
            }

            var menu = new GenericMenu();

            foreach (var group in settings.groups)
            {
                if (group == null || group.ReadOnly || group.GetSchema<BundledAssetGroupSchema>() == null)
                    continue;

                var target = group;
                var name = group.Name + (IsRemote(group) ? "  (remote)" : "  (local)");
                menu.AddItem(new GUIContent(name), false, () => MakeAddressable(asset, target, label));
            }

            menu.ShowAsContext();
        }

        public static void MakeAddressable(Object asset, AddressableAssetGroup group, string label)
        {
            var settings = Settings;

            if (settings == null || asset == null)
                return;

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            var entry = settings.CreateOrMoveEntry(guid, group ?? settings.DefaultGroup, false, true);
            AddLabel(entry, label);
        }

        public static void AddLabel(AddressableAssetEntry entry, string label)
        {
            var settings = Settings;

            if (settings == null || entry == null || string.IsNullOrEmpty(label))
                return;

            settings.AddLabel(label);
            entry.SetLabel(label, true, true);
        }
    }
}
