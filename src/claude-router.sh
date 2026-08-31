#!/usr/bin/env bash
# =====================================================================
#  claude-router.sh  —  Linux / macOS engine for ClaudeSwitch
#
#  Run a personal and a work/SSO Claude Desktop account at the same time,
#  in two separate windows, each with its own data folder and its own
#  taskbar/dock icon. Only one app on a machine can own the claude://
#  login link, so every sign-in callback lands in one window; this broker
#  owns the link instead and forwards each login to the account you pick.
#
#  This is the Unix counterpart to the Windows engine (ClaudeRouter.exe).
#  It aims for the same feature set with the correct native mechanism on
#  each OS:
#
#    * claude:// handler   Linux: xdg-mime + a .desktop handler
#                          macOS: an AppleScript applet + duti/Launch Services
#    * account chooser     Linux: zenity / kdialog / terminal
#                          macOS: osascript dialog
#    * per-account icon    Linux: a distinct WM_CLASS (--class) + a .desktop
#                                 launcher with a matching StartupWMClass and
#                                 its own PNG, so each account groups under
#                                 its own coloured taskbar button
#                          macOS: a per-account wrapper .app bundle carrying
#                                 its own .icns, used as the launcher
#    * self-heal watcher   Linux: a background loop + ~/.config/autostart entry
#                          macOS: a background loop + a LaunchAgent (KeepAlive)
#    * auto-install        macOS: downloads the official Claude .dmg if absent
#                          Linux: detects your Claude binary/AppImage and, if
#                                 none is found, tells you how to point at it
#
#  COMMANDS
#    status         read-only report
#    test           fire a harmless claude://router-test link
#    register       make this broker the claude:// handler
#    unregister     hand the claude:// handler back
#    setup          register + build the launchers + start the watcher
#                   (installs Claude first on macOS if it is missing)
#    install        alias for setup (what install.sh runs)
#    uninstall      remove the handler, launchers and watcher (keeps your data)
#    launch NAME    start an account (Personal|Work) + re-assert the broker
#    watch          resident self-heal loop (re-claims the handler)
#    tag            re-assert the per-account launcher identity now
#    handle URL     internal: run by the OS when a claude:// link fires
#
#  USAGE:  ./claude-router.sh setup
# =====================================================================

set -euo pipefail

# ---- Which accounts exist. Add more here (and they get their own icon
#      if assets/<Name>.png / <Name>.icns are present). ----------------
ACCOUNTS=("Personal" "Work")

# ---- App identity (kept in sync with Windows/macOS bundle ids) --------
APP_ID="com.claudeswitch"
HANDLER_ID="${APP_ID}.handler"
WATCHER_ID="${APP_ID}.watcher"

# ---- Platform detection ----------------------------------------------
OS="$(uname -s)"
case "$OS" in
  Linux)  PLATFORM="linux" ;;
  Darwin) PLATFORM="mac" ;;
  *) echo "Unsupported OS: $OS (this engine is Linux/macOS only; use ClaudeRouter.exe on Windows)"; exit 1 ;;
esac

if [ "$PLATFORM" = "mac" ]; then
  ROUTER_HOME="${ROUTER_HOME:-$HOME/Library/Application Support/claude-router}"
  PERSONAL_DIR="${PERSONAL_DIR:-$HOME/Library/Application Support/Claude}"
  WORK_DIR="${WORK_DIR:-$HOME/Library/Application Support/Claude-Work}"
  CLAUDE_APP="${CLAUDE_APP:-/Applications/Claude.app}"
  DESKTOP_DIR="${DESKTOP_DIR:-$HOME/Desktop}"
  LAUNCH_AGENTS="${LAUNCH_AGENTS:-$HOME/Library/LaunchAgents}"
  ICON_EXT="icns"
else
  ROUTER_HOME="${ROUTER_HOME:-$HOME/.local/share/claude-router}"
  PERSONAL_DIR="${PERSONAL_DIR:-$HOME/.config/Claude}"
  WORK_DIR="${WORK_DIR:-$HOME/.config/Claude-Work}"
  # Point this at your Claude binary or AppImage if "claude" is not on PATH:
  CLAUDE_BIN="${CLAUDE_BIN:-}"
  DESKTOP_DIR="${DESKTOP_DIR:-$HOME/.local/share/applications}"
  AUTOSTART_DIR="${AUTOSTART_DIR:-$HOME/.config/autostart}"
  ICON_EXT="png"
