# Releasing ResoDrive

Starting with the first public release, GitHub keeps each published release and
tag so users can review the history and download an earlier version if needed.
On the local development drive, keep only the current public build artifacts
rather than archiving local copies.

## Repository visibility

The built-in update checker follows GitHub's public `releases/latest` redirect;
it does not consume the anonymous REST API quota. Therefore the production
repository and its published releases must be public. A private repository returns
`404` to installed clients unless every client is given a GitHub credential, which
ResoDrive intentionally does not request or store.

Private development can still happen in a separate private repository, but the
release source/tag and assets consumed by users must be mirrored to the configured
public repository before publishing.

## Release procedure

1. Set `VersionPrefix` in `Directory.Build.props`.
2. Rewrite `RELEASE_NOTES.md` in short, plain language for the current release and
   update other release-facing documentation as needed.
3. Run `./build.ps1` on Windows and smoke-test the setup executable, portable
   package, and MSI.
4. Commit the release and create an annotated tag matching the version exactly,
   for example `git tag -a v0.3.0 -m "ResoDrive 0.3.0"`.
5. Push the commit and tag. The Release workflow rebuilds and tests from the tag,
   uploads the versioned ZIP, MSI, setup executable, SHA-256 files, and stable
   `ResoDrive-Setup.exe` download, then creates the GitHub release.
6. Confirm that Settings > Components > ResoDrive discovers the published version.

GitHub Actions are pinned to immutable commit hashes. Dependabot proposes action
and NuGet updates for review.

## Signing

Current local packages include SHA-256 sidecars but are not Authenticode signed.
Before broad distribution, configure a protected CI signing identity and sign the
executables, MSI, and setup bundle before release publication. Never place a signing certificate
or password in the repository; use GitHub environment secrets or an external
key-signing service.
