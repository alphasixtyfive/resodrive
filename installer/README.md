# ResoDrive setup

The installer projects consume the framework-dependent release staged by
`build.ps1`; they do not publish the application independently. WiX Toolset
5.0.2 is pinned as an MSBuild SDK, so no machine-wide WiX installation is
required.

`ResoDrive-Setup.exe` is the normal user download. It embeds the MSI and downloads
the pinned .NET 10 Desktop Runtime only when a compatible runtime is absent. The
MSI remains available for managed deployment and in-app updates, where the runtime
prerequisite is already satisfied.

The MSI is a 64-bit, per-machine package that installs to
`%ProgramFiles%\rdrive`, creates a common Start menu shortcut, registers with
Windows Installed apps, and preserves all per-user data in
`%LOCALAPPDATA%\rdrive` during upgrades and uninstall.

Interactive installation uses the standard Windows Installer wizard and ends
with a checked **Launch ResoDrive** option after installation or upgrade. Silent
and passive deployments do not launch the application because launching is
wired only to the Finish button.

The MSI and bundle `UpgradeCode` values are permanent product-family identities.
Never change them after publication. Every release must increase
`VersionPrefix` in `Directory.Build.props`; Windows Installer versions use the
`major.minor.build` format.

During an upgrade or maintenance reinstall, ResoDrive first drains the background
host and stops its managed work cleanly. The installer then closes only the
`resodrive.exe` process running directly from this product's install directory.
Portable copies and unrelated processes elsewhere are left running. These actions
are skipped for uninstall. Launching ResoDrive afterward restores drives configured
to mount when the application starts; interrupted sync jobs are not resumed.
