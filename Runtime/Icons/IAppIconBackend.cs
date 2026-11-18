using System.Collections.Generic;

namespace OccasionKit
{
    /// <summary>Switches the app icon on one platform. Icons are built into the app; this only picks one.</summary>
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

    /// <summary>When a backend can switch the icon.</summary>
    public enum IconSwitchMoment
    {
        /// <summary>On the way to the background, so the player never sees it happen (Android). Later changes are scheduled.</summary>
        Background = 0,

        /// <summary>
        /// Only while the app is in front (iOS, which shows an alert each time): shortly after launch and after each return
        /// to the app. Nothing is scheduled; the icon can't change while the app is closed.
        /// </summary>
        Foreground = 1,
    }

    /// <summary>Optional on an <see cref="IAppIconBackend"/>. Backends without it switch in the background.</summary>
    public interface IAppIconSwitchMoment
    {
        IconSwitchMoment SwitchMoment { get; }
    }

    public static class AppIconBackends
    {
        public static IAppIconBackend CreateDefault()
        {
#if UNITY_EDITOR
            return new EditorAppIconBackend();
#elif UNITY_ANDROID
            return new AndroidAppIconBackend();
#elif UNITY_IOS
            return new IOSAppIconBackend();
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
