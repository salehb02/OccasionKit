using System;
using System.Globalization;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>
    /// ISO 639-1 language codes. Unity's <see cref="SystemLanguage"/> has no Persian (Persian devices report Unknown),
    /// so targeting uses codes and the device language is read from the OS.
    /// </summary>
    public static class LanguageCodes
    {
        /// <summary>"fa" matches "fa" and "fa-IR"; "fa-IR" only matches "fa-IR". Case-insensitive.</summary>
        public static bool Matches(string target, string language)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(language))
                return false;

            target = Normalize(target);
            language = Normalize(language);
            return language == target || language.StartsWith(target + "-", StringComparison.Ordinal);
        }

        public static string Normalize(string code)
        {
            code = (code ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();

            // Java still reports a few retired codes.
            if (code == "iw" || code.StartsWith("iw-")) return "he" + code.Substring(2);
            if (code == "in" || code.StartsWith("in-")) return "id" + code.Substring(2);
            if (code == "ji" || code.StartsWith("ji-")) return "yi" + code.Substring(2);

            return code;
        }

        /// <summary>The device language as an ISO code, or "" when unknown.</summary>
        public static string FromDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var current = locale.CallStatic<AndroidJavaObject>("getDefault"))
                {
                    var language = current.Call<string>("getLanguage");

                    if (!string.IsNullOrEmpty(language))
                        return Normalize(language);
                }
            }
            catch (Exception)
            {
                // Fall through to Unity's value.
            }
#endif
            var code = FromSystemLanguage(Application.systemLanguage);

            if (!string.IsNullOrEmpty(code))
                return code;

            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return culture == "iv" ? string.Empty : Normalize(culture);
        }

        public static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Afrikaans: return "af";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Basque: return "eu";
                case SystemLanguage.Belarusian: return "be";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Catalan: return "ca";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Danish: return "da";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.English: return "en";
                case SystemLanguage.Estonian: return "et";
                case SystemLanguage.Faroese: return "fo";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Hungarian: return "hu";
                case SystemLanguage.Icelandic: return "is";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Latvian: return "lv";
                case SystemLanguage.Lithuanian: return "lt";
                case SystemLanguage.Norwegian: return "no";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.SerboCroatian: return "sh";
                case SystemLanguage.Slovak: return "sk";
                case SystemLanguage.Slovenian: return "sl";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Vietnamese: return "vi";
                default: return string.Empty;
            }
        }
    }
}
