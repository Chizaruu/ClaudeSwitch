#!/usr/bin/env bash
# =====================================================================
#  claude-router.sh  —  Linux / macOS port of the Windows claude:// router
#
#  Purpose: let a personal and a work/SSO Claude Desktop account both stay
#  signed in on one machine, in two windows, without either logging the
#  other out. Windows lets only one app own the claude:// login link, so
#  every callback lands in one instance; this broker owns the link instead
#  and forwards each login to the account you pick.
#
#  ------------------------------------------------------------------
#  STATUS OF THIS PORT  (read me)
#  ------------------------------------------------------------------
#  * The Windows version (ClaudeRouter.exe) is proven end-to-end.
#  * This bash version mirrors the same design with the correct native
#    mechanisms per OS, but has NOT been tested on Linux or macOS. Treat
#    the first real run as the test, and check router.log if a login
#    doesn't complete.
#  * LINUX: fully implemented (xdg-mime + .desktop handler, zenity/kdialog
#    chooser, forward by launching the app with --user-data-dir + url).
#    Note: an official Claude Desktop for Linux may not exist; set
#    CLAUDE_BIN to whatever Claude binary/AppImage you use.
#  * macOS: implemented as a best-effort scaffold. Two known unknowns:
#      (1) registering our handler needs an app bundle + `duti`
#          (brew install duti); done here via an AppleScript applet.
#      (2) macOS delivers claude:// to a RUNNING app via Apple Events,
#          not argv — so forwarding the url with `open --args` may start
#          a fresh instance instead of completing the login in the
#          waiting window. If that happens, that's the piece to rework.
#  ------------------------------------------------------------------
#
#  COMMANDS:
#    status        read-only report
#    test          fire a harmless claude://router-test link
#    register      make this broker the claude:// handler
#    unregister    hand the claude:// handler back
#    setup         register + create the two account launchers
#    launch NAME   start an instance (Personal|Work) + re-assert the broker
#    handle URL    internal: run by the OS when a claude:// link fires
#
#  USAGE:  ./claude-router.sh status
# =====================================================================

set -euo pipefail

# ---- Which accounts, and where each keeps its data -------------------
ACCOUNTS=("Personal" "Work")

OS="$(uname -s)"
case "$OS" in
  Linux)  PLATFORM="linux" ;;
  Darwin) PLATFORM="mac" ;;
  *) echo "Unsupported OS: $OS (this tool is Linux/macOS only)"; exit 1 ;;
esac

if [ "$PLATFORM" = "mac" ]; then
  ROUTER_HOME="${ROUTER_HOME:-$HOME/Library/Application Support/claude-router}"
  PERSONAL_DIR="${PERSONAL_DIR:-$HOME/Library/Application Support/Claude}"
  WORK_DIR="${WORK_DIR:-$HOME/Library/Application Support/Claude-Work}"
  CLAUDE_APP="${CLAUDE_APP:-/Applications/Claude.app}"
else
  ROUTER_HOME="${ROUTER_HOME:-$HOME/.local/share/claude-router}"
  PERSONAL_DIR="${PERSONAL_DIR:-$HOME/.config/Claude}"
  WORK_DIR="${WORK_DIR:-$HOME/.config/Claude-Work}"
  # Set this to your Claude binary or AppImage if "claude" isn't on PATH:
  CLAUDE_BIN="${CLAUDE_BIN:-claude}"
fi

STABLE="$ROUTER_HOME/claude-router.sh"
LOG="$ROUTER_HOME/router.log"

# ---- helpers ---------------------------------------------------------
log() { mkdir -p "$ROUTER_HOME"; printf '%s  %s\n' "$(date -Iseconds 2>/dev/null || date)" "$*" >> "$LOG"; }

account_dir() {
  case "$1" in
    Personal) printf '%s' "$PERSONAL_DIR" ;;
    Work)     printf '%s' "$WORK_DIR" ;;
    *) return 1 ;;
  esac
}

