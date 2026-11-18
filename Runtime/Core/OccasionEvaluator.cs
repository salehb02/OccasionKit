using System;
using System.Collections.Generic;
using System.Linq;

namespace OccasionKit
{
    public enum SuppressionReason
    {
        Disabled = 0,
        Targeting = 1,
        /// <summary>A higher-priority exclusive occasion is active.</summary>
        Exclusive = 2,
    }

    public readonly struct SuppressedOccasion
    {
        public readonly OccasionInstance Instance;
        public readonly SuppressionReason Reason;
        /// <summary>The exclusive occasion that suppressed it, for <see cref="SuppressionReason.Exclusive"/>.</summary>
        public readonly Occasion By;

        public SuppressedOccasion(OccasionInstance instance, SuppressionReason reason, Occasion by)
        {
            Instance = instance;
            Reason = reason;
            By = by;
        }
    }

    public readonly struct IconChange
    {
        public readonly DateTime Time;
        public readonly string IconId;

        public IconChange(DateTime time, string iconId)
        {
            Time = time;
            IconId = iconId;
        }

        public override string ToString() => $"{Time:yyyy-MM-dd HH:mm} → {IconId}";
    }

    public sealed class Evaluation
    {
        public DateTime Time { get; internal set; }
        /// <summary>Active occurrences after targeting and exclusivity, highest priority first.</summary>
        public List<OccasionInstance> Active { get; } = new List<OccasionInstance>();
        public List<SuppressedOccasion> Suppressed { get; } = new List<SuppressedOccasion>();
        /// <summary>The icon to show, or null for the default icon.</summary>
        public string IconId { get; internal set; }
        /// <summary>The occurrence the icon comes from, or null.</summary>
        public OccasionInstance IconSource { get; internal set; }

        public OccasionInstance Primary { get => Active.Count > 0 ? Active[0] : null; }
    }

    /// <summary>Pure rules: which occasions are active at a moment and which icon wins. No Unity state.</summary>
    public static class OccasionEvaluator
    {
        /// <param name="isIconAvailable">Whether the installed app has an icon. Null: every icon is available.</param>
        public static Evaluation Evaluate(IReadOnlyList<Occasion> occasions, DateTime time, OccasionContext context, Func<string, bool> isIconAvailable = null)
        {
            var result = new Evaluation { Time = time };
            var candidates = new List<OccasionInstance>();

            for (var i = 0; i < occasions.Count; i++)
            {
                var occasion = occasions[i];
                var instance = occasion == null ? null : ScheduleResolver.GetInstanceAt(occasion, time);

                if (instance == null)
                    continue;

                if (!occasion.enabled)
                    result.Suppressed.Add(new SuppressedOccasion(instance, SuppressionReason.Disabled, null));
                else if (occasion.targeting != null && !occasion.targeting.Matches(context))
                    result.Suppressed.Add(new SuppressedOccasion(instance, SuppressionReason.Targeting, null));
                else
                    candidates.Add(instance);
            }

            // OrderByDescending is stable: equal priorities keep catalog order.
            Occasion exclusive = null;

            foreach (var instance in candidates.OrderByDescending(c => c.Occasion.priority))
            {
                if (exclusive != null)
                {
                    result.Suppressed.Add(new SuppressedOccasion(instance, SuppressionReason.Exclusive, exclusive));
                    continue;
                }

                result.Active.Add(instance);

                if (instance.Occasion.exclusive)
                    exclusive = instance.Occasion;
            }

            foreach (var instance in result.Active)
            {
                if (!instance.Occasion.HasIcon || (isIconAvailable != null && !isIconAvailable(instance.Occasion.iconId)))
                    continue;

                result.IconId = instance.Occasion.iconId;
                result.IconSource = instance;
                break;
            }

            return result;
        }

        /// <summary>
        /// The icon changes between <paramref name="from"/> and <paramref name="to"/>, in order. The default icon is
        /// <paramref name="defaultIconId"/>. Used to hand the OS a schedule for while the app isn't running.
        /// </summary>
        public static List<IconChange> GetIconChanges(IReadOnlyList<Occasion> occasions, DateTime from, DateTime to, OccasionContext context, Func<string, bool> isIconAvailable, string defaultIconId, int maxChanges = int.MaxValue)
        {
            var changes = new List<IconChange>();
            var current = Evaluate(occasions, from, context, isIconAvailable).IconId ?? defaultIconId;

            foreach (var boundary in GetBoundaries(occasions, from, to, context))
            {
                var icon = Evaluate(occasions, boundary, context, isIconAvailable).IconId ?? defaultIconId;

                if (icon == current)
                    continue;

                changes.Add(new IconChange(boundary, icon));
                current = icon;

                if (changes.Count >= maxChanges)
                    break;
            }

            return changes;
        }

        /// <summary>The first moment after <paramref name="after"/> where any window opens or closes, or null.</summary>
        public static DateTime? GetNextBoundary(IReadOnlyList<Occasion> occasions, DateTime after, DateTime limit, OccasionContext context)
        {
            foreach (var boundary in GetBoundaries(occasions, after, limit, context))
                return boundary;

            return null;
        }

        /// <summary>Sorted, distinct window starts and ends in (<paramref name="from"/>, <paramref name="to"/>].</summary>
        public static List<DateTime> GetBoundaries(IReadOnlyList<Occasion> occasions, DateTime from, DateTime to, OccasionContext context)
        {
            var set = new SortedSet<DateTime>();
            var instances = new List<OccasionInstance>();

            foreach (var occasion in occasions)
            {
                if (occasion == null || !occasion.enabled || (occasion.targeting != null && !occasion.targeting.Matches(context)))
                    continue;

                instances.Clear();
                ScheduleResolver.GetInstances(occasion, from, to.AddTicks(1), instances);

                foreach (var instance in instances)
                {
                    if (instance.WindowStart > from && instance.WindowStart <= to)
                        set.Add(instance.WindowStart);

                    if (instance.WindowEnd > from && instance.WindowEnd <= to)
                        set.Add(instance.WindowEnd);
                }
            }

            return set.ToList();
        }
    }
}
