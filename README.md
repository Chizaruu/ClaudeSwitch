# ClaudeSwitch

Run two Claude Desktop accounts — for example a **personal** and a **work/SSO**
account — at the same time on one machine, in two separate windows, each with its
own colored taskbar/dock button. No more logging in and out to switch.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue.svg)](#platform-support)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

> **A stopgap, by design.** ClaudeSwitch exists to bridge the gap until Anthropic
> ships native multi-account support in Claude Desktop. It's an unofficial
> community workaround, not a supported feature. If and when Anthropic adds
> built-in account switching, you can remove this (see [Uninstall](#uninstall))
> and go back to the normal app — nothing here locks you in.
>
> _The name:_ **ClaudeSwitch** is the project; its login-routing engine is
> **`ClaudeRouter`**, one cross-platform program (source in [`src/`](src/)) built
> into a small native binary for Windows, macOS and Linux.

---

## Why this exists

An operating system lets only **one** app receive `claude://` login links, so a
work sign-in kept landing in the personal window (and vice versa). On Windows the
Store build of Claude also claimed that link at a priority nothing could override.

`ClaudeRouter` fixes this by owning the `claude://` link itself and routing each
sign-in to the account you pick. Both accounts run with their own data folder, and
each window is tagged with its own taskbar/dock identity and color — the same trick
Chrome uses for profiles.

## Features

- **Two live accounts, two windows** — stay signed in to both at once.
- **Per-account colored taskbar/dock buttons** — tell the windows apart at a glance.
- **A login chooser** — at sign-in, a small "Which account?" box routes the
  callback to the right window.
- **Self-healing** — a lightweight background watcher re-claims the `claude://`
  handler (and re-colors windows), so it survives Claude updates.
- **One small native program, no runtime to install** — a single C# codebase
  compiled with **NativeAOT** into a dependency-free native binary per OS. (Why
  NativeAOT and not .NET MAUI: MAUI has no official Linux desktop target and there
  is almost no shared GUI here to justify a UI framework — see [`src/README.md`](src/README.md).)
- **All three desktops from one codebase** — each OS uses the correct native
  mechanism for the handler, the chooser, the per-account icon, and the watcher.
  See [Platform support](#platform-support).

---

## Install (Windows)

**First install Claude Desktop with the modern (MSIX) installer** from
<https://claude.ai/download> — see [Cowork & the modern installer](#cowork--the-modern-installer)
below for why this matters. Then download **`ClaudeSwitch-<version>-x64.msi`** from
the [latest release](https://github.com/Chizaruu/ClaudeSwitch/releases/latest) and
run it. It's a **per-user install (no admin)**: it drops the `ClaudeRouter` binary in
place, registers the login router, creates the colored **Claude (Personal)** /
**Claude (Work)** shortcuts, and starts the background watcher.

> The MSI is currently **unsigned**, so SmartScreen may show "Windows protected your
> PC" — click **More info → Run anyway**. (Code signing is wired into CI and turns
> on once a certificate is added.)

Afterwards, right-click each taskbar button → **Pin to taskbar** and unpin any old
generic Claude icon. To remove it later, use **Settings → Apps** (or `uninstall.bat`).

**Prefer to build from source?** Unzip the repo and double-click `build.bat` — it
needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) plus the "Desktop
development with C++" workload (the MSVC linker), and does the same `dotnet publish`
+ `setup` the MSI packages.

## Building (macOS, Linux, or Windows from source)

macOS and Linux build from source (there's no installer for them yet). You need the
**[.NET 10 SDK](https://dotnet.microsoft.com/download)** plus the native toolchain
NativeAOT uses:

| OS | Toolchain for NativeAOT |
| --- | --- |
| **Windows** | "Desktop development with C++" (the MSVC linker), via Visual Studio or the Build Tools |
| **Linux** | `clang` and the `zlib` development headers (e.g. `sudo apt install clang zlib1g-dev`) |
| **macOS** | the Xcode Command Line Tools (`xcode-select --install`) |

The built binary itself needs **no runtime** on the end-user machine.

## Install (macOS)

**Also recommended:** [`duti`](https://github.com/moretension/duti) (`brew install
duti`) so the `claude://` handler sticks reliably.

1. Download and unzip the repository (or `git clone`).
2. In Terminal, from the unzipped folder:

   ```bash
   ./install.sh
   ```

`install.sh` builds the native binary with `dotnet publish` and runs its `setup`:
it registers the handler, builds a **Claude (Personal)** and **Claude (Work)**
launcher on your Desktop (each with its own icon), installs a `launchd` watcher,
and downloads Claude if it is missing.

## Install (Linux)

**Requirements:** a desktop with `xdg-mime`, and `zenity` **or** `kdialog` for the
chooser. Claude Desktop itself must already be present.

1. Download and unzip the repository (or `git clone`).
2. From the unzipped folder:

   ```bash
   ./install.sh
   # or, if Claude isn't on your PATH:
   CLAUDE_BIN="$HOME/Applications/Claude.AppImage" ./install.sh
   ```

`install.sh` builds the native binary and runs its `setup`: it registers the
`claude://` handler (`xdg-mime`), creates **Claude (Personal)** / **Claude (Work)**
`.desktop` launchers — each with its own icon and a distinct `StartupWMClass` so it
groups under its own taskbar button — and installs an autostart watcher.

> Prefer to inspect the code first? [`src/`](src/) is the whole program; the scripts
> just run `dotnet publish` and then the binary's `setup`. Nothing is fetched except,
> on Windows/macOS with no Claude present, the official installer from `claude.ai`.

---

## Usage

Day to day: open each account from its shortcut; at sign-in click **Personal** or
**Work** in the chooser; the watcher keeps each window colored and separated.

Management commands — run the installed binary (`ClaudeRouter` /
`ClaudeRouter.exe`), which lives in the router home (see [below](#how-it-works)):

| Command                        | What it does                                   |
| ------------------------------ | ---------------------------------------------- |
| `ClaudeRouter status`          | Show the handler, watcher and launcher state.  |
| `ClaudeRouter tag`             | Re-color / re-group the open windows now.      |
| `ClaudeRouter register`        | Re-claim the `claude://` link for the router.  |
| `ClaudeRouter launch Work`     | Open an account (also `Personal`).             |
| `ClaudeRouter primary Work`    | Choose the Cowork account (Windows). See below.|
| `ClaudeRouter test`            | Fire a harmless `claude://router-test` link.   |
| `ClaudeRouter uninstall`       | Remove the handler, launchers and watcher.     |

**Change the colors:** replace the icon files for your platform in `assets/` (keep
the names) and re-run the installer. Windows uses `Personal.ico` / `Work.ico`;
Linux uses `Personal.png` / `Work.png`; macOS uses `Personal.icns` / `Work.icns`.

**Add or rename accounts:** edit the `Accounts` array near the top of
`src/Config.cs`, add a matching icon in `assets/` for each new name, then re-run the
installer.

> **About the icons:** `Personal.*` and `Work.*` are **not** the official Claude
> icon reskinned or recolored — they're two original marks generated with Claude,
> used purely so each account gets a distinct taskbar/dock color, to steer well
> clear of Anthropic's trademarked artwork. Swap in whatever icons you like.

---

## How it works

`ClaudeRouter` is one native binary with a few subcommands (`setup`, `handle <url>`,
`launch <name>`, `watch`, `tag`, `register`/`unregister`, `status`, `uninstall`).
The orchestration is OS-agnostic; only a per-OS layer differs:

| Piece | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Owns `claude://` | `HKCU` registry | `xdg-mime` + a `.desktop` | AppleScript applet + `duti` |
| Account chooser | Win32 dialog | `zenity` / `kdialog` | `osascript` dialog |
| Per-account launcher | AUMID-tagged `.lnk` | `.desktop` w/ `StartupWMClass` + icon | wrapper `.app` w/ its own `.icns` |
| Separate identity | `AppUserModel.ID` tag + icon + regroup | `--class` (WM_CLASS) + `--user-data-dir` | `--user-data-dir` (per-account bundle icon) |
| Self-heal watcher | HKCU `Run` key | `~/.config/autostart` entry | `launchd` LaunchAgent |
| Auto-install Claude | downloads the official installer | not available (no official Linux build) | downloads the official `.dmg` |

Each account launches Claude with its own `--user-data-dir`, so the two sessions
never share state, and login forwarding relies on Claude's own single-instance lock:
relaunching a data dir with the callback URL hands the login to the window already
open for that account.

Where things live:

- **Windows** — `%LOCALAPPDATA%\ClaudeRouter\` (the binary, icons, `router.log`);
  `%APPDATA%\Claude` / `%APPDATA%\Claude-Work` (account data); an HKCU `Run` entry
  starts the watcher at logon.
- **Linux** — `~/.local/share/claude-router/`; `~/.config/Claude` /
  `~/.config/Claude-Work`; `~/.config/autostart/claude-router-watch.desktop`.
- **macOS** — `~/Library/Application Support/claude-router/` (with the wrapper
  `.app` launchers); `~/Library/Application Support/Claude` / `…/Claude-Work`;
  `~/Library/LaunchAgents/com.claudeswitch.watcher.plist`.

---

## Platform support

| Platform | Status | Notes |
| --- | --- | --- |
| **Windows** | ✅ Full | Handler, chooser, auto-install, and per-account taskbar coloring (AppUserModel.ID tagging + per-window icon + regroup) via NativeAOT source-generated COM. |
| **Linux** | ✅ Full — best-effort grouping | Handler, chooser, launchers, watcher. Per-account taskbar grouping relies on a WM that honors `StartupWMClass` (most do). Claude must already be installed. |
| **macOS** | ✅ Full — best-effort Dock icon | Handler (needs `duti`), chooser, wrapper-`.app` launchers, `launchd` watcher, and `.dmg` auto-install. The wrapper carries a per-account icon; the Dock may still show Claude's own icon once Claude is frontmost. |

The two *best-effort* items are the ones the OS won't let a helper control perfectly
— Linux window grouping (WM-dependent) and macOS Dock icons — everything else
behaves the same everywhere. If a login doesn't complete, `router.log` in the router
home records what each callback did.

## Releases

Releasing is **version-driven and automatic**. The `<Version>` in
[`src/ClaudeRouter.csproj`](src/ClaudeRouter.csproj) is the single source of truth:
bump it and merge to `main`, and [`.github/workflows/tag.yml`](.github/workflows/tag.yml)
creates the matching `v<version>` tag, which drives
[`.github/workflows/release.yml`](.github/workflows/release.yml) to publish a GitHub
Release. Ordinary commits that don't change the version are safe — no version bump,
no tag, no release. (You can still cut one by hand: `git tag v0.2.0 && git push
origin v0.2.0`.)

Each release carries the **Windows installer** (`ClaudeSwitch-<tag>-x64.msi`, built
on a Windows runner) plus **source bundles** (`.zip` + `.tar.gz`) for building on
macOS/Linux, and a `SHA256SUMS.txt` over all of them. The MSI is a prebuilt binary,
currently **unsigned** — a signing step is wired into the release workflow and
activates the moment you add a code-signing certificate as repo secrets
(`WINDOWS_CERT_PFX_BASE64` + `WINDOWS_CERT_PASSWORD`). macOS and Linux still build
from source with the .NET SDK.

## Cowork & the modern installer

Claude Desktop's **Cowork** feature requires the **modern (MSIX) install** of Claude
and refuses to run against the older **Squirrel** install (`%LOCALAPPDATA%\AnthropicClaude`)
— you'll see *"Cowork requires Claude Desktop be installed with our modern installer"*.
This is a Claude Desktop requirement, independent of ClaudeSwitch, but it interacts
with how ClaudeSwitch launches accounts:

- ClaudeSwitch launches Claude through its **MSIX app-execution alias**
  (`…\WindowsApps\claude.exe`) so the process keeps its package identity — the thing
  Cowork checks. `ClaudeRouter status` reports whether the Claude it found is MSIX or
  a legacy build.
- If your Claude is the **Squirrel** install, Cowork won't work in *any* window
  (ClaudeSwitch or not). Reinstall Claude with the modern installer from
  <https://claude.ai/download>.

**Cowork works in one account — you choose which.** Testing confirmed that Cowork
lights up in the account running Claude's **default profile** (`%APPDATA%\Claude`),
not in the second account's separate profile — Cowork is bound to that one installed
profile, and a parallel second profile can't get it. So on Windows you get **Cowork
in one account plus a second account without it** (not both). Which account is the
Cowork one is your choice:

```
ClaudeRouter primary Work      # make Work the default-profile / Cowork account
ClaudeRouter setup --primary Work   # or choose it at setup time
```

By default the **Personal** account is the Cowork one. `ClaudeRouter status` marks
the current pick as `[primary — Cowork]`.

> Switching the primary reassigns which account uses the default profile, so after
> changing it, close and re-open both accounts — and expect to sign in again, since
> each account now points at a different profile. Pick your Cowork account **before**
> signing in if you can. Getting Cowork in *both* accounts isn't possible until
> Anthropic ships native multi-account support — the gap this tool bridges.

## Troubleshooting

- **"Cowork requires … modern installer"** — see [Cowork & the modern installer](#cowork--the-modern-installer).
  Check `ClaudeRouter status` for whether your Claude is MSIX or legacy.
- **A login lands in the wrong window** — the watcher re-claims the link
  automatically; if it slips, run `ClaudeRouter register` once. Check `router.log`
  in the router home.
- **A taskbar/dock button won't separate or color** — run `ClaudeRouter tag`, or
  confirm the watcher is running with `ClaudeRouter status`.
- **`dotnet` not found / NativeAOT build fails** — install the .NET 10 SDK and your
  OS's native toolchain (see [Building](#building-macos-linux-or-windows-from-source)).

---

## Uninstall

**Windows:** run **`uninstall.bat`**. **Linux / macOS:** run **`./uninstall.sh`**.
Each hands the `claude://` handler back, removes the launchers, and stops and
removes the watcher. Your account data and `router.log` are left untouched. To go
back to a single app, open Claude once so it re-claims `claude://` (or reinstall it
from <https://claude.ai/download>).

---

## Project layout

```
claude-switch/
├── src/                    # the ClaudeRouter engine (one C# codebase, NativeAOT)
│   ├── ClaudeRouter.csproj
│   ├── Program.cs / Router.cs / Config.cs / IPlatform.cs
│   ├── WindowsPlatform.cs / MacPlatform.cs / LinuxPlatform.cs
│   ├── WindowsInterop.cs   # Windows-only COM/native interop (source-generated COM)
│   └── README.md           # architecture notes + why NativeAOT (not MAUI)
├── assets/                 # per-OS account icons (.ico / .png / .icns)
├── installer/              # Windows MSI (WiX): ClaudeSwitch.wxs + License.rtf
├── build.bat               # Windows: dotnet publish + install (double-click)
├── uninstall.bat           # Windows: one-click removal
├── install.sh              # Linux/macOS: dotnet publish + install
├── uninstall.sh            # Linux/macOS: one-click removal
├── .github/workflows/
│   ├── build.yml           # NativeAOT publish (Win/macOS/Linux) + MSI check + shellcheck
│   ├── tag.yml             # auto-tag v<Version> from the csproj on push to main
│   └── release.yml         # builds the MSI + source bundles, publishes the release
├── LICENSE                 # MIT
├── README.md
├── CONTRIBUTING.md
├── SECURITY.md
├── CODE_OF_CONDUCT.md
└── CHANGELOG.md
```

> **Note:** prebuilt binaries are intentionally **not** committed — build from
> source with the .NET SDK.

---

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build,
test, and submit changes, and please review the [Code of Conduct](CODE_OF_CONDUCT.md).
The most valuable real-world testing is the two best-effort UI details — Linux
window grouping and macOS Dock icons — on actual hardware.

## Security

This tool registers itself as the per-user `claude://` handler (the `HKCU` registry
on Windows, `xdg-mime` on Linux, `duti` / Launch Services on macOS), downloads the
official Claude installer when Claude is absent (Windows and macOS), and starts a
background watcher at login. It requires no admin rights and touches only per-user
state. To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

Released under the [MIT License](LICENSE). © 2026 Abdul-Kadir Coskun.

## Disclaimer

Not affiliated with, endorsed by, or supported by Anthropic. "Claude" is a
trademark of Anthropic. The icons in `assets/` are original marks generated with
Claude — not Anthropic's official icon reskinned or modified — and are included
only to color-code the two accounts. Use at your own risk; the software is
provided "as is".
