# ClaudeSwitch

Run two Claude Desktop accounts — for example a **personal** and a **work/SSO**
account — at the same time on one machine, in two separate windows, each with its
own colored taskbar button. No more logging in and out to switch.

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
> `ClaudeRouter.exe` on Windows (compiled and installed by `build.bat`) and
> `claude-router.sh` on Linux/macOS (installed by `install.sh`).

---

## Why this exists

Windows lets only **one** app receive `claude://` login links, so a work sign-in
kept landing in the personal window (and vice versa). The Windows Store build of
Claude also claimed that link at a priority nothing could override.

`ClaudeRouter` fixes this by owning the `claude://` link itself and routing each
sign-in to the account you pick. Both accounts run from a single portable copy of
Claude, each with its own data folder, and each window is tagged with its own
taskbar identity and color — the same trick Chrome uses for profiles.

## Features

- **Two live accounts, two windows** — stay signed in to both at once.
- **Per-account colored taskbar buttons** — tell the windows apart at a glance.
- **A login chooser** — at sign-in, a small "Which account?" box routes the
  callback to the right window.
- **Self-healing** — a lightweight background watcher re-claims the `claude://`
  handler (and, on Windows, re-colors windows), so it survives Claude updates.
- **One small native program** — no runtime to install; on Windows it compiles
  with the C# compiler that already ships with the OS, and on Linux/macOS it's a
  single dependency-light Bash script.
