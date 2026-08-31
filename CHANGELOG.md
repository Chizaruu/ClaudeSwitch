# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Open-source project scaffolding: MIT `LICENSE`, `CONTRIBUTING.md`,
  `SECURITY.md`, `CODE_OF_CONDUCT.md`, this changelog, issue/PR templates, and a
  GitHub Actions build check.
- Reorganized repository layout (`src/`, `assets/`) with an OSS-focused `README`.

### Changed
- Renamed the project to **ClaudeSwitch** (repo slug `claude-switch`). The
  login-routing engine keeps its name — `ClaudeRouter.exe` / `claude-router.sh`.
- `build.bat` now references source in `src/` and icons in `assets/`.

### Removed
- Prebuilt binaries (`ClaudeRouter.exe`, `ClaudeRouterSetup.exe`) are no longer
  committed; build from source instead.

## [0.1.0]

Initial release.

### Added
- Windows `claude://` login router with a Personal/Work chooser
  (`src/ClaudeRouter.cs`).
- Per-account colored taskbar buttons via `AppUserModel.ID`, and a self-healing
  background watcher.
- Automatic portable-copy creation, Claude download/install fallback, and
  removal of the conflicting standalone Claude app.
- Experimental Linux/macOS Bash port (`src/claude-router.sh`).
- `build.bat` / `uninstall.bat` one-click setup and removal.

[Unreleased]: https://github.com/Chizaruu/claude-switch/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Chizaruu/claude-switch/releases/tag/v0.1.0
