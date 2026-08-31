@echo off
REM  One-click uninstall: restores the default claude:// handler, stops the
REM  watcher, and removes the shortcuts. Your logins/backups are left alone.
if exist "%LOCALAPPDATA%\ClaudeRouter\ClaudeRouter.exe" (
  "%LOCALAPPDATA%\ClaudeRouter\ClaudeRouter.exe" uninstall
) else (
  echo ClaudeRouter is not installed.
  pause
)
