using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>
    /// Every icon id that went into an Android build. Android remembers which aliases a user had enabled across
    /// updates: if an update drops the alias that is showing, the app loses its launcher icon. So once an icon has
    /// shipped, the build keeps its alias forever (retired ones just point at the default icon).
    /// Lives in ProjectSettings so deleting assets can't lose it. Commit it.
    /// </summary>
    internal static class ReleasedIconRegistry
    {
        public const string FilePath = "ProjectSettings/OccasionKitReleasedIcons.json";

        [Serializable]
        private sealed class Data
        {
            public List<string> icons = new List<string>();
        }

        private static List<string> _cached;
        private static DateTime _cachedWriteTime;

        /// <summary>For drawing every frame: re-reads the file only when it changed on disk. Don't modify the result.</summary>
        public static IReadOnlyList<string> LoadCached()
        {
            var writeTime = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;

            if (_cached == null || writeTime != _cachedWriteTime)
            {
                _cached = Load();
                _cachedWriteTime = writeTime;
            }

            return _cached;
        }

        public static List<string> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath))?.icons ?? new List<string>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[OccasionKit] Reading {FilePath} failed: {e.Message}");
            }

            return new List<string>();
        }

        public static void Add(IEnumerable<string> ids)
        {
            var icons = Load();
            var changed = false;

            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || id == OccasionSystem.DefaultIconId || icons.Contains(id))
                    continue;

                icons.Add(id);
                changed = true;
            }

            if (changed)
                Save(icons);
        }

        public static void Remove(string id)
        {
            var icons = Load();

            if (icons.Remove(id))
                Save(icons);
        }

        private static void Save(List<string> icons)
        {
            icons.Sort(StringComparer.Ordinal);
            File.WriteAllText(FilePath, JsonUtility.ToJson(new Data { icons = icons }, true));
        }
    }
}
