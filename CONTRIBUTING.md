# Contributing

Thanks for your interest in improving **ClaudeSwitch**! This is a small,
focused utility, and contributions of all sizes are welcome — from typo fixes to
hardening the experimental Linux/macOS port.

## Ways to help

- **Test the Unix port.** The Linux/macOS Bash implementation
  (`src/claude-router.sh`) is untested in the wild. Trying it and reporting what
  happens (with `router.log`) is genuinely valuable.
- **Fix bugs** in the Windows router or the Bash port.
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

```bash
chmod +x src/claude-router.sh
./src/claude-router.sh status
```

Run it through [ShellCheck](https://www.shellcheck.net/) before submitting:

```bash
shellcheck src/claude-router.sh
```

## Guidelines

- **Keep it dependency-free.** The Windows build should keep compiling with the
  in-box C# compiler; the Bash port should rely only on common tools
  (`xdg-mime`, `zenity`/`kdialog`, `osascript`, `duti`).
- **Do not commit binaries.** `*.exe`/`*.dll` are git-ignored on purpose — users
  build from source. Release artifacts (if any) belong under GitHub Releases.
- **Match the existing style** (see `.editorconfig`): 4-space indentation in C#,
  2-space in shell, CRLF for `.bat` files.
- **Test before you push.** For Windows changes, run `build.bat` and confirm a
  login routes correctly. For the Unix port, note which OS/desktop you tested on.

## Pull request process

1. Fork the repo and create a feature branch (`git checkout -b fix/handler-race`).
2. Make your change, keeping commits focused and messages descriptive.
3. Update `README.md` / `CHANGELOG.md` if behavior or usage changes.
4. Open a PR describing **what** changed, **why**, and **how you tested it**
   (OS, Claude version).

By contributing, you agree that your contributions are licensed under the
project's [MIT License](LICENSE).
