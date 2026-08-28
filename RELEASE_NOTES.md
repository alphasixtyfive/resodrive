ResoDrive 0.3.1 fixes a crash that could occur when reopening a maximized window
after ResoDrive started in the background.

Notification-area actions are now isolated so an unexpected failure in one UI
callback cannot terminate the application. Fatal failures during very early
startup are also written to the diagnostic log and displayed with an error ID
instead of looking like a silent exit.

The normal download is `ResoDrive-Setup.exe`. Existing settings, credentials,
mounts, and sync jobs are preserved during the upgrade.