fi

STABLE="$ROUTER_HOME/claude-router.sh"
LOG="$ROUTER_HOME/router.log"
PIDFILE="$ROUTER_HOME/watcher.pid"

# Where the packaged icons live, relative to this script (src/../assets).
SELF_PATH="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
REPO_ASSETS="$(cd "$(dirname "$0")/.." 2>/dev/null && pwd)/assets"

# ---- helpers ---------------------------------------------------------
log() {
  mkdir -p "$ROUTER_HOME" 2>/dev/null || true
  printf '%s  %s\n' "$(date -Iseconds 2>/dev/null || date)" "$*" >> "$LOG" 2>/dev/null || true
}

is_account() {
  local a
  for a in "${ACCOUNTS[@]}"; do [ "$a" = "$1" ] && return 0; done
  return 1
}

account_dir() {
  case "$1" in
    Personal) printf '%s' "$PERSONAL_DIR" ;;
    Work)     printf '%s' "$WORK_DIR" ;;
    *) return 1 ;;
  esac
}

# A filesystem-safe, per-account tag reused for WM_CLASS and bundle ids.
account_slug() { printf 'ClaudeRouter-%s' "$1"; }

icon_for() { printf '%s/%s.%s' "$ROUTER_HOME" "$1" "$ICON_EXT"; }

# Copy this script into a stable install location so the handler, launchers
# and watcher all point at one path independent of the unzip folder.
ensure_stable_copy() {
  mkdir -p "$ROUTER_HOME"
  if [ "$SELF_PATH" != "$STABLE" ]; then
    cp "$SELF_PATH" "$STABLE"
    chmod +x "$STABLE"
  fi
}

# Copy packaged icons next to the installed script (best-effort; the tool
# still works without them, just without per-account colours).
install_icons() {
  [ -d "$REPO_ASSETS" ] || return 0
  local name src
  for name in "${ACCOUNTS[@]}"; do
    src="$REPO_ASSETS/$name.$ICON_EXT"
    if [ -f "$src" ]; then cp "$src" "$(icon_for "$name")" 2>/dev/null || true; fi
  done
}

# ---- locating Claude -------------------------------------------------
# Linux: resolve a usable Claude binary/AppImage. Echoes the path, or
# nothing if none is found.
find_claude_bin() {
  if [ -n "${CLAUDE_BIN:-}" ]; then
    command -v "$CLAUDE_BIN" >/dev/null 2>&1 && { command -v "$CLAUDE_BIN"; return 0; }
    [ -x "$CLAUDE_BIN" ] && { printf '%s' "$CLAUDE_BIN"; return 0; }
  fi
  local c
  for c in claude claude-desktop Claude; do
    command -v "$c" >/dev/null 2>&1 && { command -v "$c"; return 0; }
  done
  local d f
  for d in "$HOME/Applications" "$HOME/.local/bin" "$HOME/bin" "$HOME/Downloads" /opt /usr/local/bin; do
    [ -d "$d" ] || continue
    for f in "$d"/[Cc]laude*.AppImage "$d"/[Cc]laude*/[Cc]laude "$d"/[Cc]laude; do
      [ -x "$f" ] && { printf '%s' "$f"; return 0; }
    done
  done
  return 1
}

claude_present() {
  if [ "$PLATFORM" = "mac" ]; then
    [ -d "$CLAUDE_APP" ]
  else
    find_claude_bin >/dev/null 2>&1
  fi
}

# ---- launching Claude ------------------------------------------------
# launch_account NAME [url] — start (or forward a url to) an account,
# with its own data dir and its own window identity.
launch_account() {
  local name="$1"; shift || true
  local url="${1:-}"
  local dir slug
  dir="$(account_dir "$name")" || { log "unknown account: $name"; return 1; }
  slug="$(account_slug "$name")"
  mkdir -p "$dir" 2>/dev/null || true

  if [ "$PLATFORM" = "mac" ]; then
    if [ -n "$url" ]; then
      open -n -a "$CLAUDE_APP" --args --user-data-dir="$dir" "$url"
    else
      open -n -a "$CLAUDE_APP" --args --user-data-dir="$dir"
    fi
  else
    local bin
    bin="$(find_claude_bin)" || { log "no Claude binary found (set CLAUDE_BIN)"; return 1; }
    # --class gives the window a distinct WM_CLASS so a matching .desktop
    # launcher (StartupWMClass) groups it under its own coloured icon.
    if [ -n "$url" ]; then
      "$bin" --class="$slug" --user-data-dir="$dir" "$url" >/dev/null 2>&1 &
    else
      "$bin" --class="$slug" --user-data-dir="$dir" >/dev/null 2>&1 &
    fi
  fi
}

