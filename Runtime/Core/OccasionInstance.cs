using System;

namespace OccasionKit
{
    public enum OccasionPhase
    {
        Lead = 0,
        Main = 1,
        Tail = 2,
    }

    /// <summary>One occurrence of an occasion, e.g. Nowruz 1406. All times are device local time.</summary>
    public sealed class OccasionInstance
    {
        public Occasion Occasion { get; }
        /// <summary>Main start minus lead days.</summary>
        public DateTime WindowStart { get; }
        public DateTime MainStart { get; }
        public DateTime MainEnd { get; }
        /// <summary>Main end plus tail days. Exclusive.</summary>
        public DateTime WindowEnd { get; }
        public DateTime CountdownTarget { get; }

        /// <summary>Unique per occurrence, e.g. "nowruz@20270321". Used to remember shown greetings.</summary>
        public string Key { get; }

        public string Id { get => Occasion.id; }

        public OccasionInstance(Occasion occasion, DateTime windowStart, DateTime mainStart, DateTime mainEnd, DateTime windowEnd, DateTime countdownTarget)
        {
            Occasion = occasion;
            WindowStart = windowStart;
            MainStart = mainStart;
            MainEnd = mainEnd;
            WindowEnd = windowEnd;
            CountdownTarget = countdownTarget;
            Key = $"{occasion.id}@{mainStart:yyyyMMdd}";
        }

        public bool Contains(DateTime time) => time >= WindowStart && time < WindowEnd;

        public OccasionPhase GetPhase(DateTime time)
        {
            if (time < MainStart)
                return OccasionPhase.Lead;

            return time < MainEnd ? OccasionPhase.Main : OccasionPhase.Tail;
        }

        /// <summary>Time left until <see cref="CountdownTarget"/>; zero once it has passed.</summary>
        public TimeSpan GetCountdown(DateTime now) => CountdownTarget > now ? CountdownTarget - now : TimeSpan.Zero;

        public override string ToString() => $"{Occasion.Title} [{WindowStart:yyyy-MM-dd HH:mm} → {WindowEnd:yyyy-MM-dd HH:mm})";
    }
}
