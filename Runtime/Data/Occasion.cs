using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if OCCASIONKIT_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace OccasionKit
{
    [Serializable]
    public class Occasion
    {
        [Tooltip("Stable key used by code, analytics and saved state. Never rename a released occasion.")]
        public string id = "";
        public string displayName = "";
        public bool enabled = true;

        [Tooltip("Higher wins when occasions overlap (icon, theme, primary occasion).")]
        public int priority;

        [Tooltip("While active, every lower-priority occasion is suppressed: no icon, no theme, no greeting. Use for mourning days.")]
        public bool exclusive;

        [Tooltip("Only used by the editor timeline.")]
        public Color color = new Color(0.36f, 0.6f, 0.98f);

        public OccasionSchedule schedule = new OccasionSchedule();

        [Tooltip("App icon id from the OccasionKit icon set. Empty: this occasion doesn't change the icon.")]
        public string iconId = "";

        [Tooltip("Any addressable asset with themed content. Load it with OccasionSystem.LoadThemeAsync.")]
        public AssetReference theme = new AssetReference();

        public OccasionTargeting targeting = new OccasionTargeting();
        public OccasionGreeting greeting = new OccasionGreeting();
        public OccasionNotification notification = new OccasionNotification();

        [TextArea(2, 6)]
        public string notes = "";

        public bool HasIcon { get => !string.IsNullOrEmpty(iconId); }
        public bool HasTheme { get => theme != null && theme.RuntimeKeyIsValid(); }

        public string Title { get => string.IsNullOrEmpty(displayName) ? id : displayName; }

        public override string ToString() => $"{Title} ({id})";
    }

    [Serializable]
    public class OccasionTargeting
    {
        [Tooltip("Store names this occasion runs in (matched against OccasionConfig.Store). Empty: every store.")]
        public List<string> stores = new List<string>();

        [Tooltip("ISO 639-1 language codes (\"fa\", \"en\") this occasion runs for; \"fa\" also matches \"fa-IR\". Empty: every language.")]
        public List<string> languages = new List<string>();

        public bool Matches(OccasionContext context)
        {
            if (context == null || context.IgnoreTargeting)
                return true;

            if (stores != null && stores.Count > 0)
            {
                if (string.IsNullOrEmpty(context.Store))
                    return false;

                var found = false;

                foreach (var store in stores)
                {
                    if (string.Equals(store, context.Store, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            if (languages == null || languages.Count == 0)
                return true;

            foreach (var language in languages)
            {
                if (LanguageCodes.Matches(language, context.Language))
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public class OccasionGreeting
    {
        public bool enabled;
        [Tooltip("Default title, for languages without a translation.")]
        public string title = "";
        [Tooltip("Default message, for languages without a translation.")]
        [TextArea(2, 6)]
        public string message = "";
#if OCCASIONKIT_LOCALIZATION
        [Tooltip("From a Unity Localization string table, in the selected locale. Empty: the translations, then the default title.")]
        public LocalizedString localizedTitle = new LocalizedString();
        [Tooltip("From a Unity Localization string table, in the selected locale. Empty: the translations, then the default message.")]
        public LocalizedString localizedMessage = new LocalizedString();
#endif
        [Tooltip("Title and message per language. Empty fields fall back to the defaults.")]
        public List<OccasionTranslation> translations = new List<OccasionTranslation>();
        [Tooltip("Also show it during lead and tail days. Off: only during the main window.")]
        public bool includeLeadAndTail;

        /// <summary>
        /// Title and message for a player: each comes from the Unity Localization string in the selected locale when
        /// that package is installed and the string is set, otherwise from the translation for
        /// <paramref name="language"/>, otherwise from the default text.
        /// </summary>
        public (string title, string message) GetText(string language)
        {
            var translation = OccasionTranslation.Find(translations, language);
#if OCCASIONKIT_LOCALIZATION
            var localTitle = OccasionTranslation.Localize(localizedTitle);
            var localMessage = OccasionTranslation.Localize(localizedMessage);
#else
            string localTitle = null, localMessage = null;
#endif
            return (OccasionTranslation.Pick(localTitle, translation?.title, title), OccasionTranslation.Pick(localMessage, translation?.text, message));
        }
    }

    public enum NotificationAnchor
    {
        WindowStart = 0,
        MainStart = 1,
        Countdown = 2,
    }

    [Serializable]
    public class OccasionNotification
    {
        public bool enabled;
        [Tooltip("Default title, for languages without a translation.")]
        public string title = "";
        [Tooltip("Default body, for languages without a translation.")]
        [TextArea(2, 6)]
        public string body = "";
#if OCCASIONKIT_LOCALIZATION
        [Tooltip("From a Unity Localization string table, in the locale selected when the app goes to the background. Empty: the translations, then the default title.")]
        public LocalizedString localizedTitle = new LocalizedString();
        [Tooltip("From a Unity Localization string table, in the locale selected when the app goes to the background. Empty: the translations, then the default body.")]
        public LocalizedString localizedBody = new LocalizedString();
#endif
        [Tooltip("Title and body per language. Empty fields fall back to the defaults.")]
        public List<OccasionTranslation> translations = new List<OccasionTranslation>();
        public NotificationAnchor anchor = NotificationAnchor.MainStart;
        [Tooltip("Minutes after the anchor. Negative fires before it.")]
        public int offsetMinutes;

        /// <summary>Title and body, resolved like <see cref="OccasionGreeting.GetText"/>.</summary>
        public (string title, string body) GetText(string language)
        {
            var translation = OccasionTranslation.Find(translations, language);
#if OCCASIONKIT_LOCALIZATION
            var localTitle = OccasionTranslation.Localize(localizedTitle);
            var localBody = OccasionTranslation.Localize(localizedBody);
#else
            string localTitle = null, localBody = null;
#endif
            return (OccasionTranslation.Pick(localTitle, translation?.title, title), OccasionTranslation.Pick(localBody, translation?.text, body));
        }
    }
}
