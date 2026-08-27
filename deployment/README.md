# Profile deployment bundle

This template supports a small private deployment ZIP without creating a custom
ResoDrive build. Put these three files in one folder:

- `Install-ResoDrive.bat`
- the public release MSI, renamed to `ResoDrive-Setup.msi`
- a private deployment profile named `profiles.json`

Run `Install-ResoDrive.bat` as the signed-in user. Windows Installer requests
elevation for the per-machine application install; the unelevated script writes
the profile for that user to `%LOCALAPPDATA%\rdrive\profiles.json`.

The script never replaces an existing user profile. Normal MSI installs and
upgrades manage `profiles.sample.json` only and leave `profiles.json` untouched.
To deploy a changed profile later, use the application's profile editor or a
separately managed user-context update after backing up the existing file.

Do not put passwords, private keys, tokens, or other secrets in a profile file.
Keep deployment-specific profile files and completed bundles out of this public
repository.
