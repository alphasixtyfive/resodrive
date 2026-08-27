# Releasing ResoDrive

## Repository visibility

The built-in update checker uses GitHub's unauthenticated latest-release API.
Therefore the production repository and its published releases must be public.
A private repository returns `404` to installed clients unless every client is
given a GitHub credential, which ResoDrive intentionally does not request or store.

Private development can still happen in a separate private repository, but the
release source/tag and assets consumed by users must be mirrored to the configured
public repository before publishing.

## Release procedure

1. Set `VersionPrefix` in `Directory.Build.props`.
2. Update release-facing documentation as needed.
3. Run `./build.ps1` on Windows and smoke-test both the portable package and MSI.
4. Commit the release and create an annotated tag matching the version exactly,
   for example `git tag -a v0.2.29 -m "ResoDrive 0.2.29"`.
5. Push the commit and tag. The Release workflow rebuilds and tests from the tag,
   uploads the ZIP, MSI, and SHA-256 files, and creates the GitHub release.
6. Confirm that Settings > Components > ResoDrive discovers the published version.

GitHub Actions are pinned to immutable commit hashes. Dependabot proposes action
and NuGet updates for review.

## Signing

Current local packages include SHA-256 sidecars but are not Authenticode signed.
Before broad distribution, configure a protected CI signing identity and sign the
executable and MSI before release publication. Never place a signing certificate
or password in the repository; use GitHub environment secrets or an external
key-signing service.
