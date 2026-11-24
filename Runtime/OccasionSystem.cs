using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace OccasionKit
{
    /// <summary>
    /// Entry point. Call <see cref="InitializeAsync"/> once (after any Addressables catalog update), then read
    /// <see cref="Active"/> or listen to <see cref="Changed"/>. On Android the launcher icon is switched when the app goes
    /// to the background, never while it's in front, and upcoming changes are handed to the OS for while it's closed.
    /// iOS only allows switching while the app is in front, so there it happens shortly after launch and after each
    /// return to the app.
    /// </summary>
    public static class OccasionSystem
    {
        public const string DefaultIconId = "default";
        public const string DefaultCatalogLabel = "occasion-catalog";

        private const string PrefsIconsEnabled = "OccasionKit.IconsEnabled";
        private const string PrefsGreetings = "OccasionKit.ShownGreetings";
        private const int MaxRememberedGreetings = 64;

        private static readonly List<Occasion> _occasions = new List<Occasion>();
        private static readonly List<OccasionCatalog> _catalogs = new List<OccasionCatalog>();
        private static AsyncOperationHandle<IList<OccasionCatalog>> _catalogHandle;
        private static Task _initialization;
        private static OccasionConfig _config;
        private static IAppIconBackend _backend;
        private static Evaluation _state;
        private static DateTime _nextBoundary = DateTime.MaxValue;
        private static string _signature;
        private static bool _catalogsLoaded;
        private static bool? _iconsEnabled;
        private static bool _applyPending;

        public static bool IsInitialized { get; private set; }

        /// <summary>Store and language occasions are matched against. Null before initialization.</summary>
        public static OccasionContext Context { get; private set; }

        public static IReadOnlyList<OccasionCatalog> Catalogs { get => _catalogs; }

        /// <summary>Every occasion from every loaded catalog (first id wins on duplicates).</summary>
        public static IReadOnlyList<Occasion> Occasions { get => _occasions; }

        /// <summary>The last evaluation: active and suppressed occasions, winning icon.</summary>
        public static Evaluation State { get => _state; }

        /// <summary>Active occurrences, highest priority first.</summary>
        public static IReadOnlyList<OccasionInstance> Active { get => _state != null ? _state.Active : (IReadOnlyList<OccasionInstance>)Array.Empty<OccasionInstance>(); }

        /// <summary>The highest-priority active occurrence, or null.</summary>
        public static OccasionInstance Primary { get => _state?.Primary; }

        /// <summary>The icon the launcher should show now.</summary>
        public static string TargetIcon { get => (SeasonalIconsEnabled ? _state?.IconId : null) ?? DefaultIconId; }

        public static IAppIconBackend IconBackend { get => _backend; }

        /// <summary>Raised when the active occasions or the target icon change, and once after initialization.</summary>
        public static event Action Changed;

        /// <summary>Raised after the launcher icon was switched: (from, to). From is null when unknown.</summary>
        public static event Action<string, string> IconApplied;

        /// <summary>Raised when the app goes to the background, after the icon is applied. Add-ons schedule their work here.</summary>
        public static event Action GoingToBackground;

        /// <summary>Player setting. Off: the default icon is kept. Occasions and themes are not affected.</summary>
        public static bool SeasonalIconsEnabled
        {
            get
            {
                if (_iconsEnabled == null)
                    _iconsEnabled = PlayerPrefs.GetInt(PrefsIconsEnabled, 1) == 1;

                return _iconsEnabled.Value;
            }
            set
            {
                if (value == SeasonalIconsEnabled)
                    return;

                _iconsEnabled = value;
                PlayerPrefs.SetInt(PrefsIconsEnabled, value ? 1 : 0);
                PlayerPrefs.Save();
                Refresh(true);

                // The player just changed it in the app, so on iOS show the result (and its alert) right away.
                if (SwitchesInForeground)
                    _applyPending = true;
            }
        }

        public static Task InitializeAsync(OccasionConfig config = null)
        {
            if (_initialization == null)
                _initialization = InitializeCore(config ?? new OccasionConfig());

            return _initialization;
        }

        /// <summary>Reloads the catalogs, e.g. after an Addressables catalog update in a long session.</summary>
        public static async Task ReloadCatalogsAsync()
        {
            if (!IsInitialized)
                return;

            await LoadCatalogsAsync();
            Refresh(true);
        }

        /// <summary>Re-evaluates now. Happens on its own at every window boundary and when the app returns to the foreground.</summary>
        public static void Refresh() => Refresh(false);

        public static Occasion Find(string id)
        {
            foreach (var occasion in _occasions)
            {
                if (occasion.id == id)
                    return occasion;
            }

            return null;
        }

        public static bool IsActive(string id) => GetActive(id) != null;

        public static OccasionInstance GetActive(string id)
        {
            if (_state == null)
                return null;

            foreach (var instance in _state.Active)
            {
                if (instance.Occasion.id == id)
                    return instance;
            }

            return null;
        }

        /// <summary>The next occurrence that hasn't started yet, or null. Ignores occasions this store or language doesn't run.</summary>
        public static OccasionInstance GetNext(string id)
        {
            var occasion = Find(id);

            if (occasion == null || !occasion.enabled || !occasion.targeting.Matches(Context))
                return null;

            return ScheduleResolver.GetNextInstance(occasion, OccasionClock.Now);
        }

        /// <summary>Occurrences that start within <paramref name="within"/>, soonest first.</summary>
        public static List<OccasionInstance> GetUpcoming(TimeSpan within)
        {
            var now = OccasionClock.Now;
            var results = new List<OccasionInstance>();
            var instances = new List<OccasionInstance>();

            foreach (var occasion in _occasions)
            {
                if (!occasion.enabled || !occasion.targeting.Matches(Context))
                    continue;

                instances.Clear();
                ScheduleResolver.GetInstances(occasion, now, now + within, instances);
                results.AddRange(instances.Where(i => i.WindowStart > now));
            }

            results.Sort((a, b) => a.WindowStart.CompareTo(b.WindowStart));
            return results;
        }

        /// <summary>The highest-priority active occurrence that has a theme, or null.</summary>
        public static OccasionInstance GetActiveTheme()
        {
            foreach (var instance in Active)
            {
                if (instance.Occasion.HasTheme)
                    return instance;
            }

            return null;
        }

        /// <summary>Loads an occasion's theme asset. Release the handle with Addressables.Release when done.</summary>
        public static AsyncOperationHandle<T> LoadThemeAsync<T>(Occasion occasion) where T : UnityEngine.Object
        {
            if (occasion == null || !occasion.HasTheme)
                throw new ArgumentException($"Occasion \"{occasion?.id}\" has no theme.", nameof(occasion));

            return Addressables.LoadAssetAsync<T>(occasion.theme.RuntimeKey);
        }

        /// <summary>Active occurrences with a greeting that wasn't shown yet. Show them, then call <see cref="MarkGreetingShown"/>.</summary>
        public static List<OccasionInstance> GetPendingGreetings()
        {
            var results = new List<OccasionInstance>();

            if (_state == null)
                return results;

            var shown = LoadShownGreetings();
            var now = OccasionClock.Now;

            foreach (var instance in _state.Active)
            {
                var greeting = instance.Occasion.greeting;

                if (greeting == null || !greeting.enabled || shown.Contains(instance.Key))
                    continue;

                if (!greeting.includeLeadAndTail && instance.GetPhase(now) != OccasionPhase.Main)
                    continue;

                results.Add(instance);
            }

            return results;
        }

        /// <summary>
        /// A greeting's title and message for this player: the Unity Localization strings in the selected locale when
        /// that package is installed, otherwise the translation for <see cref="OccasionConfig.Language"/> (default: the
        /// device language), otherwise the default text. Pass <paramref name="language"/> to override the language.
        /// </summary>
        public static (string title, string message) GetGreetingText(OccasionInstance instance, string language = null)
        {
            var greeting = instance?.Occasion?.greeting;
            return greeting != null ? greeting.GetText(language ?? Context?.Language) : (null, null);
        }

        public static void MarkGreetingShown(OccasionInstance instance)
        {
            if (instance == null)
                return;

            var shown = LoadShownGreetings();

            if (shown.Contains(instance.Key))
                return;

            shown.Add(instance.Key);

            if (shown.Count > MaxRememberedGreetings)
                shown.RemoveRange(0, shown.Count - MaxRememberedGreetings);

            PlayerPrefs.SetString(PrefsGreetings, string.Join("|", shown));
            PlayerPrefs.Save();
        }

        public static void ResetGreetings()
        {
            PlayerPrefs.DeleteKey(PrefsGreetings);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Switches the icon right away instead of on the next trip to the background (Android) or return to the app
        /// (iOS). On Android some launchers restart the app when its icon changes, so only use it from a debug menu there.
        /// </summary>
        public static bool ApplyIconNow()
        {
            if (!CanManageIcon)
                return false;

            Refresh(false);
            return ApplyIcon();
        }

        private static async Task InitializeCore(OccasionConfig config)
        {
            _config = config;
            Context = new OccasionContext(config.Store, string.IsNullOrEmpty(config.Language) ? LanguageCodes.FromDevice() : LanguageCodes.Normalize(config.Language));
            _backend = config.IconBackend ?? AppIconBackends.CreateDefault();
            OccasionSystemRunner.Ensure();

            await LoadCatalogsAsync();

            IsInitialized = true;

            // The app is in the foreground; scheduled changes are only for while it's closed.
            if (CanManageIcon)
                _backend.CancelScheduledChanges();

            Refresh(true);
            _applyPending = SwitchesInForeground;
            Log($"Ready: {_occasions.Count} occasions from {_catalogs.Count} catalogs, {Context}, icons: {string.Join(", ", _backend.Icons)}");
        }

        private static async Task LoadCatalogsAsync()
        {
            if (_catalogHandle.IsValid())
                Addressables.Release(_catalogHandle);

            _catalogHandle = default;
            _catalogs.Clear();
            _catalogsLoaded = false;

            if (_config.Catalogs != null)
            {
                _catalogs.AddRange(_config.Catalogs.Where(c => c != null));
                _catalogsLoaded = true;
            }
            else if (!string.IsNullOrEmpty(_config.CatalogKey))
            {
                try
                {
                    var locationsHandle = Addressables.LoadResourceLocationsAsync(_config.CatalogKey, typeof(OccasionCatalog));
                    await locationsHandle.Task;

                    var locations = locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result != null
                        ? new List<IResourceLocation>(locationsHandle.Result)
                        : null;

                    Addressables.Release(locationsHandle);

                    if (locations == null)
                    {
                        Debug.LogWarning($"[OccasionKit] Looking up occasion catalogs \"{_config.CatalogKey}\" failed.");
                    }
                    else if (locations.Count == 0)
                    {
                        Debug.LogWarning($"[OccasionKit] No OccasionCatalog is addressable with the key \"{_config.CatalogKey}\". Window > OccasionKit sets one up.");
                        _catalogsLoaded = true;
                    }
                    else
                    {
                        _catalogHandle = Addressables.LoadAssetsAsync<OccasionCatalog>(locations, null, true);
                        await _catalogHandle.Task;

                        if (_catalogHandle.Status == AsyncOperationStatus.Succeeded)
                        {
                            _catalogs.AddRange(_catalogHandle.Result.Where(c => c != null));
                            _catalogsLoaded = true;
                        }
                        else
                        {
                            Debug.LogWarning($"[OccasionKit] Loading occasion catalogs failed: {_catalogHandle.OperationException?.Message}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _catalogs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            RebuildOccasions();
        }

        private static void RebuildOccasions()
        {
            _occasions.Clear();
            var ids = new HashSet<string>();

            foreach (var catalog in _catalogs)
            {
                foreach (var occasion in catalog.occasions)
                {
                    if (occasion == null || string.IsNullOrEmpty(occasion.id))
                        continue;

                    if (!ids.Add(occasion.id))
                    {
                        Debug.LogWarning($"[OccasionKit] Occasion id \"{occasion.id}\" is used twice; the copy in {catalog.name} is ignored.");
                        continue;
                    }

                    _occasions.Add(occasion);
                }
            }
        }

        private static void Refresh(bool force)
        {
            if (!IsInitialized)
                return;

            var now = OccasionClock.Now;
            _state = OccasionEvaluator.Evaluate(_occasions, now, Context, IsIconAvailable);
            _nextBoundary = OccasionEvaluator.GetNextBoundary(_occasions, now, now.AddDays(400), Context) ?? DateTime.MaxValue;

            var signature = string.Join(",", _state.Active.Select(a => a.Key)) + "|" + TargetIcon;

            if (!force && signature == _signature)
                return;

            _signature = signature;
            Log($"Active: [{string.Join(", ", _state.Active.Select(a => a.Id))}], icon: {TargetIcon}, next check: {(_nextBoundary == DateTime.MaxValue ? "-" : _nextBoundary.ToString("yyyy-MM-dd HH:mm"))}");
            Raise(Changed);
        }

        /// <summary>Called about once a second from Update, so only while the app is active.</summary>
        internal static void Tick()
        {
            if (!IsInitialized)
                return;

            if (OccasionClock.Now >= _nextBoundary)
                Refresh(false);

            // iOS: after launch or a return to the app. A boundary passing mid-session waits for the next return,
            // so the system alert doesn't interrupt play.
            if (_applyPending)
            {
                _applyPending = false;

                if (CanManageIcon)
                    ApplyIcon();
            }
        }

        internal static void OnBackground()
        {
            if (!IsInitialized)
                return;

            Refresh(false);
            _applyPending = false;

            if (CanManageIcon && !SwitchesInForeground)
            {
                ApplyIcon();
                ScheduleIconChanges();
            }

            Raise(GoingToBackground);
        }

        internal static void OnForeground()
        {
            if (!IsInitialized)
                return;

            if (CanManageIcon)
                _backend.CancelScheduledChanges();

            Refresh(false);
            _applyPending = SwitchesInForeground;
        }

        /// <summary>The backend can only switch while the app is in front (iOS).</summary>
        private static bool SwitchesInForeground { get => _backend is IAppIconSwitchMoment timing && timing.SwitchMoment == IconSwitchMoment.Foreground; }

        /// <summary>Loading the catalogs failed: keep whatever icon is showing rather than reverting it.</summary>
        private static bool CanManageIcon { get => _config != null && _config.ManageAppIcon && _catalogsLoaded && _backend != null && _backend.IsSupported; }

        private static bool IsIconAvailable(string iconId)
        {
            if (_backend == null || !_backend.IsSupported)
                return true;

            var icons = _backend.Icons;

            for (var i = 0; i < icons.Count; i++)
            {
                if (icons[i] == iconId)
                    return true;
            }

            return false;
        }

        private static bool ApplyIcon()
        {
            var target = TargetIcon;
            var current = _backend.CurrentIcon;

            if (current == target)
                return true;

            if (!_backend.SetIcon(target))
                return false;

            Log($"Icon {current ?? "?"} → {target}");

            foreach (var handler in IconApplied?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    ((Action<string, string>)handler)(current, target);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return true;
        }

        private static void ScheduleIconChanges()
        {
            var now = OccasionClock.Now;
            var changes = SeasonalIconsEnabled
                ? OccasionEvaluator.GetIconChanges(_occasions, now, now.AddDays(_config.ScheduleDays), Context, IsIconAvailable, DefaultIconId, _config.MaxScheduledChanges)
                : new List<IconChange>();

            _backend.ScheduleChanges(changes);

            if (changes.Count > 0)
                Log($"Scheduled icon changes: {string.Join("; ", changes)}");
        }

        private static List<string> LoadShownGreetings()
        {
            var value = PlayerPrefs.GetString(PrefsGreetings, string.Empty);
            return string.IsNullOrEmpty(value) ? new List<string>() : value.Split('|').ToList();
        }

        private static void Raise(Action action)
        {
            foreach (var handler in action?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static void Log(string message)
        {
            if (_config != null && _config.Verbose)
                Debug.Log($"[OccasionKit] {message}");
        }

        // Statics survive Play Mode when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            _occasions.Clear();
            _catalogs.Clear();
            _catalogHandle = default;
            _initialization = null;
            _config = null;
            _backend = null;
            _state = null;
            _nextBoundary = DateTime.MaxValue;
            _signature = null;
            _catalogsLoaded = false;
            _iconsEnabled = null;
            _applyPending = false;
            IsInitialized = false;
            Context = null;
            Changed = null;
            IconApplied = null;
            GoingToBackground = null;
        }
    }
}
