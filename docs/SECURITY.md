# Security design

Report suspected vulnerabilities privately through the repository's GitHub
Security Advisory form. Do not open a public issue containing exploit details,
credentials, private service addresses, configuration files, or unredacted logs.

- The manager runs as the interactive user. WinFsp is a separate machine-level
  prerequisite; ResoDrive links only to its official release page.
- Managed processes are identified by mount ID, PID, process creation time,
  canonical executable path, source, and target before any stop operation.
- External rclone processes are visible but never terminated by default.
- Manager-owned rclone arguments are constructed internally. User tuning options
  pass through a strict token policy at import, save, load, and launch time.
- Provider configuration is encrypted by rclone with a generated password stored
  using Windows CurrentUser DPAPI. Moving the application for the same user keeps
  access; copying the data to another Windows user or machine requires reconnection.
- Adjacent profiles are editable but strictly validated as HTTPS destinations.
  Setup displays the exact endpoint before credentials are sent. Executable update
  origins are not profile-configurable.
- Mirror destinations reject roots, protected locations, mount targets, overlapping
  jobs, and reparse-point changes. Mirror runs require explicit confirmation.
- The initial rclone download is pinned and checksum-verified. Runtime rclone
  updates use rclone's official signed self-update flow, are explicit, and are
  staged and version-checked before replacement.
- ResoDrive application update checks use the build-configured GitHub API endpoint,
  accept only stable semantic versions, and expose only HTTPS release links under
  the configured repository path. They never download or install code silently.
- Logs and notifications redact secrets and credential-bearing URLs.
