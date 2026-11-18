#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>
    /// Switches between the alternate app icons the OccasionKit build step adds to the Xcode project
    /// (<c>occasionkit_&lt;id&gt;</c>). iOS only allows it while the app is active and shows an alert each time, so
    /// <see cref="OccasionSystem"/> switches after launch and on each return to the app, and schedules nothing.
    /// </summary>
    public sealed class IOSAppIconBackend : IAppIconBackend, IAppIconSwitchMoment
    {
        private const string Prefix = "occasionkit_";

        [DllImport("__Internal")] private static extern int OccasionKit_SupportsAlternateIcons();
        [DllImport("__Internal")] private static extern string OccasionKit_GetAlternateIconNames();
        [DllImport("__Internal")] private static extern string OccasionKit_GetAlternateIconName();
        [DllImport("__Internal")] private static extern void OccasionKit_SetAlternateIconName(string name);

        private readonly bool _supported;
        private readonly List<string> _icons = new List<string> { OccasionSystem.DefaultIconId };

        public IOSAppIconBackend()
        {
            try
            {
                _supported = OccasionKit_SupportsAlternateIcons() != 0;

                foreach (var name in (OccasionKit_GetAlternateIconNames() ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (name.StartsWith(Prefix, StringComparison.Ordinal))
                        _icons.Add(name.Substring(Prefix.Length));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] iOS icon switching unavailable: {e.Message}");
                _supported = false;
            }
        }

        /// <summary>False when the build has no OccasionKit icons (no icon set, or the build step didn't run).</summary>
        public bool IsSupported { get => _supported && _icons.Count > 1; }

        public IReadOnlyList<string> Icons { get => _icons; }

        public IconSwitchMoment SwitchMoment { get => IconSwitchMoment.Foreground; }

        /// <summary>The OccasionKit icon showing, the default for the primary icon, or null for another plugin's alternate icon.</summary>
        public string CurrentIcon
        {
            get
            {
                if (!_supported)
                    return null;

                var name = OccasionKit_GetAlternateIconName();

                if (string.IsNullOrEmpty(name))
                    return OccasionSystem.DefaultIconId;

                return name.StartsWith(Prefix, StringComparison.Ordinal) ? name.Substring(Prefix.Length) : null;
            }
        }

        /// <summary>Asks iOS for the icon. It completes asynchronously; a failure is logged to the Xcode console and retried on the next return to the app.</summary>
        public bool SetIcon(string iconId)
        {
            if (!IsSupported)
                return false;

            var useDefault = string.IsNullOrEmpty(iconId) || iconId == OccasionSystem.DefaultIconId || !_icons.Contains(iconId);
            OccasionKit_SetAlternateIconName(useDefault ? null : Prefix + iconId);
            return true;
        }

        public void ScheduleChanges(IReadOnlyList<IconChange> changes) { }

        public void CancelScheduledChanges() { }
    }
}
#endif
