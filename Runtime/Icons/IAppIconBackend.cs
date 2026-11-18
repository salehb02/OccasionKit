using System.Collections.Generic;

namespace OccasionKit
{
    /// <summary>Switches the launcher icon on one platform. Icons are built into the app; this only picks one.</summary>
    public interface IAppIconBackend
    {
        bool IsSupported { get; }

        /// <summary>Icon ids the installed app has, including <see cref="OccasionSystem.DefaultIconId"/>.</summary>
        IReadOnlyList<string> Icons { get; }

        /// <summary>The icon the launcher shows now, or null when unknown.</summary>
        string CurrentIcon { get; }

        /// <summary>Shows an icon. Unknown ids fall back to the default icon.</summary>
        bool SetIcon(string iconId);

        /// <summary>Applies <paramref name="changes"/> at their times while the app isn't running. Replaces any earlier schedule.</summary>
        void ScheduleChanges(IReadOnlyList<IconChange> changes);

        void CancelScheduledChanges();
    }

    public static class AppIconBackends
    {
        public static IAppIconBackend CreateDefault()
        {
#if UNITY_EDITOR
            return new EditorAppIconBackend();
#elif UNITY_ANDROID
            return new AndroidAppIconBackend();
#else
            return new NullAppIconBackend();
#endif
        }
    }

    public sealed class NullAppIconBackend : IAppIconBackend
    {
        private static readonly string[] DefaultOnly = { OccasionSystem.DefaultIconId };

        public bool IsSupported { get => false; }
        public IReadOnlyList<string> Icons { get => DefaultOnly; }
        public string CurrentIcon { get => OccasionSystem.DefaultIconId; }
        public bool SetIcon(string iconId) => false;
        public void ScheduleChanges(IReadOnlyList<IconChange> changes) { }
        public void CancelScheduledChanges() { }
    }
}
