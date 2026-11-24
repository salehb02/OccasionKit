using System;
using System.Collections.Generic;
using UnityEngine;
#if OCCASIONKIT_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace OccasionKit
{
    /// <summary>
    /// A greeting's or notification's title and text in one language. Used when Unity Localization isn't installed,
    /// or where its localized strings are empty.
    /// </summary>
    [Serializable]
    public class OccasionTranslation
    {
        [Tooltip("ISO 639-1 code: \"fa\", or \"fa-IR\" for one region. \"fa\" also covers \"fa-IR\".")]
        public string language = "";
        public string title = "";
        [TextArea(2, 6)]
        public string text = "";

        /// <summary>The most specific translation for <paramref name="language"/> ("fa-IR" before "fa"), or null.</summary>
        public static OccasionTranslation Find(List<OccasionTranslation> translations, string language)
        {
            if (translations == null || string.IsNullOrWhiteSpace(language))
                return null;

            OccasionTranslation best = null;
            var bestLength = -1;

            foreach (var translation in translations)
            {
                if (translation == null || !LanguageCodes.Matches(translation.language, language))
                    continue;

                var length = LanguageCodes.Normalize(translation.language).Length;

                if (length > bestLength)
                {
                    best = translation;
                    bestLength = length;
                }
            }

            return best;
        }

        /// <summary>The first text that isn't empty: localized string, then translation, then the default.</summary>
        internal static string Pick(string localized, string translated, string fallback)
        {
            if (!string.IsNullOrEmpty(localized))
                return localized;

            return !string.IsNullOrEmpty(translated) ? translated : fallback;
        }

#if OCCASIONKIT_LOCALIZATION
        /// <summary>The string in the selected locale, or null when it isn't set or can't be loaded.</summary>
        internal static string Localize(LocalizedString text)
        {
            if (text == null || text.IsEmpty)
                return null;

            try
            {
                return text.GetLocalizedString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Couldn't load a localized string ({text}): {e.Message}");
                return null;
            }
        }
#endif
    }
}
