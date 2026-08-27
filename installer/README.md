# ResoDrive MSI

The installer project consumes the self-contained release staged by
`build.ps1`; it does not publish the application independently. WiX Toolset
5.0.2 is pinned as an MSBuild SDK, so no machine-wide WiX installation is
required.

The MSI is a 64-bit, per-machine package that installs to
`%ProgramFiles%\rdrive`, creates a common Start menu shortcut, registers with
Windows Installed apps, and preserves all per-user data in
`%LOCALAPPDATA%\rdrive` during upgrades and uninstall.

Interactive installation uses the standard Windows Installer wizard and ends
with a checked **Launch ResoDrive** option after installation or upgrade. Silent
and passive deployments do not launch the application because launching is
wired only to the Finish button.

`UpgradeCode` is a permanent product-family identity. Never change the x64
value in `build.ps1` after publishing the first MSI. Every release must increase
`VersionPrefix` in `Directory.Build.props`; Windows Installer versions use the
`major.minor.build` format.

During an upgrade, current releases first drain the background host and stop its
managed work cleanly. A bounded compatibility step then stops only `rdrive.exe`,
`resodrive.exe`, and legacy `rclone.exe` processes running directly from this
product's install directory. Portable copies and unrelated processes elsewhere
are left running. Launching ResoDrive afterward restores drives configured to
mount when the application starts; interrupted sync jobs are not resumed.
