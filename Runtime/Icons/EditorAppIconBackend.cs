#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>Pretends to switch icons in the Editor so the whole flow can be tested in Play Mode.</summary>
    public sealed class EditorAppIconBackend : IAppIconBackend
    {
        private const string CurrentKey = "OccasionKit.EditorCurrentIcon";

        /// <summary>Set by the editor assembly: the icon ids of the project's icon set.</summary>
        public static Func<IReadOnlyList<string>> IconsProvider;

        /// <summary>What a build would hand the OS when the app goes to the background.</summary>
        public static IReadOnlyList<IconChange> Scheduled { get; private set; } = Array.Empty<IconChange>();

        public bool IsSupported { get => true; }

        public IReadOnlyList<string> Icons
        {
            get
            {
                var icons = IconsProvider?.Invoke();
                return icons != null && icons.Count > 0 ? icons : new[] { OccasionSystem.DefaultIconId };
            }
        }

        public string CurrentIcon { get => SessionState.GetString(CurrentKey, OccasionSystem.DefaultIconId); }

        public static void ResetSimulatedIcon() => SessionState.EraseString(CurrentKey);

        public bool SetIcon(string iconId)
        {
            if (!Icons.Contains(iconId))
                iconId = OccasionSystem.DefaultIconId;

            SessionState.SetString(CurrentKey, iconId);
            Debug.Log($"[OccasionKit] App icon → \"{iconId}\" (simulated in the Editor)");
            return true;
        }

        public void ScheduleChanges(IReadOnlyList<IconChange> changes)
        {
            Scheduled = changes?.ToArray() ?? Array.Empty<IconChange>();
        }

        public void CancelScheduledChanges()
        {
            Scheduled = Array.Empty<IconChange>();
        }
    }
}
#endif