# Show the account chooser; echo the chosen name (empty if cancelled).
choose_account() {
  if [ "$PLATFORM" = "mac" ]; then
    local buttons="" a
    for a in "${ACCOUNTS[@]}"; do buttons="$buttons\"$a\","; done
    buttons="${buttons%,}"
    local last="${ACCOUNTS[$((${#ACCOUNTS[@]} - 1))]}"
    osascript -e "button returned of (display dialog \"Which account are you signing into?\" buttons {$buttons} default button \"$last\" with title \"Claude login\")" 2>/dev/null || true
  elif command -v zenity >/dev/null 2>&1; then
    zenity --list --title="Claude login" --text="Which account are you signing into?" \
           --column="Account" "${ACCOUNTS[@]}" 2>/dev/null || true
  elif command -v kdialog >/dev/null 2>&1; then
    local args=() a
    for a in "${ACCOUNTS[@]}"; do args+=("$a" "$a"); done
    kdialog --title "Claude login" --menu "Which account are you signing into?" "${args[@]}" 2>/dev/null || true
  else
    printf 'Which account? [%s]: ' "$(IFS=/; echo "${ACCOUNTS[*]}")" > /dev/tty
    local ans=""; read -r ans < /dev/tty || true
    printf '%s' "$ans"
  fi
}

notify() {
  local msg="$1"
  if [ "$PLATFORM" = "mac" ]; then
    osascript -e "display dialog \"$msg\" buttons {\"OK\"} with title \"Claude Router\"" >/dev/null 2>&1 || true
  elif command -v zenity >/dev/null 2>&1; then
    zenity --info --title="Claude Router" --text="$msg" >/dev/null 2>&1 || true
  else
    printf '%b\n' "$msg"
  fi
}

# =====================================================================
#  Handler registration
# =====================================================================
register_linux() {
  ensure_stable_copy
  mkdir -p "$DESKTOP_DIR"
  cat > "$DESKTOP_DIR/claude-router.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Claude Login Router
Exec="$STABLE" handle %u
NoDisplay=true
MimeType=x-scheme-handler/claude;
EOF
  update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
  xdg-mime default claude-router.desktop x-scheme-handler/claude >/dev/null 2>&1 || true
  log "registered as claude:// handler (linux)"
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
  local plist="$app/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $HANDLER_ID" "$plist" 2>/dev/null || \
  /usr/libexec/PlistBuddy -c "Add :CFBundleIdentifier string $HANDLER_ID" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes array" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0 dict" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLName string Claude" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes array" "$plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string claude" "$plist" 2>/dev/null || true
  local lsreg="/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"
  if [ -x "$lsreg" ]; then "$lsreg" -f "$app" 2>/dev/null || true; fi
  if command -v duti >/dev/null 2>&1; then
    duti -s "$HANDLER_ID" claude all 2>/dev/null || true
    log "registered as claude:// handler (mac, duti)"
  else
    log "registered applet; duti not installed"
    echo "Note: install duti (brew install duti) to make the handler stick:"
    echo "  duti -s $HANDLER_ID claude all"
  fi
}

cmd_register() { if [ "$PLATFORM" = "mac" ]; then register_mac; else register_linux; fi; }

current_handler() {
  if [ "$PLATFORM" = "mac" ]; then
    if command -v duti >/dev/null 2>&1; then duti -x claude 2>/dev/null | tail -1 || true; fi
  else
    xdg-mime query default x-scheme-handler/claude 2>/dev/null || true
  fi
}

handler_is_ours() {
  if [ "$PLATFORM" = "mac" ]; then
    local h; h="$(current_handler)"
    case "$h" in *ClaudeRouter.app*|*"$HANDLER_ID"*) return 0 ;; *) return 1 ;; esac
  else
    [ "$(current_handler)" = "claude-router.desktop" ]
  fi
}

ensure_registered() {
  if ! handler_is_ours; then cmd_register >/dev/null 2>&1 || true; log "re-claimed claude:// handler"; fi
}

