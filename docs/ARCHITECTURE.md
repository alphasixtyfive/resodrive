# Architecture

## Process model

`resodrive.exe` runs either as the WPF management/tray process or in the internal
`--host` mode that owns mount and transfer lifecycles. The two per-user processes
use a local, user-scoped named-pipe protocol. Closing the management window does
not interrupt active work; there is no separate host executable to deploy.

The host is the only component allowed to start or stop rclone. It serializes
operations per mount, arbitrates drive targets globally, and publishes immutable
status snapshots. The WPF process never infers ownership from a visible drive.

## Configuration model

- A remote is an entry in the managed, per-user rclone configuration.
- An optional `profiles.json` contains editable deployment connection metadata only.
  Without one, setup is manual; `profiles.sample.json` is documentation, not an
  active catalog. ResoDrive uses only
  its private per-user rclone runtime; it never discovers or updates system copies.
- A mount definition selects a remote, optional subpath, target, cache profile,
  restart policy, and startup policy.
- A sync job belongs to a mount definition for organization but addresses the
  remote endpoint directly; it does not depend on the drive being mounted.
- Copy is the default transfer behavior. Mirror is destructive and requires a
  explicit confirmation before a mirror run.

Stable GUIDs identify mounts and jobs. Names, paths, and drive letters are never
used as ownership identifiers.

## Persistence

User settings, ownership state, and terminal sync outcomes are separate documents.
Writes are atomic and serialized; settings also retain a known-good backup. Live
transfer state remains transient, while the most recent success, failure, or
cancellation for each sync job survives a host restart. rclone diagnostic output
is normalized as bounded newline-delimited JSON in the logs directory. Transient
process state never contaminates user configuration.

Sync progress comes directly from rclone's structured stats events. Parsing,
diagnostic storage, lifecycle coordination, and UI presentation are separate so
malformed log output can neither fail a transfer nor leak presentation concerns
into the host.

The application directory is read for binaries, assets, an optional `profiles.json`,
and the inert `profiles.sample.json` template;
ResoDrive never relocates itself. Mutable per-user state and the managed rclone
runtime are kept in `%LOCALAPPDATA%\rdrive`. The optional current-user startup entry points to
`resodrive.exe` in the application directory with `--background`, which initializes
the tray and host without opening the management window. A normal second launch
restores the existing window; a background second launch exits silently.

## Windows compatibility

The application targets Windows 10 version 1809 and newer. Windows 11 visual
features are enabled only after runtime capability detection; Windows 10 uses a
solid WPF surface with the same layout and controls. The release is self-contained.

## Compatibility identities

The user-data directory, startup registry value, host pipe/mutex names, and MSI
installation directory retain the historical `rdrive` identity. These are stable
machine-facing identifiers rather than product copy. Renaming them would orphan
encrypted configuration, allow competing host instances, or break in-place MSI
upgrades. New assemblies, executables, UI, and package names use `ResoDrive`.
