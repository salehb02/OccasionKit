using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>One launcher icon. Built into the app as an Android adaptive icon.</summary>
    [Serializable]
    public class AppIconVariant
    {
        [Tooltip("Lowercase letters, digits and _. Becomes the Android alias name, so never rename a released icon.")]
        public string id = "";
        public string displayName = "";

        [Tooltip("108×108 dp layer, at least 432×432 px. Keep the art inside the center 66 dp circle; launchers mask the rest.")]
        public Texture2D foreground;

        [Tooltip("Optional full-bleed 108×108 dp layer. Empty: Background Color.")]
        public Texture2D background;

        public Color backgroundColor = Color.white;

        [Tooltip("Optional. Android 13+ themed icons: a one-color silhouette on transparency, same layout as the foreground.")]
        public Texture2D monochrome;

        [Tooltip("Optional square icon for Android 7.1 and older. Empty: made from the layers. Only built when Min API Level is below 26.")]
        public Texture2D legacy;

        public string Title { get => string.IsNullOrEmpty(displayName) ? id : displayName; }
    }

    /// <summary>
    /// Editor-only project settings: icon set and store names. Never shipped; the build step turns the icons into
    /// Android resources and the catalogs refer to them by id.
    /// </summary>
    public sealed class OccasionKitSettings : ScriptableObject
    {
        public const string DefaultPath = "Assets/OccasionKit/OccasionKitSettings.asset";

        [Tooltip("Addressables label the catalogs are loaded with. Must match OccasionConfig.CatalogKey.")]
        public string catalogLabel = OccasionSystem.DefaultCatalogLabel;

        [Tooltip("Store names for targeting. The app passes the current one as OccasionConfig.Store.")]
        public List<string> stores = new List<string>();

        public List<AppIconVariant> icons = new List<AppIconVariant>();

        private static OccasionKitSettings _instance;

        /// <summary>The project's settings, or null when there are none yet.</summary>
        public static OccasionKitSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();

                return _instance;
            }
        }

        public static OccasionKitSettings Create(string path = DefaultPath)
        {
            var existing = Instance;

            if (existing != null)
                return existing;

            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var settings = CreateInstance<OccasionKitSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            _instance = settings;
            return settings;
        }

        public AppIconVariant FindIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var icon in icons)
            {
                if (icon != null && icon.id == id)
                    return icon;
            }

            return null;
        }

        private static OccasionKitSettings Load()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(OccasionKitSettings));

            if (guids.Length > 1)
                Debug.LogWarning($"[OccasionKit] {guids.Length} OccasionKitSettings assets found; using {AssetDatabase.GUIDToAssetPath(guids[0])}.");

            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<OccasionKitSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