cmd_unregister() {
  if [ "$PLATFORM" = "mac" ]; then
    if command -v duti >/dev/null 2>&1; then
      local claude_id; claude_id="$(osascript -e 'id of app "Claude"' 2>/dev/null || true)"
      if [ -n "$claude_id" ]; then duti -s "$claude_id" claude all 2>/dev/null || true; echo "Handed claude:// back to Claude ($claude_id)."; fi
    fi
    rm -rf "$ROUTER_HOME/ClaudeRouter.app"
    echo "Removed the router applet. If claude:// is still ours, open Claude once to let it re-claim."
  else
    rm -f "$DESKTOP_DIR/claude-router.desktop"
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
    echo "Removed the router .desktop handler. Re-launch Claude to let it re-claim claude://."
  fi
}

# =====================================================================
#  Per-account launchers (with their own icon)
# =====================================================================
make_launchers_linux() {
  mkdir -p "$DESKTOP_DIR"
  local name ico slug
  for name in "${ACCOUNTS[@]}"; do
    ico="$(icon_for "$name")"
    slug="$(account_slug "$name")"
    {
      echo "[Desktop Entry]"
      echo "Type=Application"
      echo "Name=Claude ($name)"
      echo "Comment=Claude Desktop — $name account"
      echo "Exec=\"$STABLE\" launch $name"
      echo "Terminal=false"
      echo "StartupWMClass=$slug"
      [ -f "$ico" ] && echo "Icon=$ico"
      echo "Categories=Network;InstantMessaging;"
    } > "$DESKTOP_DIR/claude-$name.desktop"
    echo "  created launcher: Claude ($name)"
  done
  update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
}

# macOS: a tiny wrapper .app per account, carrying its own icon, that just
# calls this script's `launch`. Used as the clickable launcher so each
# account can present its own dock icon (best-effort — see README).
make_wrapper_app_mac() {
  local name="$1"
  local app="$ROUTER_HOME/Claude ($name).app"
  local ico; ico="$(icon_for "$name")"
  rm -rf "$app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cat > "$app/Contents/MacOS/launcher" <<EOF
#!/bin/bash
exec "$STABLE" launch "$name"
EOF
  chmod +x "$app/Contents/MacOS/launcher"
  [ -f "$ico" ] && cp "$ico" "$app/Contents/Resources/icon.icns"
  cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Claude ($name)</string>
  <key>CFBundleDisplayName</key><string>Claude ($name)</string>
  <key>CFBundleIdentifier</key><string>${APP_ID}.$(echo "$name" | tr '[:upper:]' '[:lower:]')</string>
  <key>CFBundleExecutable</key><string>launcher</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>icon.icns</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>LSUIElement</key><false/>
</dict>
</plist>
EOF
  local lsreg="/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"
  if [ -x "$lsreg" ]; then "$lsreg" -f "$app" 2>/dev/null || true; fi
  # Convenient double-clickable link on the Desktop.
  ln -sfn "$app" "$DESKTOP_DIR/Claude ($name).app" 2>/dev/null || true
  echo "  created launcher: $app"
}

make_launchers_mac() {
  local name
  for name in "${ACCOUNTS[@]}"; do make_wrapper_app_mac "$name"; done
}

cmd_tag() {
  # Re-assert launcher identity so a new/updated Claude keeps its per-account
  # icon grouping. (The heavy lifting happens at launch via WM_CLASS / bundle.)
  if [ "$PLATFORM" = "mac" ]; then
    local lsreg="/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"
    local name
    for name in "${ACCOUNTS[@]}"; do
      if [ -x "$lsreg" ]; then "$lsreg" -f "$ROUTER_HOME/Claude ($name).app" 2>/dev/null || true; fi
    done
  else
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
  fi
  log "re-asserted per-account launcher identity"
}

