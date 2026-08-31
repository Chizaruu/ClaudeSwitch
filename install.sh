#!/usr/bin/env bash
# =====================================================================
#  install.sh  —  one-command ClaudeSwitch setup for Linux and macOS
#
#  Builds the ClaudeRouter engine (a single native binary) from source with
#  NativeAOT, then runs its `setup`, which registers the claude:// handler,
#  creates the per-account launchers, and installs the self-heal watcher (and,
#  on macOS, downloads Claude first if it is missing).
#
#  Requires the .NET 10 SDK (https://dotnet.microsoft.com/download) plus the
#  native toolchain NativeAOT needs: clang + zlib headers on Linux, the Xcode
#  Command Line Tools on macOS.
#
#  USAGE:
#    ./install.sh
#
#  Linux note: if Claude is not on your PATH, point the engine at your binary
#  or AppImage (the variable is passed straight through to setup):
#    CLAUDE_BIN="$HOME/Applications/Claude.AppImage" ./install.sh
# =====================================================================

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"

if [ ! -f "$HERE/src/ClaudeRouter.csproj" ]; then
  echo "ERROR: run install.sh from inside the unzipped ClaudeSwitch folder." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: the .NET 10 SDK is required to build ClaudeRouter." >&2
  echo "Install it from https://dotnet.microsoft.com/download then run ./install.sh again." >&2
  exit 1
fi

case "$(uname -s)" in
  Linux)  OS=linux ;;
  Darwin) OS=osx ;;
  *) echo "This installer is for Linux and macOS. On Windows, double-click build.bat." >&2; exit 1 ;;
esac

case "$(uname -m)" in
  x86_64|amd64)  ARCH=x64 ;;
  arm64|aarch64) ARCH=arm64 ;;
  *) echo "Unsupported CPU architecture: $(uname -m)" >&2; exit 1 ;;
esac

RID="$OS-$ARCH"
echo "Building ClaudeRouter for $RID ..."
dotnet publish "$HERE/src" -c Release -r "$RID"

PUB="$HERE/src/bin/Release/net10.0/$RID/publish/ClaudeRouter"
if [ ! -x "$PUB" ]; then
  echo "ERROR: build did not produce $PUB" >&2
  exit 1
fi

echo "Setting up ClaudeSwitch..."
"$PUB" setup
echo
echo "Done. You can close this window."
