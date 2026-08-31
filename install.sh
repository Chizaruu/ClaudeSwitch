#!/usr/bin/env bash
# =====================================================================
#  install.sh  —  one-command ClaudeSwitch setup for Linux and macOS
#
#  The Unix counterpart to build.bat. There is nothing to compile here:
#  the engine is the bash script in src/. This wrapper just makes it
#  executable and runs its `setup`, which:
#    • registers ClaudeSwitch as the claude:// login handler,
#    • creates the per-account launchers (each with its own icon),
#    • installs and starts the self-heal watcher, and
#    • on macOS, downloads and installs Claude first if it is missing.
#
#  USAGE:
#    ./install.sh
#
#  Linux note: if Claude is not on your PATH, point this at your binary
#  or AppImage:
#    CLAUDE_BIN="$HOME/Applications/Claude.AppImage" ./install.sh
# =====================================================================

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ENGINE="$HERE/src/claude-router.sh"

if [ ! -f "$ENGINE" ]; then
  echo "ERROR: could not find src/claude-router.sh next to this installer." >&2
  echo "Run install.sh from inside the unzipped ClaudeSwitch folder." >&2
  exit 1
fi

case "$(uname -s)" in
  Linux|Darwin) ;;
  *) echo "This installer is for Linux and macOS. On Windows, double-click build.bat instead." >&2; exit 1 ;;
esac

chmod +x "$ENGINE" 2>/dev/null || true

echo "Setting up ClaudeSwitch..."
"$ENGINE" setup
echo
echo "Done. You can close this window."
