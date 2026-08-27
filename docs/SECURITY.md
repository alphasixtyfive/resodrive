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
  staged and version-checked before replacement. Interrupted downloads resume from
  a partial file, but the complete archive must still match the pinned checksum.
- ResoDrive application update checks follow the build-configured GitHub latest
  release link without using the REST API, accept only stable semantic versions,
  and download only HTTPS assets under the configured repository path. Installation
  requires confirmation, SHA-256 verification, strict helper/path validation, and
  Windows elevation. A durable helper records the MSI result and reopens the app.
  Partial application downloads are reusable only if the completed MSI passes the
  published SHA-256 check.
- The public setup bundle contains the MSI and downloads a pinned .NET Desktop
  Runtime package only when the required runtime is missing. Direct MSI deployment
  is intended for managed machines where that prerequisite is already present.
- UI logs automatically redact common secrets, credential-bearing URLs, host
  names, and absolute paths. Free-form error text cannot be classified perfectly,
  so review log contents before sharing them.