# =====================================================================
#  Self-heal watcher
# =====================================================================
watcher_running() {
  [ -f "$PIDFILE" ] || return 1
  local pid; pid="$(cat "$PIDFILE" 2>/dev/null || true)"
  [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null
}

install_watcher_linux() {
  mkdir -p "$AUTOSTART_DIR"
  cat > "$AUTOSTART_DIR/claude-router-watch.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Claude Router Watcher
Comment=Keeps ClaudeSwitch owning the claude:// login link
Exec="$STABLE" watch
Terminal=false
X-GNOME-Autostart-enabled=true
NoDisplay=true
EOF
}

install_watcher_mac() {
  mkdir -p "$LAUNCH_AGENTS"
  local plist="$LAUNCH_AGENTS/$WATCHER_ID.plist"
  cat > "$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$WATCHER_ID</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/bash</string>
    <string>$STABLE</string>
    <string>watch</string>
  </array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardErrorPath</key><string>$ROUTER_HOME/watcher.err.log</string>
  <key>StandardOutPath</key><string>$ROUTER_HOME/watcher.out.log</string>
</dict>
</plist>
EOF
  launchctl unload "$plist" >/dev/null 2>&1 || true
  launchctl load "$plist" >/dev/null 2>&1 || true
}

start_watcher_now() {
  watcher_running && return 0
  nohup "$STABLE" watch >/dev/null 2>&1 &
  disown 2>/dev/null || true
}

stop_watcher() {
  if watcher_running; then
    local pid; pid="$(cat "$PIDFILE" 2>/dev/null || true)"
    if [ -n "$pid" ]; then kill "$pid" 2>/dev/null || true; fi
  fi
  rm -f "$PIDFILE"
  if [ "$PLATFORM" = "mac" ]; then
    local plist="$LAUNCH_AGENTS/$WATCHER_ID.plist"
    launchctl unload "$plist" >/dev/null 2>&1 || true
    rm -f "$plist"
  else
    rm -f "$AUTOSTART_DIR/claude-router-watch.desktop"
  fi
}

cmd_watch() {
  mkdir -p "$ROUTER_HOME"
  # Only one watcher at a time.
  if watcher_running; then
    local pid; pid="$(cat "$PIDFILE" 2>/dev/null || true)"
    if [ "$pid" != "$$" ]; then log "watcher already running (pid $pid); exiting"; exit 0; fi
  fi
  echo "$$" > "$PIDFILE"
  trap 'rm -f "$PIDFILE"' EXIT
  log "watcher started (pid $$)"
  while true; do
    ensure_registered || true
    sleep 5
  done
}

# =====================================================================
#  Auto-install of Claude when it is missing
# =====================================================================
try_install_claude_mac() {
  local url="https://claude.ai/api/desktop/darwin/universal/dmg/latest/redirect"
  local dmg; dmg="$(mktemp -d)/Claude.dmg"
  echo "Downloading Claude Desktop..."
  if ! curl -fL --retry 2 -o "$dmg" "$url"; then log "dmg download failed"; return 1; fi
  [ -s "$dmg" ] || { log "dmg empty"; return 1; }
  local mnt; mnt="$(mktemp -d)"
  if ! hdiutil attach -nobrowse -quiet -mountpoint "$mnt" "$dmg"; then log "hdiutil attach failed"; return 1; fi
  local srcapp; srcapp="$(/bin/ls -d "$mnt"/*.app 2>/dev/null | head -1 || true)"
  local ok=1
  if [ -n "$srcapp" ]; then
    if cp -R "$srcapp" /Applications/ 2>/dev/null || sudo cp -R "$srcapp" /Applications/ 2>/dev/null; then ok=0; fi
  fi
  hdiutil detach "$mnt" -quiet >/dev/null 2>&1 || true
  rm -f "$dmg"
  return $ok
}

ensure_claude() {
  claude_present && return 0
  if [ "$PLATFORM" = "mac" ]; then
    notify "Claude isn't installed, so I'll download and install it now. This takes a minute."
    if try_install_claude_mac && claude_present; then return 0; fi
    notify "Couldn't install Claude automatically. Please install it from https://claude.ai/download , then run setup again."
    return 1
  else
    echo
    echo "Claude Desktop was not found on this machine."
    echo "There is no official one-click Linux installer, so point this tool at your Claude"
    echo "binary or AppImage and re-run setup, e.g.:"
    echo "  CLAUDE_BIN=\"\$HOME/Applications/Claude.AppImage\" \"$STABLE\" setup"
    echo "(If 'claude' is already on your PATH, it will be found automatically.)"
    return 1
  fi
}

# =====================================================================
#  Commands
# =====================================================================
cmd_setup() {
  ensure_stable_copy
  install_icons
  if ! ensure_claude; then return 1; fi
  cmd_register
  if [ "$PLATFORM" = "mac" ]; then make_launchers_mac; install_watcher_mac; else make_launchers_linux; install_watcher_linux; fi
  start_watcher_now
  cmd_tag
  echo
  echo "All set."
  echo "  • Open your accounts from the 'Claude (Personal)' and 'Claude (Work)' launchers."
  echo "  • Each account keeps its own data folder and its own taskbar/dock icon."
  echo "  • At sign-in, pick the account in the small chooser."
}

cmd_launch() {
  local name="$1"
  is_account "$name" || { echo "Unknown account: $name (known: ${ACCOUNTS[*]})"; exit 1; }
  launch_account "$name"
  sleep 3
  ensure_registered   # Claude grabs claude:// at startup; take it back
  log "launched '$name' and re-asserted broker"
}

cmd_handle() {
  local url="$1"
  log "callback: $url"
  case "$url" in
    *router-test*) notify "SUCCESS — broker intercepted:\n$url"; return 0 ;;
  esac
  local choice; choice="$(choose_account)"
  if [ -z "$choice" ]; then log "no target chosen; dropped"; return 0; fi
  is_account "$choice" || { log "bad choice: $choice"; return 0; }
  log "forwarding to '$choice'"
  launch_account "$choice" "$url"
  log "forward launched"
}

