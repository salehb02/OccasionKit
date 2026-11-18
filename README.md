# OccasionKit

Seasonal occasions for Unity apps: Nowruz, Yalda, Ramadan, Halloween, Christmas, or any date you define. Each occasion can switch the app icon on Android and iOS, load a themed set of assets, show a greeting and send a local notification. Everything runs on the device clock, with no server.

- **Time windows** in the Gregorian, Persian (Jalali) or Hijri calendar. They can repeat every year or use explicit dates, with lead and tail days, times of day, and windows that cross the new year.
- **An app icon per occasion** on Android and iOS.
  - Android: the icon switches when the app goes to the background. Upcoming changes are scheduled with AlarmManager, so the icon also changes while the app is closed.
  - iOS: the icon switches when the player opens or returns to the app, which is the only time iOS allows it. iOS shows a short alert each time.
- **Priorities and exclusive occasions.** The highest priority wins the icon. An exclusive occasion, such as Tasua & Ashura, suppresses every occasion below it.
- **Targeting** by store and language.
- **Themes** (any Addressable asset), **greetings** (once per occurrence), optional **local notifications**, and **countdowns**, including the exact Nowruz moment.
- **Player opt-out** for seasonal icons.
- **An editor window** with a year timeline, an icon set editor with mask previews, a simulator for any date, store and language, and validation with one-click fixes.

## What ships where

| | Where it lives | Changing it needs |
|---|---|---|
| Occasions (dates, priorities, targeting, themes, greetings, which icon to use) | `OccasionCatalog` assets in Addressables | A content update, if the catalog is in a remote group |
| App icons | Compiled into the build: Android mipmap resources with one `<activity-alias>` each, iOS alternate app icons in the asset catalog | A new build |

So you can add an icon in one release and turn it on for an occasion later from a remote catalog. If a catalog points at an icon the installed build doesn't have, that occasion is skipped for the icon. The next active occasion with an icon wins, or the default icon if none has one.

## Requirements

- Unity 2022.3 or newer (developed on Unity 6.3).
- Addressables 1.21 or newer (installed as a dependency).
- Android app icons need compileSdk 33 or newer.
- iOS app icons need Xcode 14 or newer.
- Optional: Mobile Notifications (`com.unity.mobile.notifications`) 2.0 or newer for local notifications on Android and iOS.

## Install

In **Window > Package Manager**, choose **+ > Add package from git URL** and enter:

```
https://github.com/salehb02/OccasionKit.git
```

Append a tag such as `#v1.1.0` to pin a version. Or add it to `Packages/manifest.json`:

```json
"com.occasionkit.core": "https://github.com/salehb02/OccasionKit.git#v1.1.0"
```

To work on the package itself, clone the repository into your project's `Packages` folder instead.

## Set up

1. **Window > OccasionKit**, then **Create settings**. The settings hold the icon set and store names. They're editor-only and never ship.
2. **Occasions tab**: create a catalog and add occasions. The **+** menu has presets (Nowruz, Yalda, Ramadan, Halloween, …). The timeline at the top shows the year in the Gregorian, Persian or Hijri calendar.
3. **Validation tab** > **Make addressable**: put the catalog in a group, ideally a remote one. The runtime loads every catalog with the label `occasion-catalog`.
4. **Icons tab**: add icons. Each icon needs:
   - a foreground layer of at least 432 px, with the art inside the center 66 dp circle;
   - a background layer or color.

   Then pick the icon on an occasion.
5. **Simulator tab**: test any date, store and language. To pick a date, click or drag the timeline, or right-click an occasion > **Simulate**. It also shows the icon changes coming up and can drive Play Mode.

**Tools > OccasionKit > Run Self-Test** checks calendars, rules, icon timing, image processing and the Android and iOS build steps.

## Runtime

```csharp
using OccasionKit;

// Once at startup, after Addressables catalog updates.
await OccasionSystem.InitializeAsync(new OccasionConfig
{
    Store = "GooglePlay",       // matched against store targeting
    Language = "fa",            // optional; default is the device language
});

OccasionSystem.Changed += Refresh;                  // also raised once after init
bool yalda = OccasionSystem.IsActive("yalda");
OccasionInstance nowruz = OccasionSystem.GetActive("nowruz");
TimeSpan left = nowruz.GetCountdown(OccasionClock.Now);

var theme = OccasionSystem.GetActiveTheme();         // highest-priority active occasion with a theme
var handle = OccasionSystem.LoadThemeAsync<MyTheme>(theme.Occasion);   // Addressables.Release(handle) when done

foreach (var g in OccasionSystem.GetPendingGreetings()) { Show(g); OccasionSystem.MarkGreetingShown(g); }

OccasionSystem.SeasonalIconsEnabled = false;         // player setting

OccasionKit.Notifications.OccasionNotifications.Enable("Events"); // optional
```

