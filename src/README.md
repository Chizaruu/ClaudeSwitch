# ClaudeRouter — the ClaudeSwitch engine (.NET, NativeAOT)

One C# codebase for **Windows, macOS and Linux**, published per-RID with
**NativeAOT** into a small, dependency-free *native* binary — no .NET runtime for
end users to install. This is the whole engine; there is no separate Windows/Unix
implementation.

## Why NativeAOT (and not MAUI)

- **Linux is a first-class target.** MAUI has no official Linux desktop support
  (only a third-party Avalonia preview tied to .NET 11); a plain console app +
  NativeAOT ships Linux today.
- **There is almost no shared GUI** — the app is OS-integration plumbing plus a
  two-button chooser — so a GUI framework buys nothing while costing a runtime.
- **Keeps "no runtime to install"** — NativeAOT emits a native binary that links
  only libc/libm (Linux) / system libraries, not a .NET runtime.

## Architecture

```
Program.cs         entry point; picks the platform once
Router.cs          OS-agnostic orchestration (setup/launch/handle/watch/…)
IPlatform.cs       everything that differs by OS, behind one interface
Config.cs          accounts, per-OS paths, logging, a small process helper
LinuxPlatform.cs   xdg-mime + .desktop, zenity/kdialog, --class (WM_CLASS), autostart
MacPlatform.cs     AppleScript applet + duti, osascript, wrapper .app, launchd, dmg install
WindowsPlatform.cs HKCU handler, Win32 chooser, AUMID launchers, Run-key watcher
WindowsInterop.cs  Windows-only COM/native interop (source-generated COM for AOT)
```

`Router` never branches on the OS; only the platform classes do. Adding an account
is editing `Config.Accounts` and dropping matching icons in `../assets`.

## The Windows COM interop

`WindowsInterop.cs` is the one genuinely tricky part: per-account taskbar colouring
needs `IShellLink` / `IPropertyStore` / `SHGetPropertyStoreForWindow` and a
cross-process command-line read. Under NativeAOT that COM interop uses the .NET 10
**source-generated** COM path (`[GeneratedComInterface]` + `StrategyBasedComWrappers`)
rather than classic runtime-marshalled COM, and the process command line is read
from the PEB via `ReadProcessMemory` (WMI is not AOT-friendly). It is Windows-only
and validated on Windows; on Linux/macOS these P/Invokes are inert metadata.

## Build

Requires the .NET 10 SDK plus each OS's native toolchain (clang+zlib on Linux, MSVC
on Windows, Xcode CLT on macOS). From the repo root, the convenience scripts do
everything (`build.bat` on Windows, `./install.sh` on Linux/macOS). By hand:

```bash
dotnet publish src -c Release -r linux-x64   # or win-x64 / win-arm64 / osx-arm64 / osx-x64 / linux-arm64
./src/bin/Release/net10.0/<rid>/publish/ClaudeRouter setup
```

Subcommands: `status register unregister setup install uninstall launch watch tag test handle`.
CI NativeAOT-compiles this on Ubuntu, Windows and macOS runners
(`../.github/workflows/build.yml`).
