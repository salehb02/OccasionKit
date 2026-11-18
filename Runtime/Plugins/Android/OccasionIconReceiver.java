package com.occasionkit;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import java.util.Arrays;

/**
 * Applies scheduled icon changes while the app is closed. Each alarm applies the latest change that is due and
 * schedules the rest, so a chain of changes (window opens, closes, next one opens) runs without the app.
 * The app cancels the chain when it comes to the foreground and schedules a fresh one when it leaves.
 */
public final class OccasionIconReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        long[] times = intent.getLongArrayExtra(OccasionIcons.EXTRA_TIMES);
        String[] icons = intent.getStringArrayExtra(OccasionIcons.EXTRA_ICONS);

        if (times == null || icons == null || times.length == 0 || times.length != icons.length)
            return;

        long now = System.currentTimeMillis();
        int due = 0;

        while (due + 1 < times.length && times[due + 1] <= now)
            due++;

        OccasionIcons.setIcon(context, icons[due]);

        if (due + 1 < times.length) {
            OccasionIcons.schedule(context,
                    Arrays.copyOfRange(times, due + 1, times.length),
                    Arrays.copyOfRange(icons, due + 1, icons.length));
        }
    }
}
