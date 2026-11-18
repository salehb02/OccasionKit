using System;

namespace OccasionKit
{
    /// <summary>
    /// The device clock, with an optional simulated offset for testing. Occasions are always evaluated in device local
    /// time; there is no server time.
    /// </summary>
    public static class OccasionClock
    {
        private static TimeSpan _offset;

        public static bool IsSimulated { get; private set; }

        public static DateTime Now { get => IsSimulated ? DateTime.Now + _offset : DateTime.Now; }

        /// <summary>Makes <see cref="Now"/> read <paramref name="time"/> from this moment on; the clock keeps running.</summary>
        public static void Simulate(DateTime time)
        {
            _offset = time - DateTime.Now;
            IsSimulated = true;
        }

        public static void ClearSimulation()
        {
            _offset = TimeSpan.Zero;
            IsSimulated = false;
        }

        /// <summary>Real wall-clock time of a moment on this clock. Used to hand times to the OS (alarms, notifications).</summary>
        public static DateTime ToDeviceTime(DateTime clockTime) => IsSimulated ? clockTime - _offset : clockTime;
    }
}
