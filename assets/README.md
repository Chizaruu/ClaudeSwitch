# Icons

These give each Claude account its own colored taskbar/dock button. Each account
has one icon per platform, in the format that platform's shell expects:

| File            | Used by | Account  |
| --------------- | ------- | -------- |
| `Personal.ico`  | Windows | Personal |
| `Work.ico`      | Windows | Work     |
| `Personal.png`  | Linux   | Personal |
| `Work.png`      | Linux   | Work     |
| `Personal.icns` | macOS   | Personal |
| `Work.icns`     | macOS   | Work     |

The PNG and ICNS files are generated from the ICO sources, so all three platforms
share the same two marks.

These are **original icons generated with Claude** — they are **not** the official
Claude Desktop icon reskinned, recolored, or otherwise modified. They exist only to
color-code the two accounts, and were made this way deliberately to avoid using
Anthropic's trademarked artwork.

Swap in your own icons (keep the same names) and re-run the installer for your
platform (`build.bat` on Windows, `install.sh` on Linux/macOS) to change the look.
