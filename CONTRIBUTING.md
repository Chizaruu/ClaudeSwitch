# Contributing

Thanks for your interest in improving **ClaudeSwitch**! This is a small,
focused utility, and contributions of all sizes are welcome — from typo fixes to
hardening the Linux/macOS engine.

## Ways to help

- **Exercise it on real hardware.** The engine is fully implemented on all three
  OSes, but two *best-effort* UI pieces — per-account window grouping on Linux
  (`StartupWMClass`, WM-dependent) and per-account Dock icons on macOS — benefit
  most from real-world reports (attach `router.log`, and note your OS / desktop
  environment / Claude version). The Windows COM interop in `WindowsInterop.cs`
  also deserves testing on real Windows builds.
- **Fix bugs** anywhere in `src/`.
- **Improve docs** — clearer setup steps, screenshots, troubleshooting.
- **Add small features** that fit the tool's scope (e.g. more than two accounts).

## Development setup

The engine is one C# project in [`src/`](src/), published with **NativeAOT**. You
need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and your OS's native
toolchain (MSVC "Desktop development with C++" on Windows, `clang` + `zlib` dev
headers on Linux, Xcode Command Line Tools on macOS).

```bash
# Build + install for your platform (from the repo root):
build.bat            # Windows
./install.sh         # Linux / macOS   (CLAUDE_BIN=... ./install.sh if Claude isn't on PATH)

# Or drive the compiler directly:
dotnet build   src -c Release                    # fast compile check (all platforms' code)
dotnet publish src -c Release -r linux-x64        # native binary (win-x64 / osx-arm64 / …)
./src/bin/Release/net10.0/<rid>/publish/ClaudeRouter status
```

Useful subcommands while developing: `status`, `register`, `launch Work`, `tag`,
`test`, `watch`, `handle 'claude://…'`.

**Notes for the code:**

- **NativeAOT-safe only** — no reflection-based APIs (no WMI/`System.Management`, no
  runtime COM marshalling). Windows COM uses source-generated `[GeneratedComInterface]`;
  a cross-process command line is read from the PEB via `ReadProcessMemory`.
- **`Router` stays OS-agnostic** — put anything OS-specific behind `IPlatform`.
- A plain `dotnet build` compiles every platform's code; `dotnet publish -r <rid>`
  does the real NativeAOT link and only cross-compiles for the host OS (CI covers
  the other two).

The two install scripts are small; keep them `shellcheck`-clean:

```bash
shellcheck install.sh uninstall.sh
```

## Guidelines

- **Keep it dependency-light and AOT-safe.** No extra NuGet packages unless they're
  trim/AOT-compatible; rely on the OS's own tools (`xdg-mime`, `zenity`/`kdialog`,
  `osascript`, `duti`, `launchctl`, `reg`).
- **Do not commit binaries.** `bin/`, `obj/`, `*.exe`/`*.dll` are git-ignored on
  purpose — users build from source; releases ship source bundles. The icon assets
  (`.ico`/`.png`/`.icns`) are committed on purpose so installs are turnkey.
- **Match the existing style** (see `.editorconfig`): 4-space indentation in C#,
  2-space in shell, CRLF for `.bat` files, LF for `.sh`.
- **Test before you push.** Run `build.bat` / `./install.sh`, confirm a login
  routes correctly, and note the OS / desktop / Claude version you tested on.

## Pull request process

1. Fork the repo and create a feature branch (`git checkout -b fix/handler-race`).
2. Make your change, keeping commits focused and messages descriptive.
3. Update `README.md` / `CHANGELOG.md` if behavior or usage changes.
4. Open a PR describing **what** changed, **why**, and **how you tested it**
   (OS, Claude version).

By contributing, you agree that your contributions are licensed under the
project's [MIT License](LICENSE).

## Releasing

Releases are version-driven and automatic. Bump `<Version>` in
`src/ClaudeRouter.csproj` (and roll the `CHANGELOG.md` `[Unreleased]` section into a
dated entry for that version), then merge to `main`. The **Tag** workflow creates
`v<version>` and the **Release** workflow publishes the source bundles. Don't push
tags by hand unless you want an out-of-band release — a version bump is the normal
path.
