# Security Policy

## Supported versions

This is a small community utility. Security fixes are applied to the latest code
on the `main` branch. There are no long-term supported branches.

## What this tool does to your system

So you can review the risk surface before running it, `ClaudeRouter` (and the
Bash port) will, on the current user account only and **without admin rights**:

- **Modify the per-user registry** (`HKEY_CURRENT_USER\Software\Classes\claude`)
  to make itself the `claude://` protocol handler. On Linux this is an
  `xdg-mime` `.desktop` handler; on macOS a Launch Services applet + `duti`.
- **Create desktop and startup shortcuts** for the two accounts and the watcher.
- **Run a background process at logon** (`ClaudeRouter.exe watch`) that
  re-registers the handler and re-colors windows.
- **Make a portable copy of Claude** under `%LOCALAPPDATA%\ClaudePortable`.
- **Download the official Claude installer** from `claude.ai` — only if Claude is
  not already installed — over HTTPS (TLS 1.2).
- **Uninstall the existing standalone Claude app** so it stops claiming the login
  link.

It does not require elevation, does not touch other users' data, and keeps login
backups under `%LOCALAPPDATA%\ClaudeRouter\session-backup-*`. Everything is undone
by `uninstall.bat`.

Because the tool registers a URL-scheme handler and starts a login-time process,
**build it from source** (via `build.bat`) rather than running an unsigned
prebuilt binary from an untrusted place.

## Reporting a vulnerability

If you find a security issue, please **do not open a public issue**. Instead:

- Use GitHub's **[Report a vulnerability](https://github.com/Chizaruu/claude-switch/security/advisories/new)**
  (Security → Advisories) to open a private report, **or**
- Contact the maintainer privately via their GitHub profile:
  [@Chizaruu](https://github.com/Chizaruu).

Please include steps to reproduce, the affected platform and Claude version, and
any relevant `router.log` excerpts. You can expect an initial response within a
reasonable time; fixes will be coordinated before public disclosure.
