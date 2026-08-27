# ResoDrive

[![CI](https://github.com/alphasixtyfive/resodrive/actions/workflows/ci.yml/badge.svg)](https://github.com/alphasixtyfive/resodrive/actions/workflows/ci.yml)
[![License: 0BSD](https://img.shields.io/badge/license-0BSD-blue.svg)](LICENSE)

ResoDrive is a compact Windows manager for rclone mounts and per-mount transfer
jobs. It provides a modern WPF interface, a notification-area controller, and a
per-user background host mode that keeps mounts running when the window is closed.

## Highlights

- Manual WebDAV, Nextcloud, and SFTP setup with optional deployment profiles
- WebDAV/Nextcloud and SFTP password or private-key authentication
- Multiple independently managed mounts and drive letters
- Per-drive copy and mirror jobs with explicit confirmation for destructive mirror runs
- Live sync counters from rclone's structured stats and persistent last-run outcomes
- Auto-mount, retry, scheduling, and start-with-Windows options
- Encrypted managed rclone configuration with a DPAPI-protected password
- Explicit stable rclone update checks and verified updates
- Explicit ResoDrive release checks with trusted GitHub release links
- Self-contained Windows x64 packages; no .NET runtime required
- Windows 10 version 1809 or newer, and Windows 11

## First run

1. Install the MSI, or extract the portable release and run `resodrive.exe`.
2. Download the ResoDrive-managed rclone component when prompted.
3. Enter the connection details, account username and password, and select a free
   drive letter. Deployment profiles appear here only when you provide a valid
   `profiles.json`.
4. Install a current WinFsp release if ResoDrive reports that it is missing.

ResoDrive does not bundle or adopt a system rclone. It downloads and manages one
verified private copy under `%LOCALAPPDATA%\rdrive\components\rclone`. Mounts need
WinFsp because it is a Windows filesystem driver. Sync jobs do not need WinFsp.

The portable package does not install or copy itself. Its `resodrive.exe` and
`profiles.sample.json` stay in the extracted folder; the MSI installs the same files
under Program Files. Application settings, the private rclone runtime, encrypted configuration,
structured sync logs, last-run state, cache, and ownership state live under
`%LOCALAPPDATA%\rdrive` by default. Start with Windows
points to the current `resodrive.exe` with a background-start option, so it opens the
tray without showing the management window. Move the application folder before
enabling that option. Moving it later only requires toggling Start with Windows
off and on again.

Credentials are protected with Windows DPAPI for the current user. This keeps
the application package self-contained, but intentionally prevents copying the
saved credentials to a different Windows account or computer.

SFTP private-key authentication stores the selected `key_file` path in the
encrypted rclone configuration. The key itself remains in its original location
and must remain available there; an optional key passphrase is obscured by rclone.

## Profiles

Connection profiles are optional. Without a valid `profiles.json`, Add Drive opens
directly in manual mode and does not show a profile selector. Copy
[`profiles.sample.json`](profiles.sample.json) to `profiles.json` beside the portable
executable, or use **Settings > Edit profiles** to create a per-user copy under the
application data directory. Replace all example values before use. Service URLs,
WebDAV path templates, suggested drive letters, and allow-listed mount arguments
can then be managed without rebuilding the application.

Profile files are strictly validated. Invalid files are ignored with a visible
diagnostic and manual setup remains available. Service URLs must use HTTPS and cannot
contain embedded credentials, queries, or fragments. The setup window displays
the exact service URL and the loaded profile source before connecting. A missing
file quietly uses manual setup; an invalid file reports the reason. No organization
name or service endpoint is compiled into ResoDrive. Executable update origins are
intentionally not configurable.

## Updating rclone

Settings offers **Download** when rclone is not installed and shows its version
afterwards. **Check** queries the official
stable channel; **Update** is always explicit and is disabled while mounts or
sync jobs are active. rclone downloads and verifies the new binary, ResoDrive
validates the staged version, and replacement uses a rollback copy.

## Updating ResoDrive

Settings has a separate ResoDrive update row. **Check** queries the latest published
stable release from the project's GitHub repository without delaying application
startup. When a newer semantic version is available, **Get update** opens that exact
GitHub release so the MSI or portable ZIP can be downloaded. MSI upgrades ask the
running host to drain and stop mounts before Windows Installer replaces application
files; settings and the managed rclone runtime remain under the per-user data folder.

The update check becomes available once the repository has a public published release
tagged with a version such as `v0.2.29`. Drafts and prereleases are not returned by the
latest-stable endpoint.

## Building

Install the .NET 10 SDK, then run:

```powershell
dotnet test resodrive.slnx -c Release
.\build.ps1
```

The unpacked package, versioned portable ZIP, and per-machine MSI are written
under `artifacts\win-x64`. The MSI installs to `%ProgramFiles%\rdrive`, creates
a Start menu shortcut, supports silent deployment with `msiexec /i <file>.msi
/qn`, and upgrades older releases in place. Interactive installation ends with
an optional **Launch ResoDrive** checkbox; unattended installation never launches
the app. Pass `-BuildMsi $false` when only a portable development build is
required.

The build runs all tests and writes checksums for the release files. The first
rclone download uses a release and SHA-256 value pinned in the application;
later updates use rclone's signed official self-update mechanism.

## Project structure

- `ResoDrive.App`: WPF desktop, setup, settings, and tray UI
- `ResoDrive.Host`: per-user mount and transfer supervisor
- `ResoDrive.Core`: models, policies, validation, and contracts
- `ResoDrive.Windows`: rclone, Windows, encrypted storage, update, and IPC adapters

## Project links

- [Releases](https://github.com/alphasixtyfive/resodrive/releases)
- [Changelog](CHANGELOG.md)
- [Issue tracker](https://github.com/alphasixtyfive/resodrive/issues)
- [Contributing](CONTRIBUTING.md)
- [Security policy](docs/SECURITY.md)
- [Release process](docs/RELEASING.md)

The application-facing repository, release, issue, and update API links are derived
from the GitHub properties in `Directory.Build.props`. Change those properties when
publishing a fork; no C# source changes are required.

Developed by [Alexey Ivanov](https://github.com/alphasixtyfive).

## License

0BSD. Use, copy, modify, and redistribute for any purpose.
