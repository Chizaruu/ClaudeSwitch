#!/usr/bin/env bash
# =====================================================================
#  uninstall.sh  —  one-command ClaudeSwitch removal for Linux and macOS
#
#  The Unix counterpart to uninstall.bat. Hands the claude:// handler
#  back, removes the per-account launchers, and stops/removes the
#  self-heal watcher. Your account data (Personal/Work) and router.log
#  are left untouched.
#
#  USAGE:  ./uninstall.sh
# =====================================================================

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"

case "$(uname -s)" in
  Darwin) INSTALLED="$HOME/Library/Application Support/claude-router/claude-router.sh" ;;
  Linux)  INSTALLED="$HOME/.local/share/claude-router/claude-router.sh" ;;
  *) echo "This uninstaller is for Linux and macOS. On Windows, run uninstall.bat instead." >&2; exit 1 ;;
esac

# Prefer the installed copy; fall back to the one in this repo folder.
if [ -f "$INSTALLED" ]; then
  ENGINE="$INSTALLED"
elif [ -f "$HERE/src/claude-router.sh" ]; then
  ENGINE="$HERE/src/claude-router.sh"
else
  echo "ClaudeSwitch does not appear to be installed (nothing to remove)."
  exit 0
fi

chmod +x "$ENGINE" 2>/dev/null || true
"$ENGINE" uninstall