- **All three desktops** — Windows (`ClaudeRouter.exe`), Linux and macOS
  (`claude-router.sh`) each use the correct native mechanism for the login
  handler, the account chooser, the per-account taskbar/dock icon, and the
  self-heal watcher. See [Platform support](#platform-support) for what's
  fully native versus best-effort on each OS.

---

## Install (Windows)

**Requirements:** Windows 10/11. No separate .NET download is needed — `build.bat`
uses the C# compiler bundled with Windows.

1. [Download this repository](https://github.com/Chizaruu/claude-switch/archive/refs/heads/main.zip)
   (or `git clone`) and unzip it.
2. **Double-click `build.bat`.**

That's the whole thing. `build.bat` compiles `ClaudeRouter.exe` from source, makes
the portable copy the two accounts run from, creates the colored desktop shortcuts,
registers the login router, starts the background watcher, and removes the old
single-app install. If Claude isn't installed yet, it downloads and installs it
first. Follow the box it shows at the end.

Then open both accounts from the **Claude (Personal)** and **Claude (Work)** desktop
icons, right-click each button → **Pin to taskbar**, and unpin any old generic
Claude icon.

> Prefer to inspect the code first? `src/ClaudeRouter.cs` is the entire program —
> `build.bat` just compiles and runs it. Nothing is fetched at build time except,
> if you don't already have Claude, the official installer from `claude.ai`.

---

## Install (macOS)

**Requirements:** macOS 12+. [`duti`](https://github.com/moretension/duti)
(`brew install duti`) is recommended so the `claude://` handler sticks reliably.

1. Download and unzip this repository (or `git clone`).
2. In Terminal, from the unzipped folder:

   ```bash
   ./install.sh
   ```

`install.sh` runs the engine's `setup`: it registers ClaudeSwitch as the
`claude://` handler, builds a **Claude (Personal)** and **Claude (Work)** launcher
on your Desktop (each carrying its own icon), and installs a login-item watcher
(a `launchd` LaunchAgent) that re-claims the handler after Claude updates. If
Claude Desktop isn't installed, it downloads and installs the official app first.

Open each account from its Desktop launcher; keep both in the Dock if you like.

## Install (Linux)

**Requirements:** a desktop environment with `xdg-mime`, and `zenity` **or**
`kdialog` for the account chooser. Claude Desktop itself must already be present —
point the tool at your Claude binary or AppImage if it isn't on your `PATH`.

1. Download and unzip this repository (or `git clone`).
2. From the unzipped folder:

   ```bash
   ./install.sh
   # or, if Claude isn't on your PATH:
   CLAUDE_BIN="$HOME/Applications/Claude.AppImage" ./install.sh
   ```

`install.sh` runs the engine's `setup`: it registers ClaudeSwitch as the
`claude://` handler (`xdg-mime`), creates **Claude (Personal)** / **Claude (Work)**
`.desktop` launchers in your applications menu — each with its own icon and a
distinct `StartupWMClass` so it groups under its own taskbar button — and installs
an autostart watcher that re-claims the handler after Claude updates.

> Prefer to inspect the code first? `src/claude-router.sh` is the entire engine —
> `install.sh` just makes it executable and runs its `setup`. Nothing is fetched
> except, on macOS with no Claude present, the official installer from `claude.ai`.

---

## Usage

Day to day:

- Open each account from its desktop shortcut.
- At sign-in, click **Personal** or **Work** in the chooser — the login lands in
  the right window.
- The background watcher keeps each window colored and separated automatically.

**Windows** management commands (run from `%LOCALAPPDATA%\ClaudeRouter`, or use the
shortcuts):

| Command                        | What it does                                   |
| ------------------------------ | ---------------------------------------------- |
| `ClaudeRouter.exe status`      | Show the current handler + watcher state.      |
| `ClaudeRouter.exe tag`         | Recolor the open windows right now.            |
| `ClaudeRouter.exe register`    | Re-claim the `claude://` link for the router.  |
| `ClaudeRouter.exe launch Work` | Open an account (also `Personal`).             |
| `uninstall.bat`                | Remove the router + shortcuts.                 |

**Linux / macOS** management commands (the installed engine lives at
`~/.local/share/claude-router/claude-router.sh` on Linux and
`~/Library/Application Support/claude-router/claude-router.sh` on macOS):

| Command                              | What it does                                        |
| ------------------------------------ | --------------------------------------------------- |
| `claude-router.sh status`            | Show the handler, watcher, and launcher state.      |
| `claude-router.sh tag`               | Re-assert the per-account launcher identity now.    |
| `claude-router.sh register`          | Re-claim the `claude://` link for the router.       |
| `claude-router.sh launch Work`       | Open an account (also `Personal`).                  |
| `claude-router.sh test`              | Fire a harmless `claude://router-test` link.        |
| `./uninstall.sh`                     | Remove the handler, launchers + watcher.            |

**Change the colors:** replace the icon files for your platform in `assets/` (keep
the names) and re-run the installer. Windows uses `Personal.ico` / `Work.ico`;
Linux uses `Personal.png` / `Work.png`; macOS uses `Personal.icns` / `Work.icns`.

> **About the icons:** `Personal.ico` and `Work.ico` are **not** the official
> Claude icon reskinned or recolored — they're two original marks generated with
> Claude, used purely so each account gets a distinct taskbar color. That's a
> deliberate choice to steer well clear of Anthropic's trademarked artwork (and,
> you know, not end up broke). Swap in whatever icons you like.

**Add or rename accounts:** edit the `Accounts` array near the top of
`src/ClaudeRouter.cs` and the `ACCOUNTS` array in `src/claude-router.sh`, add a
matching icon in `assets/` for each new name, then re-run the installer for your
platform.

---

## How it works

`ClaudeRouter.exe` is a single native executable with a few subcommands:

| Subcommand      | Role                                                              |
| --------------- | ---------------------------------------------------------------- |
| `setup`         | Register the handler, make shortcuts, start the watcher.         |
| `handle <url>`  | Run by Windows on a `claude://` callback; shows the chooser and forwards the login. |
| `launch <name>` | Start `Personal`/`Work` and re-claim the handler.                |
| `watch`         | Resident watcher that re-registers the handler and colors windows. |
| `tag`           | Color the open windows once.                                     |

Each account launches Claude with its own `--user-data-dir`, so the two sessions
never share state. Windows are tagged via the `AppUserModel.ID` property (plus a
relaunch command and per-account icon) so a pinned shortcut and its window collapse
into one colored taskbar button per account.

Where things live on Windows:

- `%LOCALAPPDATA%\ClaudeRouter\` — `ClaudeRouter.exe`, the icons, and `router.log`.
- `%LOCALAPPDATA%\ClaudePortable\` — the portable Claude app both accounts run from.
- `%APPDATA%\Claude` — Personal account data. `%APPDATA%\Claude-Work` — Work data.
- Startup shortcut `ClaudeRouterWatcher.lnk` — starts the watcher at each logon.

### On Linux and macOS

`claude-router.sh` has the same subcommands (`setup`, `handle`, `launch`, `watch`,
`tag`, `register` / `unregister`, `status`, `uninstall`) and uses each OS's native
building blocks:

| Piece                     | Linux                                             | macOS                                                      |
| ------------------------- | ------------------------------------------------- | ---------------------------------------------------------- |
| Owns `claude://`          | `xdg-mime` default + a `claude-router.desktop`    | an AppleScript applet + `duti` / Launch Services           |
| Account chooser           | `zenity` / `kdialog` (terminal fallback)          | `osascript` dialog                                         |
| Per-account launcher      | a `.desktop` with a distinct `StartupWMClass` and its own PNG | a wrapper `.app` bundle carrying its own `.icns`  |
| Separate identity at launch | `--class=ClaudeRouter-<name>` + `--user-data-dir` | `open -n` with `--user-data-dir` (per-account bundle icon) |
| Self-heal watcher         | a background loop + a `~/.config/autostart` entry | a background loop + a `launchd` LaunchAgent (`KeepAlive`)   |
| Auto-install Claude        | not available (no official Linux installer)      | downloads the official `.dmg` from `claude.ai`             |

Login forwarding works the same way on every OS: each account runs with its own
`--user-data-dir`, so relaunching that data dir with the callback URL hits Claude's
own single-instance lock and hands the login to the already-open window.

Where things live on Linux:

- `~/.local/share/claude-router/` — the installed `claude-router.sh`, the icons,
  and `router.log`.
- `~/.config/Claude` — Personal data. `~/.config/Claude-Work` — Work data.
- `~/.local/share/applications/claude-{Personal,Work}.desktop` — the launchers.
- `~/.config/autostart/claude-router-watch.desktop` — starts the watcher at login.

Where things live on macOS:

- `~/Library/Application Support/claude-router/` — the installed script, the
  wrapper `.app` launchers, the icons, and `router.log`.
- `~/Library/Application Support/Claude` — Personal data. `…/Claude-Work` — Work data.
- `~/Library/LaunchAgents/com.claudeswitch.watcher.plist` — the watcher LaunchAgent.

---

## Platform support

| Platform    | Status                  | Notes                                                                 |
| ----------- | ----------------------- | --------------------------------------------------------------------- |
| **Windows** | ✅ Proven                | The primary, end-to-end tested implementation (`src/ClaudeRouter.cs`).|
| **Linux**   | ✅ Full — best-effort UI | `src/claude-router.sh`: handler, chooser, per-account launchers with `StartupWMClass` grouping, and an autostart watcher. Per-account taskbar grouping needs a WM that honours `StartupWMClass` (most do). Claude must already be installed — set `CLAUDE_BIN` if it isn't on your `PATH`. |
| **macOS**   | ✅ Full — best-effort UI | `src/claude-router.sh`: handler (needs `duti`), chooser, wrapper-`.app` launchers, a `launchd` watcher, and automatic download/install of Claude when absent. Each account launches with its own data dir; the wrapper carries a per-account icon (the Dock may still show Claude's own icon once Claude is frontmost). |

Every feature has a working implementation on all three platforms. The two items
marked *best-effort* are the ones the OS doesn't let a helper control perfectly —
per-account **window grouping** on Linux (WM-dependent) and per-account **Dock
icons** on macOS — everything else (routing, the chooser, launchers, the self-heal
watcher, uninstall) behaves the same everywhere. If a login doesn't complete,
`router.log` records what each callback did. Reports and fixes are welcome — see
[Contributing](#contributing).

```bash
# Linux / macOS
./install.sh                      # register + launchers + watcher (full setup)
./src/claude-router.sh status     # read-only report
./uninstall.sh                    # remove everything (keeps your data)
```

---

## Troubleshooting

- **A login lands in the wrong window** — the watcher re-claims the link
  automatically, so this should be rare. If it happens, run
  `ClaudeRouter.exe register` once. `%LOCALAPPDATA%\ClaudeRouter\router.log`
  records what each callback and tag did.
- **A taskbar button won't separate / won't color** — run `ClaudeRouter.exe tag`,
  or check that the watcher is running with `ClaudeRouter.exe status`.
- **Windows Defender flags an unsigned build** — building locally with `build.bat`
  (rather than distributing a prebuilt `.exe`) is the reliable path; the code you
  compile is right there in `src/`.

If you're stuck, [open an issue](https://github.com/Chizaruu/claude-switch/issues)
and attach `router.log`.

---

## Uninstall

**Windows:** run **`uninstall.bat`**. It restores the default `claude://` handler,
stops the watcher, and removes the shortcuts. Your login backups are kept under
`%LOCALAPPDATA%\ClaudeRouter\session-backup-*`.

**Linux / macOS:** run **`./uninstall.sh`**. It hands the `claude://` handler back,
removes the per-account launchers, and stops and removes the watcher (the autostart
entry on Linux, the LaunchAgent on macOS). Your account data and `router.log` are
left untouched.

To go back to a single app, open Claude once so it re-claims `claude://` (or
reinstall it from <https://claude.ai/download>).

---

## Project layout

```
claude-switch/
├── src/
│   ├── ClaudeRouter.cs     # Windows implementation (the whole program)
│   └── claude-router.sh    # Linux/macOS engine (full parity, best-effort UI)
├── assets/
│   ├── Personal.ico        # Windows taskbar icon — Personal account
│   ├── Work.ico            # Windows taskbar icon — Work account
│   ├── Personal.png        # Linux taskbar icon — Personal account
│   ├── Work.png            # Linux taskbar icon — Work account
│   ├── Personal.icns       # macOS Dock icon — Personal account
│   └── Work.icns           # macOS Dock icon — Work account
├── build.bat               # Windows: compile + install (double-click)
├── uninstall.bat           # Windows: one-click removal
├── install.sh              # Linux/macOS: one-command setup
├── uninstall.sh            # Linux/macOS: one-command removal
├── LICENSE                 # MIT
├── README.md
├── CONTRIBUTING.md
├── SECURITY.md
├── CODE_OF_CONDUCT.md
└── CHANGELOG.md
```

> **Note:** prebuilt binaries are intentionally **not** committed. Build from
> source with `build.bat`. Signed release binaries may be published under
> [Releases](https://github.com/Chizaruu/claude-switch/releases) in the
> future.

---

## Contributing

Contributions are welcome — especially testing and hardening the Linux/macOS port.
See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test, and submit changes,
and please review the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

This tool registers itself as the per-user `claude://` handler (the `HKCU`
registry on Windows, `xdg-mime` on Linux, `duti` / Launch Services on macOS),
downloads the official Claude installer when Claude is absent (Windows and macOS),
and starts a background watcher at login. It requires no admin rights and touches
only per-user state. To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

Released under the [MIT License](LICENSE). © 2026 Abdul-Kadir Coskun.

## Disclaimer

Not affiliated with, endorsed by, or supported by Anthropic. "Claude" is a
trademark of Anthropic. The icons in `assets/` are original marks generated with
Claude — not Anthropic's official icon reskinned or modified — and are included
only to color-code the two accounts. Use at your own risk; the software is
provided "as is".