cmd_uninstall() {
  cmd_unregister || true
  stop_watcher
  if [ "$PLATFORM" = "mac" ]; then
    local name
    for name in "${ACCOUNTS[@]}"; do
      rm -rf "$ROUTER_HOME/Claude ($name).app"
      rm -f  "$DESKTOP_DIR/Claude ($name).app"
    done
  else
    local name
    for name in "${ACCOUNTS[@]}"; do rm -f "$DESKTOP_DIR/claude-$name.desktop"; done
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
  fi
  echo "Removed the ClaudeSwitch handler, launchers and watcher."
  echo "Your account data (Personal/Work) and $ROUTER_HOME/router.log were left untouched."
  echo "To go back to a single Claude, open Claude once so it re-claims claude://."
}

cmd_status() {
  echo "===== ClaudeSwitch (claude-router) status — $PLATFORM ====="
  echo "Router home : $ROUTER_HOME"
  echo "Installed   : $([ -f "$STABLE" ] && echo yes || echo no)"
  echo "Personal dir: $PERSONAL_DIR"
  echo "Work dir    : $WORK_DIR"
  if [ "$PLATFORM" = "mac" ]; then
    echo "Claude app  : $CLAUDE_APP $([ -d "$CLAUDE_APP" ] && echo '(found)' || echo '(missing)')"
    command -v duti >/dev/null 2>&1 || echo "duti        : not installed (brew install duti)"
  else
    local bin; bin="$(find_claude_bin 2>/dev/null || true)"
    echo "Claude bin  : ${bin:-'(not found — set CLAUDE_BIN)'}"
  fi
  local handler; handler="$(current_handler)"
  echo "claude:// handler : ${handler:-(none)}"
  echo "Handler is ours   : $(handler_is_ours && echo yes || echo no)"
  echo "Watcher running   : $(watcher_running && echo yes || echo no)"
  local name present=()
  for name in "${ACCOUNTS[@]}"; do
    if [ "$PLATFORM" = "mac" ]; then
      [ -d "$ROUTER_HOME/Claude ($name).app" ] && present+=("$name")
    else
      [ -f "$DESKTOP_DIR/claude-$name.desktop" ] && present+=("$name")
    fi
  done
  echo "Launchers present : ${present[*]:-none}"
}

cmd_test() {
  if [ "$PLATFORM" = "mac" ]; then open "claude://router-test-12345" 2>/dev/null || true
  else xdg-open "claude://router-test-12345" >/dev/null 2>&1 || true; fi
  echo "Fired claude://router-test-12345 — a success box should appear if the broker is registered."
}

# =====================================================================
main() {
  local cmd="${1:-status}"; shift || true
  case "$cmd" in
    status)     cmd_status ;;
    test)       cmd_test ;;
    register)   cmd_register ;;
    unregister) cmd_unregister ;;
    setup|install) cmd_setup ;;
    uninstall)  cmd_uninstall ;;
    launch)     cmd_launch "${1:?account name required}" ;;
    watch)      cmd_watch ;;
    tag)        cmd_tag ;;
    handle)     cmd_handle "${1:?url required}" ;;
    *) echo "Unknown command: $cmd"
       echo "Commands: status test register unregister setup install uninstall launch watch tag handle"
       exit 1 ;;
  esac
}
main "$@"
