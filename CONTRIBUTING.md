# Contributing

ResoDrive welcomes focused bug fixes and improvements. Open an issue before a
large behavioral or architectural change so the design can be discussed first.

## Development

Requirements:

- Windows 10 version 1809 or newer
- The .NET SDK version pinned in `global.json`
- WinFsp for manual mount testing

Run the complete test suite before opening a pull request:

```powershell
dotnet test resodrive.slnx --configuration Release
```

Use `./build.ps1 -BuildMsi $false` to verify the self-contained portable package,
or `./build.ps1` for the full MSI release pipeline. Never commit generated files
from `artifacts`, `bin`, `obj`, or local application data.

Do not commit a deployment-specific `profiles.json`. Update the generic
`profiles.sample.json` only with reserved example domains and non-sensitive values.

## Security and privacy

Do not attach credentials, private keys, unredacted rclone configuration, or logs
containing private service URLs. Follow [docs/SECURITY.md](docs/SECURITY.md) for
security reports.
