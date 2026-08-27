# Changelog

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

[0.3.0]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0
