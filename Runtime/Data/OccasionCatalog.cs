using System.Collections.Generic;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>
    /// A list of occasions. Mark it addressable with the catalog label (default "occasion-catalog"); every catalog with
    /// that label is loaded and merged, so a remote group lets you change dates and switches without a new build.
    /// Icons are built into the app, so a catalog can only use icons the installed build has.
    /// </summary>
    [CreateAssetMenu(fileName = "OccasionCatalog", menuName = "OccasionKit/Occasion Catalog", order = 0)]
    public sealed class OccasionCatalog : ScriptableObject
    {
        public List<Occasion> occasions = new List<Occasion>();
    }
}
