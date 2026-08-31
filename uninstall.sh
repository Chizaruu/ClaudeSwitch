#!/usr/bin/env bash
# =====================================================================
#  uninstall.sh  —  one-command ClaudeSwitch removal for Linux and macOS
#
#  Hands the claude:// handler back, removes the per-account launchers, and
#  stops/removes the self-heal watcher. Your account data (Personal/Work) and
#  router.log are left untouched.
#
#  USAGE:  ./uninstall.sh
# =====================================================================

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"

case "$(uname -s)" in
  Darwin) INSTALLED="$HOME/Library/Application Support/claude-router/ClaudeRouter" ;;
  Linux)  INSTALLED="$HOME/.local/share/claude-router/ClaudeRouter" ;;
  *) echo "This uninstaller is for Linux and macOS. On Windows, run uninstall.bat." >&2; exit 1 ;;
esac

if [ -x "$INSTALLED" ]; then
  ENGINE="$INSTALLED"
else
  # Fall back to a build sitting in this folder, if there is one.
  ENGINE="$(find "$HERE/src/bin" -type f -name ClaudeRouter -path '*/publish/*' 2>/dev/null | head -1 || true)"
fi

if [ -z "${ENGINE:-}" ] || [ ! -x "$ENGINE" ]; then
  echo "ClaudeSwitch does not appear to be installed (nothing to remove)."
  exit 0
fi

"$ENGINE" uninstall
