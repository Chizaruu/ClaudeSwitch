# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Full Linux and macOS support.** `src/claude-router.sh` is built out from an
  experimental scaffold to a complete engine with parity to the Windows version:
  per-account launchers that carry their own icon (a `.desktop` with a distinct
  `StartupWMClass` on Linux; a wrapper `.app` bundle on macOS), a self-heal watcher
  (an autostart entry on Linux, a `launchd` LaunchAgent on macOS), `register` /
  `unregister` / `status` / `tag` / `uninstall` subcommands, and — on macOS —
  automatic download and install of Claude Desktop when it is absent.
- `install.sh` / `uninstall.sh` — one-command setup and removal for Linux and
  macOS (the Unix counterparts to `build.bat` / `uninstall.bat`).
- Per-platform icon assets: `assets/*.png` (Linux) and `assets/*.icns` (macOS),
  generated from the existing `.ico` sources.
- Open-source project scaffolding: MIT `LICENSE`, `CONTRIBUTING.md`,
  `SECURITY.md`, `CODE_OF_CONDUCT.md`, this changelog, issue/PR templates, and a
  GitHub Actions build check.
- Reorganized repository layout (`src/`, `assets/`) with an OSS-focused `README`.

### Changed
- Renamed the project to **ClaudeSwitch** (repo slug `claude-switch`). The
  login-routing engine keeps its name — `ClaudeRouter.exe` / `claude-router.sh`.
- `build.bat` now references source in `src/` and icons in `assets/`.
- The Linux/macOS engine is written to stay Bash 3.2-compatible (stock macOS) and
  is `shellcheck`-clean; CI now checks it on both Ubuntu and macOS runners.
- README, CONTRIBUTING, and the platform-support table rewritten to document all
  three platforms as supported, with the two inherently best-effort UI details
  (Linux window grouping and macOS Dock icons) called out honestly.

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
