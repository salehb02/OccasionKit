using System.Collections.Generic;
using UnityEngine;

namespace OccasionKit
{
    public sealed class OccasionConfig
    {
        /// <summary>Addressables key (label or address) of the catalogs. Every OccasionCatalog with it is loaded and merged.</summary>
        public string CatalogKey = OccasionSystem.DefaultCatalogLabel;

        /// <summary>Use these catalogs instead of loading them from Addressables (tests, projects without Addressables content).</summary>
        public IReadOnlyList<OccasionCatalog> Catalogs;

        /// <summary>Store this build is for, matched against each occasion's store targeting (case-insensitive).</summary>
        public string Store;

        /// <summary>ISO 639-1 code matched against language targeting, e.g. the app's selected locale. Default: the device language.</summary>
        public string Language;

        /// <summary>Switch the launcher icon. Off: occasions, themes and greetings still work.</summary>
        public bool ManageAppIcon = true;

        /// <summary>How far ahead icon changes are handed to the OS for while the app is closed.</summary>
        public int ScheduleDays = 60;

        public int MaxScheduledChanges = 12;

        /// <summary>Custom icon switching. Default: Android aliases on device, a simulation in the Editor.</summary>
        public IAppIconBackend IconBackend;

        /// <summary>Log state changes.</summary>
        public bool Verbose;
    }
}
