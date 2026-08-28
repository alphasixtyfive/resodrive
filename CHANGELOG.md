# Changelog

## [0.3.3] - 2026-08-28

- Do not abort an upgrade when optional graceful-shutdown preparation fails.
- Wait for path-matched installed processes to exit before replacing files.
- Treat an already-stopped application as a successful installer no-op.
- Include the MSI log path in failed application-update diagnostics.
- Keep fixed header and footer actions aligned with scrollable content whenever
  a vertical scrollbar appears across the main pages and editor windows.

## [0.3.2] - 2026-08-28

- Keep the per-user startup task registered when Windows normalizes its saved
  user identity and default privilege fields.
- Avoid visible PowerShell windows while an installer upgrade stops ResoDrive.

## [0.3.1] - 2026-08-28

- Prevent a crash when restoring a maximized window after background startup.
- Contain notification-area callback failures so a single UI action cannot
  terminate the application.
- Log fatal early-startup failures with an error ID and show a useful error
  message instead of exiting silently.

## [0.3.0] - 2026-08-28

First public preview of the cleaned ResoDrive codebase.

- Mount Nextcloud, WebDAV, and SFTP storage as Windows drives.
- Run copy and mirror jobs independently of mounted drive availability.
- Keep active work in a per-user background host when the window is closed.
- Start promptly at sign-in through Windows Task Scheduler when enabled.
- Retry interrupted mounts with bounded backoff for unreliable links.
- Resume application and rclone downloads after transient connection failures.
- Encrypt managed rclone credentials with Windows CurrentUser DPAPI.
- Install through a small setup bundle that downloads the .NET Desktop Runtime
  only when it is missing.

[0.3.3]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.3
[0.3.2]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.2
[0.3.1]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.1
[0.3.0]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0
