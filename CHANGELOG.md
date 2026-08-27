# Changelog

All notable user-facing changes are documented here. ResoDrive follows semantic
versioning while the `0.x` series remains under active development.

## [0.2.29] - 2026-08-27

### Added

- ResoDrive stable-release checks with trusted GitHub release links.
- GitHub Actions CI, verified release packaging, Dependabot, and issue templates.
- Repository and issue-tracker actions in the About window.
- Manual-only setup when no deployment profile catalog is present, plus a generic
  `profiles.sample.json` template.

### Changed

- Automatic mounts begin in parallel with UI startup for faster Windows sign-in.
- Required drive names and drive-letter choices are no longer prefilled.
- Repository URLs are centralized as build metadata.
- Release builds tolerate an open artifact directory in File Explorer.

### Fixed

- Renamed fixed and network drives now pass the updated Windows volume name when
  they are mounted again.

[0.2.29]: https://github.com/alphasixtyfive/resodrive/releases/tag/v0.2.29
