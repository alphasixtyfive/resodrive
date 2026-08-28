ResoDrive 0.3.4 fixes settings changes being rejected just because a different
drive was mounted. Each change is now checked against the drive it actually
affects, so unrelated mounts stay connected.

When a mounted drive's connection settings need to change, ResoDrive explains
that the drive will briefly disconnect and asks before continuing. If approved,
the affected drive reconnects automatically. Running or queued sync work is
still protected and will never be interrupted to apply settings.

This release also keeps the real mount states visible when a settings change is
rejected, instead of briefly showing every drive as unmounted, and fixes a race
that could prevent an active drive from being deleted cleanly.

The normal download is `ResoDrive-Setup.exe`. Existing settings, credentials,
mounts, and sync jobs are preserved during the upgrade.
