#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>
    /// Switches between the &lt;activity-alias&gt; entries the OccasionKit build step adds to the manifest. The Java side
    /// (com.occasionkit.OccasionIcons) does the work so scheduled changes can run while the app is closed.
    /// </summary>
    public sealed class AndroidAppIconBackend : IAppIconBackend
    {
        private const string JavaClass = "com.occasionkit.OccasionIcons";

        private readonly AndroidJavaClass _icons;
        private readonly AndroidJavaObject _context;
        private readonly string[] _available = Array.Empty<string>();

        public AndroidAppIconBackend()
        {
            try
            {
                _icons = new AndroidJavaClass(JavaClass);

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _context = activity.Call<AndroidJavaObject>("getApplicationContext");

                _available = _icons.CallStatic<string[]>("getIcons", _context) ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Android icon switching unavailable: {e.Message}");
                _icons = null;
            }
        }

        /// <summary>False when the build has no OccasionKit aliases (no icon set, or the build step didn't run).</summary>
        public bool IsSupported { get => _icons != null && _available.Length > 0; }

        public IReadOnlyList<string> Icons { get => _available; }

        public string CurrentIcon
        {
            get
            {
                if (!IsSupported)
                    return null;

                try
                {
                    return _icons.CallStatic<string>("getCurrentIcon", _context);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OccasionKit] Reading the current icon failed: {e.Message}");
                    return null;
                }
            }
        }

        public bool SetIcon(string iconId)
        {
            if (!IsSupported)
                return false;

            try
            {
                return _icons.CallStatic<bool>("setIcon", _context, iconId ?? OccasionSystem.DefaultIconId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Switching the icon failed: {e.Message}");
                return false;
            }
        }

        public void ScheduleChanges(IReadOnlyList<IconChange> changes)
        {
            if (!IsSupported)
                return;

            if (changes == null || changes.Count == 0)
            {
                CancelScheduledChanges();
                return;
            }

            var times = new long[changes.Count];
            var icons = new string[changes.Count];

            for (var i = 0; i < changes.Count; i++)
            {
                times[i] = new DateTimeOffset(OccasionClock.ToDeviceTime(changes[i].Time)).ToUnixTimeMilliseconds();
                icons[i] = changes[i].IconId;
            }

            try
            {
                _icons.CallStatic("schedule", _context, times, icons);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Scheduling icon changes failed: {e.Message}");
            }
        }

        public void CancelScheduledChanges()
        {
            if (!IsSupported)
                return;

            try
            {
                _icons.CallStatic("cancel", _context);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Cancelling icon changes failed: {e.Message}");
            }
        }
    }
}
#endif