- `OccasionSystem.GetNext(id)` returns an occasion's next occurrence, and `GetUpcoming(within)` lists everything starting soon.
- `OccasionSystem.ReloadCatalogsAsync()` reloads the catalogs after an Addressables catalog update in a long session.
- `OccasionConfig.Catalogs` passes catalogs directly, without Addressables (tests, or projects without remote content).
- `OccasionConfig.ManageAppIcon = false` keeps occasions, themes and greetings but never touches the icon.
- `OccasionConfig.IconBackend` plugs in your own icon switching.
- `OccasionToggle` (**Add Component > OccasionKit**) shows or hides objects while occasions are active.
- `[OccasionId]` turns a string field into a dropdown of occasion ids.
- `OccasionClock.Simulate(time)` moves the clock for debug menus.
- `OccasionSystem.ApplyIconNow()` switches the icon immediately, instead of at the next trip to the background (Android) or return to the app (iOS). On Android, use it only from a debug menu, since some launchers restart the app when its icon changes.

## Android notes

- The build step moves the `MAIN/LAUNCHER` intent filter from your launcher activity to one `<activity-alias>` per icon (`com.occasionkit.Icon_<id>`). Only `default` starts enabled, and it uses the Player Settings icon. Other intent filters, such as deep links, stay on the activity.
- **Never-delete rule:** Android remembers which alias was enabled across updates. If an update removes that alias, the app has no launcher icon at all. Every icon that ships is therefore recorded in `ProjectSettings/OccasionKitReleasedIcons.json` (commit it). Builds keep a removed icon's alias, pointed at the default icon.
- **Icon ids** start with a lowercase letter and use only `a-z`, `0-9` and `_`, because they become Android resource names and iOS icon names.
- **Home-screen shortcuts:**
  - Some launchers drop pinned shortcuts when the alias changes; the app drawer entry always follows.
  - The first update that adds OccasionKit changes the launcher component, which can do the same once.
- Scheduled changes use inexact, non-waking alarms, so no exact-alarm permission is needed. They don't survive a reboot; the app fixes the icon the next time it goes to the background.
- The Java side uses API 33's batch `setComponentEnabledSettings` when available, so it needs compileSdk 33 or newer.
- Other platforms keep their normal icon; `IAppIconBackend` is the extension point. Occasions, themes, greetings and countdowns work on every platform.

## iOS notes

- **Build step:** each icon becomes an alternate app icon named `occasionkit_<id>` in `Unity-iPhone/Images.xcassets`.
  - It's a single 1024 px image (Xcode makes the smaller sizes), cut from the layers' center 72 dp, the part an Android launcher shows. It sits on the background color, so it has no transparency, and iOS rounds the corners.
  - The step also sets the `ASSETCATALOG_COMPILER_ALTERNATE_APPICON_NAMES` build setting, which needs Xcode 14 or newer.
  - The primary icon stays the Player Settings icon.
- **When the icon changes:** iOS only lets an app change its icon while it's open, and shows an alert ("You have changed the icon for …") each time.
  - OccasionKit switches it about a second after launch and after each return to the app, and only when the icon actually changes.
  - An occasion that starts while the app is open waits for the next return, so the alert doesn't interrupt play.
  - The icon can't change while the app is closed, so an occasion that starts then shows its icon the next time the player opens the app.
- **Removing an icon** needs no retired copy, unlike Android: if the player's current icon is no longer in the build, OccasionKit switches back to the default on the next launch.
- **Themed icons:** iOS 18 dark and tinted variants aren't generated. The monochrome layer is only used on Android.
- **Notifications** need the player's permission. Ask for it with the Mobile Notifications setting **Request Authorization on App Launch**, or with an `AuthorizationRequest` when it suits your app.

## Calendars

- **Persian** uses .NET's `PersianCalendar`; the self-test checks it against known dates.
- **Hijri** is arithmetic and can differ from the official, sighting-based calendar by a day or two. Check each year and use the occasion's Hijri offset, or switch to explicit dates once they're announced.
- **March equinox** times come from Meeus's algorithm, accurate to about a minute.

## License

[MIT](LICENSE.md)
