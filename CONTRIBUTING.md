# Contributing

Thanks for your interest in improving **ClaudeSwitch**! This is a small,
focused utility, and contributions of all sizes are welcome — from typo fixes to
hardening the Linux/macOS engine.

## Ways to help

- **Exercise the Linux/macOS engine on real hardware.** `src/claude-router.sh`
  implements the full feature set, but the two *best-effort* UI pieces —
  per-account window grouping on Linux (`StartupWMClass`, WM-dependent) and
  per-account Dock icons on macOS — benefit most from real-world reports (attach
  `router.log`, and note your OS / desktop environment / Claude version).
- **Fix bugs** in the Windows router or the Unix engine.
- **Improve docs** — clearer setup steps, screenshots, troubleshooting.
- **Add small features** that fit the tool's scope (e.g. more than two accounts).

## Development setup

### Windows (`src/ClaudeRouter.cs`)

No SDK download is required — the C# compiler ships with Windows.

```bat
:: From the repo root, build + install:
build.bat

:: Or compile manually to inspect the output:
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /nologo /target:winexe /out:ClaudeRouter.exe ^
  /reference:System.Management.dll /reference:System.Windows.Forms.dll ^
  src\ClaudeRouter.cs
```

Useful subcommands while developing: `status`, `tag`, `register`, `launch Work`.

### Linux / macOS (`src/claude-router.sh`)

The engine is a single Bash script — no build step. Install it (or just probe it):

```bash
./install.sh                      # full setup (register + launchers + watcher)
./src/claude-router.sh status     # read-only report
```

Keep it **Bash 3.2-compatible** (that's what stock macOS ships): no `${arr[-1]}`,
no `${var,,}`, no associative arrays. Run both checks before submitting — CI runs
them on Ubuntu and macOS:

```bash
bash -n src/claude-router.sh install.sh uninstall.sh   # syntax
shellcheck src/claude-router.sh install.sh uninstall.sh # lint (keep it clean)
```

Useful subcommands while developing: `status`, `register`, `launch Work`, `tag`,
`test`, `watch`, `handle 'claude://…'`.

## Guidelines

- **Keep it dependency-light.** The Windows build should keep compiling with the
  in-box C# compiler; the Bash engine should rely only on common tools
  (`xdg-mime`, `zenity`/`kdialog`, `osascript`, `duti`, `launchctl`).
- **Do not commit binaries.** `*.exe`/`*.dll` are git-ignored on purpose — users
  build from source. Release artifacts (if any) belong under GitHub Releases. The
  icon assets (`.ico`/`.png`/`.icns`) are committed on purpose so installs are
  turnkey.
- **Match the existing style** (see `.editorconfig`): 4-space indentation in C#,
  2-space in shell, CRLF for `.bat` files, LF for `.sh`.
- **Test before you push.** For Windows changes, run `build.bat` and confirm a
  login routes correctly. For the Unix engine, run `./install.sh`, confirm a login
  routes, and note which OS/desktop you tested on.

## Pull request process

1. Fork the repo and create a feature branch (`git checkout -b fix/handler-race`).
2. Make your change, keeping commits focused and messages descriptive.
3. Update `README.md` / `CHANGELOG.md` if behavior or usage changes.
4. Open a PR describing **what** changed, **why**, and **how you tested it**
   (OS, Claude version).

By contributing, you agree that your contributions are licensed under the
project's [MIT License](LICENSE).
