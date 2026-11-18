using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    internal enum IssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    internal sealed class ValidationIssue
    {
        public IssueSeverity Severity;
        public string Message;
        public OccasionCatalog Catalog;
        public int OccasionIndex = -1;
        public int IconIndex = -1;
        public string FixLabel;
        /// <summary>Gets the button rect so a fix can open a dropdown under it.</summary>
        public Action<Rect> Fix;
    }

    internal static class OccasionValidator
    {
        private static readonly Regex IconIdPattern = new Regex(@"^[a-z][a-z0-9_]*$");
        private static readonly Regex OccasionIdPattern = new Regex(@"^[a-z0-9][a-z0-9_.\-]*$");

        /// <summary>Icon ids become Android resource and component names.</summary>
        public static bool IsValidIconId(string id) => !string.IsNullOrEmpty(id) && id != OccasionSystem.DefaultIconId && IconIdPattern.IsMatch(id);

        public static List<ValidationIssue> Run(OccasionKitSettings settings, IReadOnlyList<OccasionCatalog> catalogs)
        {
            var issues = new List<ValidationIssue>();

            if (settings == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = "No OccasionKit settings in this project. They hold the icon set and store names.",
                    FixLabel = "Create settings",
                    Fix = _ => Selection.activeObject = OccasionKitSettings.Create(),
                });
            }
            else
            {
                issues.AddRange(ValidateIcons(settings));
                ValidateStores(settings, issues);
            }

            if (catalogs.Count == 0)
                issues.Add(new ValidationIssue { Severity = IssueSeverity.Info, Message = "No occasion catalog yet. Create one from the Occasions tab." });

            var label = settings != null ? settings.catalogLabel : OccasionSystem.DefaultCatalogLabel;
            var seenIds = new Dictionary<string, OccasionCatalog>();

            foreach (var catalog in catalogs)
            {
                ValidateAddressables(catalog, label, issues);

                for (var i = 0; i < catalog.occasions.Count; i++)
                {
                    var occasion = catalog.occasions[i];

                    if (occasion == null)
                        continue;

                    ValidateOccasion(settings, catalog, i, occasion, issues);

                    if (string.IsNullOrEmpty(occasion.id))
                        continue;

                    if (seenIds.TryGetValue(occasion.id, out var first))
                        Add(issues, IssueSeverity.Error, catalog, i, $"Id \"{occasion.id}\" is also used in {(first == catalog ? "this catalog" : first.name)}. The runtime keeps only the first one.");
                    else
                        seenIds[occasion.id] = catalog;
                }
            }

            ValidateOverlaps(catalogs, issues);
            return issues;
        }

        public static List<ValidationIssue> ValidateIcons(OccasionKitSettings settings)
        {
            var issues = new List<ValidationIssue>();
            var seen = new HashSet<string>();
            var used = new HashSet<string>(CatalogDatabase.Enumerate().Select(e => e.occasion.iconId).Where(id => !string.IsNullOrEmpty(id)));

            for (var i = 0; i < settings.icons.Count; i++)
            {
                var icon = settings.icons[i];

                if (icon == null)
                    continue;

                var name = string.IsNullOrEmpty(icon.id) ? $"Icon #{i + 1}" : $"Icon \"{icon.id}\"";

                if (icon.id == OccasionSystem.DefaultIconId)
                    AddIcon(issues, IssueSeverity.Error, i, $"{name}: \"default\" is reserved for the Player Settings icon.");
                else if (!IsValidIconId(icon.id))
                    AddIcon(issues, IssueSeverity.Error, i, $"{name}: ids must start with a lowercase letter and use only a-z, 0-9 and _ (they become Android resource names).");
                else if (!seen.Add(icon.id))
                    AddIcon(issues, IssueSeverity.Error, i, $"{name} is defined twice.");

                if (icon.foreground == null)
                {
                    AddIcon(issues, IssueSeverity.Error, i, $"{name} has no foreground layer.");
                }
                else
                {
                    CheckTexture(issues, i, name, "foreground", icon.foreground);
                    CheckTexture(issues, i, name, "background", icon.background);
                    CheckTexture(issues, i, name, "monochrome", icon.monochrome);
                }

                if (IsValidIconId(icon.id) && !used.Contains(icon.id))
                    AddIcon(issues, IssueSeverity.Info, i, $"{name} isn't used by any occasion.");
            }

            foreach (var released in ReleasedIconRegistry.Load())
            {
                if (settings.FindIcon(released) == null)
                    issues.Add(new ValidationIssue { Severity = IssueSeverity.Info, Message = $"Icon \"{released}\" shipped before and was removed. Builds keep its alias pointing at the default icon, so users who had it keep a launcher icon." });
            }

            return issues;
        }

        private static void CheckTexture(List<ValidationIssue> issues, int index, string name, string layer, Texture2D texture)
        {
            if (texture == null)
                return;

            var path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            int width = texture.width, height = texture.height;

            if (importer != null)
                importer.GetSourceTextureWidthAndHeight(out width, out height);

            if (width != height)
                AddIcon(issues, IssueSeverity.Warning, index, $"{name}: the {layer} layer is {width}×{height}; it should be square.");
            else if (width < IconLayers.RecommendedSourceSize)
                AddIcon(issues, IssueSeverity.Warning, index, $"{name}: the {layer} layer is {width} px; use at least {IconLayers.RecommendedSourceSize} px so xxxhdpi screens stay sharp.");

            if (importer != null && !importer.sRGBTexture)
                AddIcon(issues, IssueSeverity.Warning, index, $"{name}: the {layer} texture has sRGB off; colors may come out wrong if it isn't a PNG or JPG.");
        }

        private static void ValidateStores(OccasionKitSettings settings, List<ValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var store in settings.stores)
            {
                if (string.IsNullOrWhiteSpace(store))
                    issues.Add(new ValidationIssue { Severity = IssueSeverity.Warning, Message = "Settings: a store name is empty." });
                else if (!seen.Add(store))
                    issues.Add(new ValidationIssue { Severity = IssueSeverity.Warning, Message = $"Settings: store \"{store}\" is listed twice." });
            }
        }

        private static void ValidateAddressables(OccasionCatalog catalog, string label, List<ValidationIssue> issues)
        {
            if (AddressablesHelper.Settings == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Catalog = catalog,
                    Message = $"{catalog.name}: Addressables isn't set up, so the catalog can't load at runtime.",
                    FixLabel = "Set up",
                    Fix = _ => AddressablesHelper.ShowMakeAddressableMenu(catalog, label),
                });
                return;
            }

            var entry = AddressablesHelper.GetEntry(catalog);

            if (entry == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Catalog = catalog,
                    Message = $"{catalog.name} isn't addressable, so the runtime won't load it. Put it in a remote group to change dates without a new build.",
                    FixLabel = "Make addressable ▾",
                    Fix = _ => AddressablesHelper.ShowMakeAddressableMenu(catalog, label),
                });
            }
            else if (!AddressablesHelper.HasLabel(entry, label))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Catalog = catalog,
                    Message = $"{catalog.name} is addressable but doesn't have the label \"{label}\" the runtime loads.",
                    FixLabel = "Add label",
                    Fix = _ => AddressablesHelper.AddLabel(entry, label),
                });
            }
        }

        private static void ValidateOccasion(OccasionKitSettings settings, OccasionCatalog catalog, int index, Occasion occasion, List<ValidationIssue> issues)
        {
            var name = string.IsNullOrEmpty(occasion.id) ? $"Occasion #{index + 1}" : $"\"{occasion.Title}\"";

            if (string.IsNullOrEmpty(occasion.id))
                Add(issues, IssueSeverity.Error, catalog, index, $"{name} has no id.");
            else if (!OccasionIdPattern.IsMatch(occasion.id))
                Add(issues, IssueSeverity.Warning, catalog, index, $"{name}: use a lowercase id (a-z, 0-9, _ - .) so it reads the same in code and analytics.");

            ValidateSchedule(catalog, index, name, occasion, issues);

            if (occasion.HasIcon)
            {
                if (settings != null && settings.FindIcon(occasion.iconId) == null)
                {
                    Add(issues, IssueSeverity.Error, catalog, index, $"{name} uses icon \"{occasion.iconId}\", which isn't in the icon set.", "Clear icon", _ =>
                    {
                        Undo.RecordObject(catalog, "Clear occasion icon");
                        occasion.iconId = "";
                        EditorUtility.SetDirty(catalog);
                    });
                }
            }

            if (settings != null)
            {
                foreach (var store in occasion.targeting.stores)
                {
                    if (settings.stores.Contains(store, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var missing = store;
                    Add(issues, IssueSeverity.Warning, catalog, index, $"{name} targets store \"{store}\", which isn't in the settings' store list. Typo?", "Add to settings", _ =>
                    {
                        Undo.RecordObject(settings, "Add store");
                        settings.stores.Add(missing);
                        EditorUtility.SetDirty(settings);
                    });
                }
            }

            if (occasion.greeting.enabled && string.IsNullOrWhiteSpace(occasion.greeting.title) && string.IsNullOrWhiteSpace(occasion.greeting.message))
                Add(issues, IssueSeverity.Warning, catalog, index, $"{name} has a greeting with no title or message.");

            if (occasion.notification.enabled)
            {
                if (string.IsNullOrWhiteSpace(occasion.notification.title) && string.IsNullOrWhiteSpace(occasion.notification.body))
                    Add(issues, IssueSeverity.Warning, catalog, index, $"{name} has a notification with no title or body.");
#if !OCCASIONKIT_NOTIFICATIONS
                Add(issues, IssueSeverity.Info, catalog, index, $"{name} has a notification, but com.unity.mobile.notifications isn't installed, so it won't be sent.");
#endif
            }
        }

        private static void ValidateSchedule(OccasionCatalog catalog, int index, string name, Occasion occasion, List<ValidationIssue> issues)
        {
            var s = occasion.schedule;

            if (s.leadDays < 0 || s.tailDays < 0)
                Add(issues, IssueSeverity.Error, catalog, index, $"{name}: lead and tail days can't be negative.");
            else if (s.leadDays > 90 || s.tailDays > 90)
                Add(issues, IssueSeverity.Warning, catalog, index, $"{name}: lead or tail days over 90 make windows that overlap the next year's.");

            if (s.mode == ScheduleMode.Yearly)
            {
                CheckDay(issues, catalog, index, name, "start", s.calendar, s.startMonth, s.startDay);
                CheckDay(issues, catalog, index, name, "end", s.calendar, s.endMonth, s.endDay);

                if (s.startMinute < 0 || s.startMinute > OccasionSchedule.MinutesPerDay || s.endMinute < 0 || s.endMinute > OccasionSchedule.MinutesPerDay)
                    Add(issues, IssueSeverity.Error, catalog, index, $"{name}: times must be between 00:00 and 24:00.");

                var year = CalendarUtility.GetYear(s.calendar, DateTime.Now);

                if (!ScheduleResolver.TryGetYearly(s, year, out _, out _) && !ScheduleResolver.TryGetYearly(s, year + 1, out _, out _))
                    Add(issues, IssueSeverity.Error, catalog, index, $"{name} ends before it starts (same day, end time before start time).");

                if (s.calendar == CalendarType.Hijri)
                    Add(issues, IssueSeverity.Info, catalog, index, $"{name}: arithmetic Hijri dates can be a day or two off the official calendar. Check each year and adjust the offset, or use explicit dates.");
            }
            else
            {
                if (s.ranges.Count == 0)
                {
                    Add(issues, IssueSeverity.Warning, catalog, index, $"{name} has no date ranges.");
                    return;
                }

                var now = DateTime.Now;
                var upcoming = false;

                for (var r = 0; r < s.ranges.Count; r++)
                {
                    var range = s.ranges[r];

                    if (!range.start.TryToDateTime(out var start) || !range.end.TryToDateTime(out var end))
                        Add(issues, IssueSeverity.Error, catalog, index, $"{name}: range {r + 1} has an invalid date.");
                    else if (end <= start)
                        Add(issues, IssueSeverity.Error, catalog, index, $"{name}: range {r + 1} ends before it starts.");
                    else if (end.AddDays(Math.Max(0, s.tailDays)) > now)
                        upcoming = true;
                }

                if (!upcoming && occasion.enabled)
                    Add(issues, IssueSeverity.Warning, catalog, index, $"{name}: every date range is in the past. Add next year's dates.");
            }
        }

        private static void CheckDay(List<ValidationIssue> issues, OccasionCatalog catalog, int index, string name, string which, CalendarType calendar, int month, int day)
        {
            if (month < 1 || month > 12 || day < 1 || day > CalendarUtility.GetMaxDaysInMonth(calendar, month))
            {
                Add(issues, IssueSeverity.Error, catalog, index, $"{name}: the {which} date ({day}/{month}) doesn't exist in the {calendar} calendar.");
                return;
            }

            var leapOnly = (calendar == CalendarType.Persian && month == 12 && day == 30) || (calendar == CalendarType.Gregorian && month == 2 && day == 29);

            if (leapOnly)
                Add(issues, IssueSeverity.Info, catalog, index, $"{name}: {day} {CalendarUtility.GetMonthName(calendar, month)} only exists in leap years; other years use the day before.");
        }

        /// <summary>Two icon occasions overlapping at the same priority: catalog order silently decides which icon shows.</summary>
        private static void ValidateOverlaps(IReadOnlyList<OccasionCatalog> catalogs, List<ValidationIssue> issues)
        {
            var entries = CatalogDatabase.Enumerate().Where(e => e.occasion.enabled && e.occasion.HasIcon).ToList();
            var from = DateTime.Now;
            var to = from.AddYears(2);
            var instances = entries.Select(e => ScheduleResolver.GetInstances(e.occasion, from, to)).ToList();

            for (var a = 0; a < entries.Count; a++)
            {
                for (var b = a + 1; b < entries.Count; b++)
                {
                    if (entries[a].occasion.priority != entries[b].occasion.priority)
                        continue;

                    var overlap = instances[a].FirstOrDefault(x => instances[b].Any(y => x.WindowStart < y.WindowEnd && y.WindowStart < x.WindowEnd));

                    if (overlap == null)
                        continue;

                    Add(issues, IssueSeverity.Warning, entries[b].catalog, entries[b].index,
                        $"\"{entries[a].occasion.Title}\" and \"{entries[b].occasion.Title}\" overlap around {overlap.WindowStart:d MMM yyyy} with the same priority ({entries[a].occasion.priority}); the first in catalog order gets the icon. Give one a higher priority.");
                }
            }
        }

        private static void Add(List<ValidationIssue> issues, IssueSeverity severity, OccasionCatalog catalog, int index, string message, string fixLabel = null, Action<Rect> fix = null)
        {
            issues.Add(new ValidationIssue { Severity = severity, Catalog = catalog, OccasionIndex = index, Message = message, FixLabel = fixLabel, Fix = fix });
        }

        private static void AddIcon(List<ValidationIssue> issues, IssueSeverity severity, int iconIndex, string message)
        {
            issues.Add(new ValidationIssue { Severity = severity, IconIndex = iconIndex, Message = message });
        }
    }
}
