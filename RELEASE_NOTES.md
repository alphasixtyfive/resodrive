ResoDrive 0.3.2 fixes Start with Windows registration on systems where Windows
Task Scheduler normalizes the saved user identity and default privilege fields.
ResoDrive now recognizes the normalized task as its own instead of removing it
during post-registration verification.

Installer upgrades no longer open PowerShell windows while stopping the running
application.

The normal download is `ResoDrive-Setup.exe`. Existing settings, credentials,
mounts, and sync jobs are preserved during the upgrade.
