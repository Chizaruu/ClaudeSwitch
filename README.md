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
> `ClaudeRouter.exe` on Windows (and `claude-router.sh` on Linux/macOS), which is
> what `build.bat` compiles and installs.

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
  handler and re-colors windows, so it survives Claude updates.
- **One small native program** — no runtime to install; on Windows it compiles
  with the C# compiler that already ships with the OS.
- **Cross-platform port** — a Bash implementation for Linux and macOS is included
  (experimental — see [Platform support](#platform-support)).

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

## Usage

Day to day:

- Open each account from its desktop shortcut.
- At sign-in, click **Personal** or **Work** in the chooser — the login lands in
  the right window.
- The background watcher keeps each window colored and separated automatically.

Management commands (run from `%LOCALAPPDATA%\ClaudeRouter`, or use the shortcuts):

| Command                        | What it does                                   |
| ------------------------------ | ---------------------------------------------- |
| `ClaudeRouter.exe status`      | Show the current handler + watcher state.      |
| `ClaudeRouter.exe tag`         | Recolor the open windows right now.            |
| `ClaudeRouter.exe register`    | Re-claim the `claude://` link for the router.  |
| `ClaudeRouter.exe launch Work` | Open an account (also `Personal`).             |
| `uninstall.bat`                | Remove the router + shortcuts.                 |

**Change the colors:** replace `assets/Personal.ico` / `assets/Work.ico` (keep the
names) and run `build.bat` again.

> **About the icons:** `Personal.ico` and `Work.ico` are **not** the official
> Claude icon reskinned or recolored — they're two original marks generated with
> Claude, used purely so each account gets a distinct taskbar color. That's a
> deliberate choice to steer well clear of Anthropic's trademarked artwork (and,
> you know, not end up broke). Swap in whatever icons you like.

**Add or rename accounts:** edit the `Accounts` array near the top of
`src/ClaudeRouter.cs` (and the `ACCOUNTS` array in `src/claude-router.sh` for the
Unix port), then rebuild.

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

---

## Platform support

| Platform    | Status         | Notes                                                                 |
| ----------- | -------------- | --------------------------------------------------------------------- |
| **Windows** | ✅ Proven       | The primary, end-to-end tested implementation (`src/ClaudeRouter.cs`).|
| **Linux**   | 🧪 Experimental | Implemented in `src/claude-router.sh` (xdg-mime + `.desktop` handler, zenity/kdialog chooser). Untested — set `CLAUDE_BIN` to your Claude binary/AppImage. |
| **macOS**   | 🧪 Experimental | Best-effort scaffold in `src/claude-router.sh`. Registering the handler needs `duti` (`brew install duti`); forwarding a `claude://` URL to a running app via Apple Events is the piece most likely to need rework. |

The Bash port mirrors the Windows design but has **not** been tested on Linux or
macOS. Treat the first real run as the test, and check `router.log` if a login
doesn't complete. Reports and fixes are very welcome — see
[Contributing](#contributing).

```bash
# Unix port (Linux/macOS)
chmod +x src/claude-router.sh
./src/claude-router.sh status     # read-only report
./src/claude-router.sh setup      # register + create the two launchers
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

Run **`uninstall.bat`**. It restores the default `claude://` handler, stops the
watcher, and removes the shortcuts. Your login backups are kept under
`%LOCALAPPDATA%\ClaudeRouter\session-backup-*`. To go back to a single app,
reinstall the normal Claude from <https://claude.ai/download>.

---

## Project layout

```
claude-switch/
├── src/
│   ├── ClaudeRouter.cs     # Windows implementation (the whole program)
│   └── claude-router.sh    # Linux/macOS port (experimental)
├── assets/
│   ├── Personal.ico        # taskbar color/icon for the Personal account
│   └── Work.ico            # taskbar color/icon for the Work account
├── build.bat               # Windows: compile + install (double-click)
├── uninstall.bat           # Windows: one-click removal
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

This tool modifies the current user's registry (`HKCU`), downloads the official
Claude installer when Claude is absent, and starts a background process at logon.
It requires no admin rights and touches only per-user state. To report a
vulnerability, see [SECURITY.md](SECURITY.md).

## License

Released under the [MIT License](LICENSE). © 2026 Abdul-Kadir Coskun.

## Disclaimer

Not affiliated with, endorsed by, or supported by Anthropic. "Claude" is a
trademark of Anthropic. The icons in `assets/` are original marks generated with
Claude — not Anthropic's official icon reskinned or modified — and are included
only to color-code the two accounts. Use at your own risk; the software is
provided "as is".
