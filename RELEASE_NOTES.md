ResoDrive 0.3.3 fixes application updates that could stop with Windows Installer
code 1603 when the optional graceful-shutdown preparation returned an error.
Preparation is now best-effort; the installer always continues to a hidden,
path-scoped fallback that stops only the installed ResoDrive processes and waits
for them to exit before replacing files. An already-stopped application is now
handled as a successful no-op instead of another installer error.

Failed updates now include the exact installer-log path in their diagnostic
message.

Fixed header and footer buttons now stay aligned with their scrollable content
when a vertical scrollbar appears on Drives, Sync, Settings, setup, or an editor.

The normal download is `ResoDrive-Setup.exe`. Existing settings, credentials,
mounts, and sync jobs are preserved during the upgrade.
