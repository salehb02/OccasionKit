using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OccasionKit.EditorTools
{
    /// <summary>Checks calendars, rules, image processing and the Android and iOS build steps against known values.</summary>
    internal static class OccasionKitSelfTest
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _passed;
        private static int _failed;

        [MenuItem("Tools/OccasionKit/Run Self-Test", priority = 2000)]
        public static void Run()
        {
            Report.Clear();
            _passed = 0;
            _failed = 0;

            Safe("Calendars", Calendars);
            Safe("Schedules", Schedules);
            Safe("Rules", Rules);
            Safe("Parsing and languages", Parsing);
            Safe("Translations", Translations);
            Safe("Runtime facade", RuntimeFacade);
            Safe("iOS icon timing", ForegroundSwitching);
            Safe("Images", Images);
            Safe("Android build step", BuildStep);
            Safe("iOS build step", IOSBuildStep);

            var summary = $"[OccasionKit] Self-test: {_passed} passed, {_failed} failed\n{Report}";

            if (_failed > 0)
                Debug.LogError(summary);
            else
                Debug.Log(summary);
        }

        private static void Calendars()
        {
            // Published March equinox times (UTC).
            Near("Equinox 2025", Equinox.MarchUtc(2025), new DateTime(2025, 3, 20, 9, 1, 0), 3);
            Near("Equinox 2026", Equinox.MarchUtc(2026), new DateTime(2026, 3, 20, 14, 46, 0), 3);
            Near("Equinox 2027", Equinox.MarchUtc(2027), new DateTime(2027, 3, 20, 20, 25, 0), 3);

            Date("1 Farvardin 1405", CalendarType.Persian, 1405, 1, 1, new DateTime(2026, 3, 21));
            Date("1 Farvardin 1406", CalendarType.Persian, 1406, 1, 1, new DateTime(2027, 3, 21));
            Date("30 Azar 1405 (Yalda)", CalendarType.Persian, 1405, 9, 30, new DateTime(2026, 12, 21));
            Date("Esfand 30 in a common year clamps to 29", CalendarType.Persian, 1405, 12, 30, new DateTime(2027, 3, 20));

            CalendarUtility.TryToDateTime(CalendarType.Hijri, 1448, 9, 1, out var ramadan);
            Note($"1 Ramadan 1448 (arithmetic) = {ramadan:yyyy-MM-dd}");
        }

        private static void Schedules()
        {
            var christmas = Yearly("xmas", CalendarType.Gregorian, 12, 20, 1, 2, 0, "");
            var instances = ScheduleResolver.GetInstances(christmas, new DateTime(2026, 1, 1), new DateTime(2027, 1, 1));
            Check("Wrapping window: two occurrences touch 2026", instances.Count == 2, string.Join("; ", instances));
            Check("Wrapping window ends 3 Jan 00:00", instances.Count == 2 && instances[1].MainEnd == new DateTime(2027, 1, 3), instances.LastOrDefault()?.ToString());

            var nowruz = Yearly("nowruz", CalendarType.Persian, 1, 1, 1, 12, 0, "");
            nowruz.schedule.leadDays = 7;
            nowruz.schedule.countdown = CountdownTarget.MarchEquinox;
            var next = ScheduleResolver.GetNextInstance(nowruz, new DateTime(2026, 11, 17));
            Check("Nowruz 1406 main start", next != null && next.MainStart == new DateTime(2027, 3, 21), next?.ToString());
            Check("Nowruz lead days", next != null && next.WindowStart == new DateTime(2027, 3, 14), next?.WindowStart.ToString("u"));
            Near("Nowruz countdown is the equinox", next?.CountdownTarget.ToUniversalTime() ?? default, new DateTime(2027, 3, 20, 20, 25, 0), 3);

            var timed = Yearly("timed", CalendarType.Gregorian, 3, 20, 3, 20, 0, "");
            timed.schedule.startMinute = 18 * 60;
            timed.schedule.endMinute = 20 * 60;
            var t = ScheduleResolver.GetInstanceAt(timed, new DateTime(2027, 3, 20, 19, 0, 0));
            Check("Time of day window", t != null && ScheduleResolver.GetInstanceAt(timed, new DateTime(2027, 3, 20, 20, 0, 0)) == null);

            var explicitDates = new Occasion { id = "ex", schedule = new OccasionSchedule { mode = ScheduleMode.Explicit } };
            explicitDates.schedule.ranges.Add(new DateRange { start = new LocalDateTime(new DateTime(2027, 2, 8)), end = new LocalDateTime(new DateTime(2027, 3, 10)) });
            Check("Explicit range", ScheduleResolver.GetInstanceAt(explicitDates, new DateTime(2027, 3, 1)) != null && ScheduleResolver.GetInstanceAt(explicitDates, new DateTime(2027, 3, 10)) == null);
        }

        private static void Rules()
        {
            var halloween = Yearly("halloween", CalendarType.Gregorian, 10, 25, 11, 5, 10, "halloween");
            var cup = Yearly("cup", CalendarType.Gregorian, 10, 30, 11, 2, 20, "cup");
            var mourning = Yearly("mourning", CalendarType.Gregorian, 11, 1, 11, 1, 100, "");
            mourning.exclusive = true;
            var bonus = Yearly("bonus", CalendarType.Gregorian, 10, 1, 11, 30, 50, "");
            var all = new List<Occasion> { halloween, cup, mourning, bonus };

            Icon("Only Halloween has an icon", all, new DateTime(2026, 10, 26), "halloween");
            Icon("Higher priority icon wins", all, new DateTime(2026, 10, 31), "cup");
            Icon("Exclusive occasion hides the rest", all, new DateTime(2026, 11, 1, 12, 0, 0), null);
            Icon("Back after the exclusive day", all, new DateTime(2026, 11, 2, 12, 0, 0), "cup");

            var blocked = OccasionEvaluator.Evaluate(all, new DateTime(2026, 11, 1, 12, 0, 0), OccasionContext.Any);
            Check("Exclusive suppresses all lower ones", blocked.Active.Count == 1 && blocked.Suppressed.Count(s => s.Reason == SuppressionReason.Exclusive) == 3);

            var missing = OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 31), OccasionContext.Any, id => id != "cup");
            Check("Icon missing from the build falls through", missing.IconId == "halloween", missing.IconId);

            var changes = OccasionEvaluator.GetIconChanges(all, new DateTime(2026, 10, 20), new DateTime(2026, 12, 1), OccasionContext.Any, null, "default");
            var expected = "2026-10-25 00:00 → halloween|2026-10-30 00:00 → cup|2026-11-01 00:00 → default|2026-11-02 00:00 → cup|2026-11-03 00:00 → halloween|2026-11-06 00:00 → default";
            Check("Icon change schedule", string.Join("|", changes) == expected, string.Join("|", changes));

            halloween.targeting.stores.Add("GooglePlay");
            var bazaar = new OccasionContext("CafeBazaar", "fa");
            var googlePlay = new OccasionContext("googleplay", "en");
            Check("Store targeting excludes", OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 26), bazaar).IconId == null);
            Check("Store targeting is case-insensitive", OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 26), googlePlay).IconId == "halloween");

            halloween.targeting.stores.Clear();
            halloween.targeting.languages.Add("en");
            Check("Language targeting", OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 26), new OccasionContext(null, "fa")).IconId == null
                                        && OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 26), new OccasionContext(null, "en-US")).IconId == "halloween");

            halloween.enabled = false;
            Check("Disabled occasion", OccasionEvaluator.Evaluate(all, new DateTime(2026, 10, 26), OccasionContext.Any).IconId == null);
        }

        private static void Parsing()
        {
            Check("Parse 24:00", LocalDateTime.TryParse("2027-03-20 24:00", out var end) && end.TryToDateTime(out var endTime) && endTime == new DateTime(2027, 3, 21));
            Check("Reject 30 February", !LocalDateTime.TryParse("2027-02-30", out _));
            Check("Time parse", OccasionSchedule.TryParseTime("18:30", out var m) && m == 1110 && !OccasionSchedule.TryParseTime("25:00", out _));
            Check("Language prefix match", LanguageCodes.Matches("fa", "fa-IR") && !LanguageCodes.Matches("fa-IR", "fa") && LanguageCodes.Matches("FA", "fa_ir"));
            Check("Retired Java codes", LanguageCodes.Normalize("iw") == "he" && LanguageCodes.Normalize("in-ID") == "id-id");
            Check("Icon id rules", OccasionValidator.IsValidIconId("nowruz_1405") && !OccasionValidator.IsValidIconId("Nowruz") && !OccasionValidator.IsValidIconId("default") && !OccasionValidator.IsValidIconId("1abc"));
            Note("Device language here: " + LanguageCodes.FromDevice());
        }

        private static void Translations()
        {
            var greeting = new OccasionGreeting { title = "Happy Nowruz", message = "From all of us" };
            greeting.translations.Add(new OccasionTranslation { language = "fa", title = "نوروز مبارک", text = "از طرف همه ما" });
            greeting.translations.Add(new OccasionTranslation { language = "fa-AF", title = "نوروز تان مبارک" });

            Check("Default text without a language", greeting.GetText(null) == ("Happy Nowruz", "From all of us"));
            Check("Default text for an untranslated language", greeting.GetText("de") == ("Happy Nowruz", "From all of us"));
            Check("Base language covers regions", greeting.GetText("fa-IR") == ("نوروز مبارک", "از طرف همه ما"));
            Check("Region before base language", greeting.GetText("fa-AF").title == "نوروز تان مبارک");
            Check("Empty translated field falls back to the default", greeting.GetText("fa-AF").message == "From all of us");
            Check("Codes are normalized", greeting.GetText("FA_ir").title == "نوروز مبارک");

            var notification = new OccasionNotification { title = "Nowruz", body = "Tap to celebrate" };
            notification.translations.Add(new OccasionTranslation { language = "fa", title = "نوروز" });
            Check("Notification text", notification.GetText("fa") == ("نوروز", "Tap to celebrate"));
#if OCCASIONKIT_LOCALIZATION
            Note("Unity Localization is installed: set localized strings come before these translations.");
#endif
        }

        private sealed class FakeBackend : IAppIconBackend, IAppIconSwitchMoment
        {
            public readonly List<string> Applied = new List<string>();
            public IReadOnlyList<IconChange> LastSchedule = Array.Empty<IconChange>();
            public int Cancels;

            public bool IsSupported { get => true; }
            public IconSwitchMoment SwitchMoment { get; set; } = IconSwitchMoment.Background;
            public IReadOnlyList<string> Icons { get; } = new[] { "default", "halloween", "cup" };
            public string CurrentIcon { get; private set; } = "default";

            public bool SetIcon(string iconId)
            {
                Applied.Add(iconId);
                CurrentIcon = iconId;
                return true;
            }

            public void ScheduleChanges(IReadOnlyList<IconChange> changes) => LastSchedule = changes;
            public void CancelScheduledChanges() => Cancels++;
        }

        private static void RuntimeFacade()
        {
            if (Application.isPlaying)
            {
                Note("Skipped in Play Mode (it would reset the running OccasionSystem).");
                return;
            }

            var catalog = ScriptableObject.CreateInstance<OccasionCatalog>();
            var halloween = Yearly("halloween", CalendarType.Gregorian, 10, 25, 11, 5, 10, "halloween");
            halloween.greeting.enabled = true;
            halloween.greeting.title = "Boo";
            halloween.greeting.translations.Add(new OccasionTranslation { language = "fa", title = "Boo!" });
            var cup = Yearly("cup", CalendarType.Gregorian, 10, 30, 11, 2, 20, "cup");
            cup.targeting.stores.Add("GooglePlay");
            var mourning = Yearly("mourning", CalendarType.Gregorian, 11, 1, 11, 1, 100, "");
            mourning.exclusive = true;
            var unknownIcon = Yearly("future", CalendarType.Gregorian, 10, 1, 12, 31, 500, "not_in_this_build");
            catalog.occasions.AddRange(new[] { halloween, cup, mourning, unknownIcon });

            var backend = new FakeBackend();
            var changed = 0;
            OccasionSystem.ResetStatics();
            OccasionClock.Simulate(new DateTime(2026, 10, 31, 12, 0, 0));

            try
            {
                OccasionSystem.Changed += () => changed++;
                var init = OccasionSystem.InitializeAsync(new OccasionConfig { Catalogs = new[] { catalog }, IconBackend = backend, Store = "CafeBazaar", Language = "fa" });
                Check("Initializes with given catalogs", init.IsCompleted && OccasionSystem.IsInitialized && OccasionSystem.Occasions.Count == 4);
                Check("Same task on a second call", OccasionSystem.InitializeAsync() == init);
                Check("Changed raised on init", changed == 1, changed.ToString());
                Check("Store targeting applied (no cup on CafeBazaar)", !OccasionSystem.IsActive("cup") && OccasionSystem.IsActive("halloween"));
                Check("Icon missing from the build is skipped", OccasionSystem.TargetIcon == "halloween", OccasionSystem.TargetIcon);
                Check("No switch while in the foreground", backend.Applied.Count == 0 && backend.Cancels == 1);

                OccasionSystem.OnBackground();
                Check("Switches when going to the background", backend.Applied.SequenceEqual(new[] { "halloween" }), string.Join(",", backend.Applied));
                Check("Schedules the following changes", string.Join("|", backend.LastSchedule.Take(3)) == "2026-11-01 00:00 → default|2026-11-02 00:00 → halloween|2026-11-06 00:00 → default", string.Join("|", backend.LastSchedule));

                OccasionSystem.OnForeground();
                Check("Cancels the schedule in the foreground", backend.Cancels == 2);

                var greetings = OccasionSystem.GetPendingGreetings();
                Check("Pending greeting", greetings.Count == 1 && greetings[0].Key == "halloween@20261025", string.Join(",", greetings.Select(g => g.Key)));
                OccasionSystem.MarkGreetingShown(greetings[0]);
                Check("Greeting shown once", OccasionSystem.GetPendingGreetings().Count == 0);
                Check("Greeting text in the context's language", OccasionSystem.GetGreetingText(greetings[0]) == ("Boo!", "") && OccasionSystem.GetGreetingText(greetings[0], "en").title == "Boo");

                OccasionClock.Simulate(new DateTime(2026, 11, 1, 9, 0, 0));
                OccasionSystem.Tick();
                Check("Boundary tick picks up the exclusive day", OccasionSystem.IsActive("mourning") && !OccasionSystem.IsActive("halloween") && OccasionSystem.TargetIcon == "default" && changed == 2,
                    $"{string.Join(",", OccasionSystem.Active.Select(a => a.Id))} {OccasionSystem.TargetIcon} {changed}");

                OccasionSystem.SeasonalIconsEnabled = false;
                OccasionClock.Simulate(new DateTime(2026, 10, 31, 12, 0, 0));
                OccasionSystem.Refresh();
                Check("Player opt-out keeps the default icon", OccasionSystem.TargetIcon == "default" && OccasionSystem.IsActive("halloween"));
            }
            finally
            {
                PlayerPrefs.DeleteKey("OccasionKit.IconsEnabled");
                OccasionSystem.ResetGreetings();
                OccasionClock.ClearSimulation();
                OccasionSystem.ResetStatics();
                Object.DestroyImmediate(catalog);
            }
        }

        private static void Images()
        {
            var solid = RgbaImage.Solid(432, new Color(1, 0, 0, 1)).Resize(108, 108);
            Check("Resize keeps color", Approximately(solid.Pixels[54 * 108 + 54], new Color(1, 0, 0, 1)));

            var masked = RgbaImage.Solid(100, Color.white);
            masked.Mask(MaskShape.Circle);
            Check("Circle mask", masked.Pixels[0].a < 0.01f && masked.Pixels[50 * 100 + 50].a > 0.99f && masked.Pixels[14 * 100 + 14].a is var edge && edge > 0 && edge < 1, $"edge alpha {masked.Pixels[14 * 100 + 14].a:0.00}");

            var halo = new RgbaImage(2, 1);
            halo.Pixels[0] = new Color(1, 1, 1, 1);
            halo.Pixels[1] = new Color(0, 0, 0, 0);
            var half = halo.Resize(1, 1).Pixels[0];
            Check("Premultiplied resize has no dark fringe", Mathf.Abs(half.r - 1) < 0.01f && Mathf.Abs(half.a - 0.5f) < 0.01f, half.ToString());

            var layers = new IconLayers(TestIcon(out var texture));

            try
            {
                var viewport = layers.Viewport(72);
                Check("Viewport is the center 72 dp", viewport.Width == 72 && viewport.Pixels[36 * 72 + 36].r > 0.9f && viewport.Pixels[0].b > 0.5f);
                Check("PNG encodes", layers.Masked(96, MaskShape.Squircle).EncodePng().Length > 100);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void BuildStep()
        {
            var root = Path.Combine(Path.GetTempPath(), "OccasionKitSelfTest", "unityLibrary");

            if (Directory.Exists(root))
                Directory.Delete(root, true);

            Directory.CreateDirectory(Path.Combine(root, "src", "main"));
            File.WriteAllText(Path.Combine(root, "proguard-unity.txt"), "-keep class com.unity3d.** { *; }\n");
            File.WriteAllText(Path.Combine(root, "src", "main", "AndroidManifest.xml"),
@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
  <application>
    <activity android:name=""com.unity3d.player.UnityPlayerActivity"" android:exported=""true"">
      <intent-filter>
        <category android:name=""android.intent.category.LAUNCHER"" />
        <action android:name=""android.intent.action.MAIN"" />
      </intent-filter>
      <intent-filter>
        <action android:name=""android.intent.action.VIEW"" />
        <data android:scheme=""myapp"" />
      </intent-filter>
    </activity>
  </application>
</manifest>");

            var icon = TestIcon(out var texture);

            try
            {
                var target = AndroidIconBuildStep.Process(root, new[] { icon }, new[] { "old_event" }, true);
                Check("Launcher activity found", target == "com.unity3d.player.UnityPlayerActivity", target);

                // Run twice: Gradle projects can be reused, so the step must not stack aliases.
                AndroidIconBuildStep.Process(root, new[] { icon }, new[] { "old_event" }, true);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            var ns = "http://schemas.android.com/apk/res/android";
            var document = new XmlDocument();
            document.Load(Path.Combine(root, "src", "main", "AndroidManifest.xml"));
            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("android", ns);

            var aliases = document.SelectNodes("/manifest/application/activity-alias", namespaces).OfType<XmlElement>().ToList();
            Check("Three aliases (default, icon, retired)", aliases.Count == 3, string.Join(", ", aliases.Select(a => a.GetAttribute("name", ns))));
            Check("Only default starts enabled", aliases.Count(a => a.GetAttribute("enabled", ns) == "true") == 1 && aliases.First(a => a.GetAttribute("enabled", ns) == "true").GetAttribute("name", ns) == "com.occasionkit.Icon_default");
            Check("Icon alias points at its resources", aliases.Any(a => a.GetAttribute("icon", ns) == "@mipmap/occasionkit_selftest"));
            Check("Retired alias keeps the default icon", aliases.Any(a => a.GetAttribute("name", ns) == "com.occasionkit.Icon_old_event" && !a.HasAttribute("icon", ns)));
            Check("Launcher filter moved off the activity", document.SelectNodes("/manifest/application/activity/intent-filter/category[@android:name='android.intent.category.LAUNCHER']", namespaces).Count == 0);
            Check("Deep link filter stays", document.SelectNodes("/manifest/application/activity/intent-filter/data[@android:scheme='myapp']", namespaces).Count == 1);
            Check("Receiver added once", document.SelectNodes("/manifest/application/receiver[@android:name='com.occasionkit.OccasionIconReceiver']", namespaces).Count == 1);

            var icons = document.SelectSingleNode("/manifest/application/meta-data[@android:name='com.occasionkit.icons']", namespaces) as XmlElement;
            var all = document.SelectSingleNode("/manifest/application/meta-data[@android:name='com.occasionkit.aliases']", namespaces) as XmlElement;
            Check("Showable icons", icons?.GetAttribute("value", ns) == "default,selftest", icons?.GetAttribute("value", ns));
            Check("All aliases", all?.GetAttribute("value", ns) == "default,selftest,old_event", all?.GetAttribute("value", ns));

            var res = Path.Combine(root, "src", "main", "res");
            Check("Adaptive icon xml", File.Exists(Path.Combine(res, "mipmap-anydpi-v26", "occasionkit_selftest.xml")));
            Check("Foreground layers per density", new[] { "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi" }.All(d => File.Exists(Path.Combine(res, "mipmap-" + d, "occasionkit_selftest_fg.png"))));
            Check("Legacy icons", File.Exists(Path.Combine(res, "mipmap-xxxhdpi", "occasionkit_selftest_round.png")));
            Check("Background color resource", File.ReadAllText(Path.Combine(res, "values", "occasionkit_colors.xml")).Contains("occasionkit_selftest_bg"));
            Check("Keep rule added once", File.ReadAllText(Path.Combine(root, "proguard-unity.txt")).Split('\n').Count(l => l.Contains("com.occasionkit")) == 1);
            Note("Output: " + root);
        }

        /// <summary>A foreground-switching backend (iOS): never on the way out, once after launch and after each return.</summary>
        private static void ForegroundSwitching()
        {
            if (Application.isPlaying)
            {
                Note("Skipped in Play Mode (it would reset the running OccasionSystem).");
                return;
            }

            var catalog = ScriptableObject.CreateInstance<OccasionCatalog>();
            catalog.occasions.Add(Yearly("halloween", CalendarType.Gregorian, 10, 25, 11, 5, 10, "halloween"));
            var backend = new FakeBackend { SwitchMoment = IconSwitchMoment.Foreground };
            OccasionSystem.ResetStatics();
            OccasionClock.Simulate(new DateTime(2026, 10, 31, 12, 0, 0));

            try
            {
                OccasionSystem.InitializeAsync(new OccasionConfig { Catalogs = new[] { catalog }, IconBackend = backend });
                Check("Nothing switched during initialization", backend.Applied.Count == 0, string.Join(",", backend.Applied));

                OccasionSystem.Tick();
                Check("Switches on the first tick after launch", backend.Applied.SequenceEqual(new[] { "halloween" }), string.Join(",", backend.Applied));

                OccasionSystem.Tick();
                Check("Only once", backend.Applied.Count == 1, string.Join(",", backend.Applied));

                OccasionSystem.OnBackground();
                Check("Nothing switched or scheduled on the way out", backend.Applied.Count == 1 && backend.LastSchedule.Count == 0, string.Join("|", backend.LastSchedule));

                OccasionClock.Simulate(new DateTime(2026, 11, 10, 9, 0, 0));
                OccasionSystem.OnForeground();
                Check("Waits for the app to tick after returning", backend.Applied.Count == 1, string.Join(",", backend.Applied));

                OccasionSystem.Tick();
                Check("Back to the default icon after the occasion", backend.Applied.SequenceEqual(new[] { "halloween", "default" }), string.Join(",", backend.Applied));
            }
            finally
            {
                OccasionClock.ClearSimulation();
                OccasionSystem.ResetStatics();
                Object.DestroyImmediate(catalog);
            }
        }

        private static void IOSBuildStep()
        {
            var root = Path.Combine(Path.GetTempPath(), "OccasionKitSelfTest", "xcode");

            if (Directory.Exists(root))
                Directory.Delete(root, true);

            var catalog = Path.Combine(root, "Unity-iPhone", "Images.xcassets");
            Directory.CreateDirectory(Path.Combine(catalog, "AppIcon.appiconset"));
            Directory.CreateDirectory(Path.Combine(catalog, "occasionkit_old_event.appiconset"));

            var icon = TestIcon(out var texture);
            List<string> names;

            try
            {
                // Run twice: Xcode projects can be built into again (Append).
                IOSIconBuildStep.Process(root, new[] { icon });
                names = IOSIconBuildStep.Process(root, new[] { icon });
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            var set = Path.Combine(catalog, "occasionkit_selftest.appiconset");
            var contents = File.Exists(Path.Combine(set, "Contents.json")) ? File.ReadAllText(Path.Combine(set, "Contents.json")) : "";
            Check("Alternate icon names", names.SequenceEqual(new[] { "occasionkit_selftest" }), string.Join(",", names));
            Check("Single-size icon set", contents.Contains("\"1024x1024\"") && contents.Contains("\"icon.png\"") && contents.Contains("\"universal\""));
            Check("Earlier OccasionKit icon sets removed", !Directory.Exists(Path.Combine(catalog, "occasionkit_old_event.appiconset")));
            Check("Primary icon set untouched", Directory.Exists(Path.Combine(catalog, "AppIcon.appiconset")));

            var png = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                // IHDR color type (byte 25): 2 is RGB, 6 is RGBA.
                var bytes = File.ReadAllBytes(Path.Combine(set, "icon.png"));
                Check("No alpha channel", bytes.Length > 25 && bytes[25] == 2, bytes.Length > 25 ? $"PNG color type {bytes[25]}" : "too short");

                png.LoadImage(bytes);
                Check("1024 px", png.width == 1024 && png.height == 1024, $"{png.width}×{png.height}");
                Check("Foreground in the middle", Approximately(png.GetPixel(512, 512), Color.white));
                Check("Background color in the corner", Approximately(png.GetPixel(4, 4), new Color(0.1f, 0.4f, 0.9f)), png.GetPixel(4, 4).ToString());
            }
            finally
            {
                Object.DestroyImmediate(png);
            }

            Note("Output: " + root);
        }

        private static AppIconVariant TestIcon(out Texture2D texture)
        {
            const int size = 432;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - size / 2 + 0.5f;
                    var dy = y - size / 2 + 0.5f;
                    pixels[y * size + x] = dx * dx + dy * dy < 100 * 100 ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return new AppIconVariant { id = "selftest", foreground = texture, backgroundColor = new Color(0.1f, 0.4f, 0.9f) };
        }

        private static Occasion Yearly(string id, CalendarType calendar, int startMonth, int startDay, int endMonth, int endDay, int priority, string icon) => new Occasion
        {
            id = id,
            displayName = id,
            priority = priority,
            iconId = icon,
            schedule = new OccasionSchedule { calendar = calendar, startMonth = startMonth, startDay = startDay, endMonth = endMonth, endDay = endDay },
        };

        private static void Icon(string name, List<Occasion> occasions, DateTime time, string expected)
        {
            var actual = OccasionEvaluator.Evaluate(occasions, time, OccasionContext.Any).IconId;
            Check(name, actual == expected, actual ?? "default");
        }

        private static void Date(string name, CalendarType calendar, int year, int month, int day, DateTime expected)
        {
            var ok = CalendarUtility.TryToDateTime(calendar, year, month, day, out var actual);
            Check(name, ok && actual == expected, actual.ToString("yyyy-MM-dd"));
        }

        private static void Near(string name, DateTime actual, DateTime expected, double minutes)
        {
            Check(name, Math.Abs((actual - expected).TotalMinutes) <= minutes, actual.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static bool Approximately(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok)
                _passed++;
            else
                _failed++;

            Report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail != null ? "  [" + detail + "]" : "")}");
        }

        private static void Note(string text) => Report.AppendLine("      " + text);

        private static void Safe(string group, Action test)
        {
            Report.AppendLine($"— {group}");

            try
            {
                test();
            }
            catch (Exception e)
            {
                _failed++;
                Report.AppendLine($"FAIL  {group} threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
