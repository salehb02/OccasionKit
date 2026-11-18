using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace OccasionKit.Notifications
{
    public readonly struct PlannedNotification
    {
        public readonly OccasionInstance Instance;
        public readonly DateTime FireTime;
        public readonly string Title;
        public readonly string Body;

        public PlannedNotification(OccasionInstance instance, DateTime fireTime, string title, string body)
        {
            Instance = instance;
            FireTime = fireTime;
            Title = title;
            Body = body;
        }
    }

    /// <summary>
    /// Local notifications for occasions that have one enabled, on Android and iOS. Compiled only when
    /// com.unity.mobile.notifications is installed. Call <see cref="Enable"/> once; notifications are rescheduled every
    /// time the app goes to the background. Asking for permission is up to the app: POST_NOTIFICATIONS on Android 13+,
    /// and on iOS either the package's "Request Authorization on App Launch" setting or an AuthorizationRequest.
    /// </summary>
    public static class OccasionNotifications
    {
        private const string PrefsIds = "OccasionKit.NotificationIds";

        private static string _channelId = "occasionkit";
        private static string _channelName = "Events";
        private static string _channelDescription = "Seasonal events";
        private static int _days = 30;
        private static Func<OccasionInstance, (string title, string body)> _text;
        private static bool _enabled;
#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool _channelRegistered;
#endif

        /// <param name="text">Optional: localized title and body per occurrence. Default: the occasion's own fields.</param>
        /// <param name="channelId">Android notification channel; on iOS it groups the notifications (thread identifier).</param>
        public static void Enable(string channelName = "Events", string channelDescription = "Seasonal events", int days = 30, Func<OccasionInstance, (string title, string body)> text = null, string channelId = "occasionkit")
        {
            _channelId = channelId;
            _channelName = channelName;
            _channelDescription = channelDescription;
            _days = Math.Max(1, days);
            _text = text;

            if (_enabled)
                return;

            _enabled = true;
            OccasionSystem.GoingToBackground += Schedule;
        }

        public static void Disable()
        {
            if (!_enabled)
                return;

            _enabled = false;
            OccasionSystem.GoingToBackground -= Schedule;
            CancelScheduled();
        }

        /// <summary>
        /// What would be scheduled from <paramref name="now"/>: one notification per occurrence whose fire time is still
        /// ahead and that wouldn't be suppressed by an exclusive occasion at that moment.
        /// </summary>
        public static List<PlannedNotification> Plan(IReadOnlyList<Occasion> occasions, DateTime now, int days, OccasionContext context, Func<OccasionInstance, (string title, string body)> text = null)
        {
            var results = new List<PlannedNotification>();
            var instances = new List<OccasionInstance>();
            var end = now.AddDays(days);

            foreach (var occasion in occasions)
            {
                var settings = occasion?.notification;

                if (settings == null || !settings.enabled || !occasion.enabled || !occasion.targeting.Matches(context))
                    continue;

                instances.Clear();
                ScheduleResolver.GetInstances(occasion, now, end.AddDays(Math.Max(0, occasion.schedule.leadDays) + 1), instances);

                foreach (var instance in instances)
                {
                    var fireTime = GetAnchor(instance, settings.anchor).AddMinutes(settings.offsetMinutes);

                    if (fireTime <= now || fireTime > end)
                        continue;

                    var active = OccasionEvaluator.Evaluate(occasions, fireTime, context).Active;

                    if (settings.anchor != NotificationAnchor.Countdown && !active.Any(a => a.Occasion == occasion))
                        continue;

                    var (title, body) = text != null ? text(instance) : (settings.title, settings.body);

                    if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
                        continue;

                    results.Add(new PlannedNotification(instance, fireTime, title, body));
                }
            }

            results.Sort((a, b) => a.FireTime.CompareTo(b.FireTime));
            return results;
        }

        private static DateTime GetAnchor(OccasionInstance instance, NotificationAnchor anchor)
        {
            switch (anchor)
            {
                case NotificationAnchor.WindowStart: return instance.WindowStart;
                case NotificationAnchor.Countdown: return instance.CountdownTarget;
                default: return instance.MainStart;
            }
        }

        private static void Schedule()
        {
            CancelScheduled();

            var planned = Plan(OccasionSystem.Occasions, OccasionClock.Now, _days, OccasionSystem.Context, _text);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!_channelRegistered)
                {
                    AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(_channelId, _channelName, _channelDescription, Importance.Default));
                    _channelRegistered = true;
                }

                var ids = new List<int>();

                foreach (var notification in planned)
                {
                    var fireTime = OccasionClock.ToDeviceTime(notification.FireTime);
                    ids.Add(AndroidNotificationCenter.SendNotification(new AndroidNotification(notification.Title, notification.Body, fireTime), _channelId));
                }

                PlayerPrefs.SetString(PrefsIds, string.Join(",", ids));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Scheduling notifications failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                var ids = new List<string>();

                foreach (var notification in planned)
                {
                    var fireTime = OccasionClock.ToDeviceTime(notification.FireTime);
                    var id = $"occasionkit.{notification.Instance.Key}";

                    iOSNotificationCenter.ScheduleNotification(new iOSNotification(id)
                    {
                        Title = notification.Title,
                        Body = notification.Body,
                        ThreadIdentifier = _channelId,
                        ShowInForeground = false,
                        Trigger = new iOSNotificationCalendarTrigger
                        {
                            Year = fireTime.Year,
                            Month = fireTime.Month,
                            Day = fireTime.Day,
                            Hour = fireTime.Hour,
                            Minute = fireTime.Minute,
                            Second = fireTime.Second,
                            UtcTime = false,
                            Repeats = false,
                        },
                    });

                    ids.Add(id);
                }

                PlayerPrefs.SetString(PrefsIds, string.Join(",", ids));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Scheduling notifications failed: {e.Message}");
            }
#else
            foreach (var notification in planned)
                Debug.Log($"[OccasionKit] Would notify at {notification.FireTime:yyyy-MM-dd HH:mm}: {notification.Title} — {notification.Body}");
#endif
        }

        private static void CancelScheduled()
        {
            var value = PlayerPrefs.GetString(PrefsIds, string.Empty);

            if (string.IsNullOrEmpty(value))
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var part in value.Split(','))
            {
                if (int.TryParse(part, out var id))
                {
                    try
                    {
                        AndroidNotificationCenter.CancelScheduledNotification(id);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[OccasionKit] Cancelling notification {id} failed: {e.Message}");
                    }
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            foreach (var id in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    iOSNotificationCenter.RemoveScheduledNotification(id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OccasionKit] Cancelling notification {id} failed: {e.Message}");
                }
            }
#endif

            PlayerPrefs.DeleteKey(PrefsIds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _enabled = false;
            _text = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            _channelRegistered = false;
#endif
        }
    }
}
