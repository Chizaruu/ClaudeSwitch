# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Unified cross-platform engine (`src/`, C# + NativeAOT).** One codebase for
  Windows, macOS and Linux, published per-RID into a small, dependency-free native
  binary — no .NET runtime for end users. An OS-agnostic `Router` sits over an
  `IPlatform` abstraction with `Windows` / `Mac` / `Linux` implementations.
- **Full feature set on every OS:** `claude://` handler (registry / `xdg-mime` /
  `duti`), account chooser (Win32 dialog / `zenity`·`kdialog` / `osascript`),
  per-account launchers with their own icon (AUMID-tagged `.lnk` / `.desktop` with
  `StartupWMClass` / wrapper `.app`), a self-heal watcher (HKCU `Run` /
  `~/.config/autostart` / `launchd`), and auto-install of Claude when absent
  (Windows & macOS).
- **Windows taskbar coloring under NativeAOT:** AppUserModel.ID window tagging,
  per-window icon and regroup, and AUMID-tagged shortcuts, implemented with
  source-generated COM (`[GeneratedComInterface]`) and a PEB-based cross-process
  command-line read (`WindowsInterop.cs`) — no WMI, no runtime COM marshalling.
- **Version-driven auto-tagging** (`.github/workflows/tag.yml`): the `<Version>` in
  `src/ClaudeRouter.csproj` is the single source of truth — bumping it and merging to
  `main` creates the `v<version>` tag and publishes the release automatically.
- **Source-only release automation** (`.github/workflows/release.yml`): a `vX.Y.Z`
  tag (auto or manual) publishes a GitHub Release with source bundles
  (`.zip` + `.tar.gz` + `SHA256SUMS.txt`) — no unsigned prebuilt binaries.
- **NuGet caching in CI** so the ~570 MB NativeAOT toolchain (ILCompiler + runtime
  packs) is not re-downloaded on every run.
- **Windows MSI installer** (`installer/ClaudeSwitch.wxs`, WiX v5): a per-user
  installer (no admin) with a small wizard that installs the native binary into
  `%LOCALAPPDATA%\ClaudeRouter`, runs the engine's `setup`, and runs `uninstall` on
  removal. Built on a Windows runner and attached to each release. The release
  workflow also has a **signing hook** that activates when `WINDOWS_CERT_PFX_BASE64`
  + `WINDOWS_CERT_PASSWORD` secrets are added (the MSI ships unsigned until then).
- Per-platform icon assets: `assets/*.png` (Linux) and `assets/*.icns` (macOS),
  generated from the existing `.ico` sources.
- Open-source project scaffolding: MIT `LICENSE`, `CONTRIBUTING.md`,
  `SECURITY.md`, `CODE_OF_CONDUCT.md`, this changelog, issue/PR templates, and
  GitHub Actions build/lint checks.

### Changed
- Renamed the project to **ClaudeSwitch** (repo slug `claude-switch`); the engine
  keeps its name, **ClaudeRouter**.
- **`build.bat` / `install.sh` now build via `dotnet publish`** (NativeAOT) instead
  of the in-box C# compiler, then run the binary's `setup`. Building now requires
  the .NET 10 SDK plus each OS's native toolchain.
- Background launches (the watcher and each Claude window) are now fully detached,
  so a terminal or an OS-invoked `handle` returns immediately.
- Release builds emit no debug symbols (`DebugType=none`), so the published output
  is just the native binary + `assets/` — dropping the ~11 MB `ClaudeRouter.pdb` on
  Windows and the `.dbg` on Unix.
- Windows is now distributed as a **prebuilt MSI** (a deliberate, Windows-only
  reversal of the source-only policy); macOS and Linux still build from source.

### Fixed
- No more console window flashing on Windows when opening an account (or on a
  `claude://` login callback): the app is built as `WinExe` (GUI subsystem) so the
  shortcut-launched `launch`/`handle` commands run without allocating a console.
- CI builds the NativeAOT binary on Windows, macOS and Linux runners; README,
  CONTRIBUTING and the platform-support table rewritten around the single app, with
  the two inherently best-effort UI details (Linux window grouping, macOS Dock
  icons) called out honestly.

### Removed
- The original Windows-only WinForms implementation (`src/ClaudeRouter.cs`) and the
  Bash Linux/macOS port (`src/claude-router.sh`), both superseded by the unified
  .NET engine.
- Prebuilt binaries are not committed; build from source instead.

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
