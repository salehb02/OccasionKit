package com.occasionkit;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.util.Log;

import java.util.ArrayList;
import java.util.List;

/**
 * Switches the launcher icon between the activity-alias entries that the OccasionKit build step adds:
 * one alias per icon, named com.occasionkit.Icon_[id], exactly one enabled at a time.
 * Called from C# (AndroidAppIconBackend) and from OccasionIconReceiver for scheduled changes.
 */
public final class OccasionIcons {
    public static final String DEFAULT_ICON = "default";

    static final String ALIAS_PREFIX = "com.occasionkit.Icon_";
    static final String META_ICONS = "com.occasionkit.icons";
    static final String META_ALIASES = "com.occasionkit.aliases";
    static final String EXTRA_TIMES = "com.occasionkit.extra.TIMES";
    static final String EXTRA_ICONS = "com.occasionkit.extra.ICONS";
    static final String TAG = "OccasionKit";
    private static final int REQUEST_CODE = 0x0CCA;

    private OccasionIcons() { }

    /** Icons that can be shown, including "default". Empty when the build step didn't run. */
    public static String[] getIcons(Context context) {
        return readList(context, META_ICONS);
    }

    /** Every alias in the manifest, including retired ones that only point at the default icon. */
    static String[] getAliases(Context context) {
        return readList(context, META_ALIASES);
    }

    /** The icon the launcher shows now, or null when no alias is set up. */
    public static String getCurrentIcon(Context context) {
        PackageManager pm = context.getPackageManager();

        for (String alias : getAliases(context)) {
            if (isEnabled(pm, component(context, alias), alias))
                return alias;
        }

        return null;
    }

    /** Enables the alias of iconId and disables every other one. Unknown ids fall back to the default icon. */
    public static boolean setIcon(Context context, String iconId) {
        String[] aliases = getAliases(context);

        if (aliases.length == 0)
            return false;

        if (iconId == null || !contains(getIcons(context), iconId))
            iconId = DEFAULT_ICON;

        PackageManager pm = context.getPackageManager();
        List<ComponentName> enable = new ArrayList<>();
        List<ComponentName> disable = new ArrayList<>();

        for (String alias : aliases) {
            ComponentName name = component(context, alias);
            boolean enabled = isEnabled(pm, name, alias);
            boolean wanted = alias.equals(iconId);

            if (wanted && !enabled)
                enable.add(name);
            else if (!wanted && enabled)
                disable.add(name);
        }

        if (enable.isEmpty() && disable.isEmpty())
            return true;

        try {
            if (Build.VERSION.SDK_INT >= 33) {
                // One atomic change, one launcher refresh.
                List<PackageManager.ComponentEnabledSetting> settings = new ArrayList<>();

                for (ComponentName name : enable)
                    settings.add(new PackageManager.ComponentEnabledSetting(name, PackageManager.COMPONENT_ENABLED_STATE_ENABLED, PackageManager.DONT_KILL_APP));

                for (ComponentName name : disable)
                    settings.add(new PackageManager.ComponentEnabledSetting(name, PackageManager.COMPONENT_ENABLED_STATE_DISABLED, PackageManager.DONT_KILL_APP));

                pm.setComponentEnabledSettings(settings);
            } else {
                // Enable first so there is never a moment without a launcher entry.
                for (ComponentName name : enable)
                    pm.setComponentEnabledSetting(name, PackageManager.COMPONENT_ENABLED_STATE_ENABLED, PackageManager.DONT_KILL_APP);

                for (ComponentName name : disable)
                    pm.setComponentEnabledSetting(name, PackageManager.COMPONENT_ENABLED_STATE_DISABLED, PackageManager.DONT_KILL_APP);
            }

            Log.i(TAG, "App icon -> " + iconId);
            return true;
        } catch (Exception e) {
            Log.w(TAG, "Switching the app icon failed", e);
            return false;
        }
    }

    /** Applies icons[i] at times[i] (epoch ms) while the app is closed. Replaces any earlier schedule. */
    public static void schedule(Context context, long[] times, String[] icons) {
        cancel(context);

        if (times == null || icons == null || times.length == 0 || times.length != icons.length)
            return;

        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);

        if (alarms == null)
            return;

        Intent intent = new Intent(context, OccasionIconReceiver.class);
        intent.putExtra(EXTRA_TIMES, times);
        intent.putExtra(EXTRA_ICONS, icons);

        PendingIntent pending = PendingIntent.getBroadcast(context, REQUEST_CODE, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        // Inexact and non-waking: no exact-alarm permission needed, and the icon only matters once the device is in use.
        if (Build.VERSION.SDK_INT >= 23)
            alarms.setAndAllowWhileIdle(AlarmManager.RTC, times[0], pending);
        else
            alarms.set(AlarmManager.RTC, times[0], pending);
    }

    public static void cancel(Context context) {
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        Intent intent = new Intent(context, OccasionIconReceiver.class);
        PendingIntent pending = PendingIntent.getBroadcast(context, REQUEST_CODE, intent, PendingIntent.FLAG_NO_CREATE | PendingIntent.FLAG_IMMUTABLE);

        if (pending != null) {
            if (alarms != null)
                alarms.cancel(pending);

            pending.cancel();
        }
    }

    static ComponentName component(Context context, String iconId) {
        return new ComponentName(context.getPackageName(), ALIAS_PREFIX + iconId);
    }

    /** Only the default alias is enabled in the manifest; the rest start disabled. */
    private static boolean isEnabled(PackageManager pm, ComponentName name, String iconId) {
        int state = pm.getComponentEnabledSetting(name);

        if (state == PackageManager.COMPONENT_ENABLED_STATE_DEFAULT)
            return DEFAULT_ICON.equals(iconId);

        return state == PackageManager.COMPONENT_ENABLED_STATE_ENABLED;
    }

    private static String[] readList(Context context, String key) {
        try {
            ApplicationInfo info = context.getPackageManager().getApplicationInfo(context.getPackageName(), PackageManager.GET_META_DATA);
            Bundle meta = info.metaData;
            String value = meta == null ? null : meta.getString(key);
            return value == null || value.isEmpty() ? new String[0] : value.split(",");
        } catch (Exception e) {
            Log.w(TAG, "Reading " + key + " failed", e);
            return new String[0];
        }
    }

    private static boolean contains(String[] values, String value) {
        for (String v : values) {
            if (v.equals(value))
                return true;
        }

        return false;
    }
}