ensure_stable_copy() {
  mkdir -p "$ROUTER_HOME"
  local self; self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  if [ "$self" != "$STABLE" ]; then cp "$self" "$STABLE"; chmod +x "$STABLE"; fi
}

# Launch Claude with a given data dir; extra args (e.g. a url) are passed through.
launch_claude() {
  local dir="$1"; shift || true
  if [ "$PLATFORM" = "mac" ]; then
    open -n -a "$CLAUDE_APP" --args --user-data-dir="$dir" "$@"
  else
    "$CLAUDE_BIN" --user-data-dir="$dir" "$@" >/dev/null 2>&1 &
  fi
}

# Show the Personal/Work chooser; echo the chosen name (empty if cancelled).
choose_account() {
  if [ "$PLATFORM" = "mac" ]; then
    osascript -e 'button returned of (display dialog "Which account are you signing into?" buttons {"Personal","Work"} default button "Work" with title "Claude login")' 2>/dev/null || true
  elif command -v zenity >/dev/null 2>&1; then
    zenity --list --title="Claude login" --text="Which account are you signing into?" \
           --column="Account" "${ACCOUNTS[@]}" 2>/dev/null || true
  elif command -v kdialog >/dev/null 2>&1; then
    kdialog --title "Claude login" --menu "Which account are you signing into?" \
            Personal Personal Work Work 2>/dev/null || true
  else
    # Headless fallback: read from the terminal.
    printf 'Which account? [Personal/Work]: ' > /dev/tty
    read -r ans < /dev/tty || true
    printf '%s' "$ans"
  fi
}

# =====================================================================
cmd_status() {
  echo "===== claude-router status ($PLATFORM) ====="
  echo "Router home : $ROUTER_HOME"
  echo "Personal dir: $PERSONAL_DIR"
  echo "Work dir    : $WORK_DIR"
  if [ "$PLATFORM" = "mac" ]; then
    echo "Claude app  : $CLAUDE_APP"
    echo -n "claude:// default handler: "
    if command -v duti >/dev/null 2>&1; then duti -x claude 2>/dev/null | head -1 || echo "(none)"; else echo "(install duti to inspect)"; fi
  else
    echo "Claude bin  : $CLAUDE_BIN"
    echo -n "claude:// default handler: "
    xdg-mime query default x-scheme-handler/claude 2>/dev/null || echo "(none)"
  fi
  echo -n "router installed: "; [ -f "$STABLE" ] && echo yes || echo no
}

# ---- registration ----------------------------------------------------
register_linux() {
  ensure_stable_copy
  local appdir="$HOME/.local/share/applications"
  mkdir -p "$appdir"
  cat > "$appdir/claude-router.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Claude Login Router
Exec=$STABLE handle %u
NoDisplay=true
MimeType=x-scheme-handler/claude;
EOF
  update-desktop-database "$appdir" >/dev/null 2>&1 || true
  xdg-mime default claude-router.desktop x-scheme-handler/claude
  echo "Registered as the claude:// handler (Linux)."
}

register_mac() {
  ensure_stable_copy
  local app="$ROUTER_HOME/ClaudeRouter.app"
  rm -rf "$app"
  # An AppleScript applet that forwards the incoming URL to this script.
  osacompile -o "$app" <<APPLESCRIPT
on open location this_URL
  do shell script quoted form of "$STABLE" & " handle " & quoted form of this_URL
end open location
APPLESCRIPT
  # Declare the claude:// scheme + a bundle id in the applet's Info.plist.
  local plist="$app/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c "Add :CFBundleIdentifier string com.claukdrouter.handler" "$plist" 2>/dev/null || \
  /usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier com.claukdrouter.handler" "$plist"
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes array" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0 dict" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLName string Claude" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes array" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string claude" "$plist" 2>/dev/null || true
  # Make Launch Services see it.
  /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$app" 2>/dev/null || true
  if command -v duti >/dev/null 2>&1; then
    duti -s com.claukdrouter.handler claude all
    echo "Registered as the claude:// handler (macOS)."
  else
    echo "Applet built at: $app"
    echo "duti is not installed. Install it (brew install duti) then run:"
    echo "  duti -s com.claukdrouter.handler claude all"
  fi
}

