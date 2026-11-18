using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace OccasionKit.EditorTools
{
    /// <summary>Every OccasionCatalog in the project, merged the way the runtime merges them.</summary>
    [InitializeOnLoad]
    internal static class CatalogDatabase
    {
        private static List<OccasionCatalog> _all;

        static CatalogDatabase()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static IReadOnlyList<OccasionCatalog> All
        {
            get
            {
                if (_all == null || _all.Any(c => c == null))
                {
                    _all = AssetDatabase.FindAssets("t:" + nameof(OccasionCatalog))
                        .Select(guid => AssetDatabase.LoadAssetAtPath<OccasionCatalog>(AssetDatabase.GUIDToAssetPath(guid)))
                        .Where(c => c != null)
                        .OrderBy(c => c.name, StringComparer.Ordinal)
                        .ToList();
                }

                return _all;
            }
        }

        public static void Invalidate() => _all = null;

        /// <summary>All occasions, first id wins (same order and rule as <see cref="OccasionSystem"/>).</summary>
        public static List<Occasion> GetMergedOccasions()
        {
            var ids = new HashSet<string>();
            var result = new List<Occasion>();

            foreach (var catalog in All)
            {
                foreach (var occasion in catalog.occasions)
                {
                    if (occasion != null && !string.IsNullOrEmpty(occasion.id) && ids.Add(occasion.id))
                        result.Add(occasion);
                }
            }

            return result;
        }

        public static IEnumerable<(OccasionCatalog catalog, int index, Occasion occasion)> Enumerate()
        {
            foreach (var catalog in All)
            {
                for (var i = 0; i < catalog.occasions.Count; i++)
                {
                    if (catalog.occasions[i] != null)
                        yield return (catalog, i, catalog.occasions[i]);
                }
            }
        }

        public static IEnumerable<string> GetOccasionIds() => Enumerate().Select(e => e.occasion.id).Where(id => !string.IsNullOrEmpty(id)).Distinct();
    }
}