cmd_register() {
  if [ "$PLATFORM" = "mac" ]; then register_mac; else register_linux; fi
}

cmd_unregister() {
  if [ "$PLATFORM" = "mac" ]; then
    echo "To restore: set the claude:// default back to Claude, e.g.:"
    echo "  duti -s <Claude bundle id> claude all"
    echo "(Find it with:  osascript -e 'id of app \"Claude\"')"
  else
    rm -f "$HOME/.local/share/applications/claude-router.desktop"
    update-desktop-database "$HOME/.local/share/applications" >/dev/null 2>&1 || true
    echo "Removed the router .desktop handler. Re-launch Claude to let it re-claim claude://."
  fi
}

# ---- launchers -------------------------------------------------------
cmd_setup() {
  cmd_register
  if [ "$PLATFORM" = "mac" ]; then
    for name in "${ACCOUNTS[@]}"; do
      local f="$HOME/Desktop/Claude ($name).command"
      printf '#!/usr/bin/env bash\nexec "%s" launch %s\n' "$STABLE" "$name" > "$f"
      chmod +x "$f"
      echo "  created launcher: $f"
    done
  else
    local appdir="$HOME/.local/share/applications"
    for name in "${ACCOUNTS[@]}"; do
      cat > "$appdir/claude-$name.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Claude ($name)
Exec=$STABLE launch $name
Terminal=false
EOF
      echo "  created launcher: Claude ($name)"
    done
    update-desktop-database "$appdir" >/dev/null 2>&1 || true
  fi
  echo "Use these launchers from now on (each re-claims claude:// on start)."
}

cmd_launch() {
  local name="$1"; local dir
  dir="$(account_dir "$name")" || { echo "Unknown account: $name"; exit 1; }
  launch_claude "$dir"
  sleep 3
  cmd_register >/dev/null 2>&1 || true   # app grabs claude:// at startup; take it back
  log "launched '$name' and re-asserted broker"
}

# ---- the callback handler -------------------------------------------
cmd_handle() {
  local url="$1"
  log "callback: $url"

  case "$url" in
    *router-test*)
      if [ "$PLATFORM" = "mac" ]; then
        osascript -e "display dialog \"SUCCESS - broker intercepted:\n$url\" buttons {\"OK\"} with title \"Claude Router\"" >/dev/null 2>&1 || true
      elif command -v zenity >/dev/null 2>&1; then
        zenity --info --title="Claude Router" --text="SUCCESS - broker intercepted:\n$url" >/dev/null 2>&1 || true
      else
        echo "SUCCESS - broker intercepted: $url"
      fi
      return 0 ;;
  esac

  local choice; choice="$(choose_account)"
  if [ -z "$choice" ]; then log "no target chosen; dropped"; return 0; fi
  local dir; dir="$(account_dir "$choice")" || { log "bad choice: $choice"; return 0; }

  log "forwarding to '$choice' -> $dir"
  launch_claude "$dir" "$url"
  log "forward launched"
}

# =====================================================================
main() {
  local cmd="${1:-status}"; shift || true
  case "$cmd" in
    status)     cmd_status ;;
    test)       if [ "$PLATFORM" = "mac" ]; then open "claude://router-test-12345"; else xdg-open "claude://router-test-12345" >/dev/null 2>&1 || true; fi; echo "Fired claude://router-test-12345 - a success box should appear." ;;
    register)   cmd_register ;;
    unregister) cmd_unregister ;;
    setup)      cmd_setup ;;
    launch)     cmd_launch "${1:?account name required}" ;;
    handle)     cmd_handle "${1:?url required}" ;;
    *) echo "Unknown command: $cmd"; echo "Commands: status test register unregister setup launch handle"; exit 1 ;;
  esac
}
main "$@"
