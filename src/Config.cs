using System.Diagnostics;
using System.Text;

namespace ClaudeSwitch;

/// <summary>
/// Cross-platform locations, the account list, and logging. Paths are computed
/// explicitly per OS so they match the shipping shell/exe implementations
/// exactly (rather than relying on SpecialFolder mapping, which differs).
/// </summary>
static class Config
{
    // Add accounts here; each gets its own data dir, launcher and icon.
    public static readonly string[] Accounts = { "Personal", "Work" };

    public const string AppId = "com.claudeswitch";
    public const string HandlerId = AppId + ".handler";
    public const string WatcherId = AppId + ".watcher";

    static string Home =>
        Environment.GetEnvironmentVariable(OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME")
        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string RouterHome
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeRouter");
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Home, "Library", "Application Support", "claude-router");
            return Path.Combine(Home, ".local", "share", "claude-router");
        }
    }

    public static string DataDirFor(string account)
    {
        // The PRIMARY account uses Claude's default profile ("Claude"); every other
        // account gets a suffixed profile. On Windows only the default-profile
        // account is Cowork-capable, so `Primary` is which account has Cowork.
        string suffix = account == Primary ? "" : "-" + account;
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude" + suffix);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Home, "Library", "Application Support", "Claude" + suffix);
        return Path.Combine(Home, ".config", "Claude" + suffix);
    }

    public static string IconExt =>
        OperatingSystem.IsWindows() ? "ico" : OperatingSystem.IsMacOS() ? "icns" : "png";

    public static string IconFor(string account) => Path.Combine(RouterHome, $"{account}.{IconExt}");

    public static string LogFile => Path.Combine(RouterHome, "router.log");
    public static string PidFile => Path.Combine(RouterHome, "watcher.pid");

    /// <summary>The stable installed binary the handler/launchers/watcher point at.</summary>
    public static string InstalledEnginePath =>
        Path.Combine(RouterHome, OperatingSystem.IsWindows() ? "ClaudeRouter.exe" : "ClaudeRouter");

    /// <summary>Installed engine if present, otherwise the currently-running binary.</summary>
    public static string EnginePath =>
        File.Exists(InstalledEnginePath) ? InstalledEnginePath : (Environment.ProcessPath ?? InstalledEnginePath);

    public static bool IsAccount(string name) => Array.IndexOf(Accounts, name) >= 0;

    // ---- primary (default-profile / Cowork-capable) account ----
    public static string PrimaryFile => Path.Combine(RouterHome, "primary.txt");
    static string? _primary;

    /// <summary>Which account uses Claude's default profile; defaults to the first.</summary>
    public static string Primary
    {
        get
        {
            if (_primary != null) return _primary;
            try
            {
                if (File.Exists(PrimaryFile))
                {
                    string s = File.ReadAllText(PrimaryFile).Trim();
                    if (IsAccount(s)) return _primary = s;
                }
            }
            catch { }
            return _primary = Accounts[0];
        }
    }

    public static void SetPrimary(string account)
    {
        if (!IsAccount(account)) return;
        try { Directory.CreateDirectory(RouterHome); File.WriteAllText(PrimaryFile, account); } catch { }
        _primary = account;
    }

    public static string Slug(string account) => "ClaudeRouter-" + account;

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(RouterHome);
            File.AppendAllText(LogFile, $"{DateTime.Now:o}  {message}{Environment.NewLine}");
        }
        catch { /* logging is best-effort */ }
    }
}

/// <summary>Tiny process helper shared by every platform.</summary>
static class Sh
{
    /// <summary>Run a command, wait, return (exit code, stdout+stderr trimmed).</summary>
    public static (int code, string output) Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            string outp = p.StandardOutput.ReadToEnd();
            string errp = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, (outp + errp).Trim());
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    /// <summary>
    /// Start a background process that must NOT hold the caller's console — the
    /// watcher and each Claude launch outlive the command that spawned them, so a
    /// terminal (or `handle` invoked by the OS) returns immediately. On Unix we
    /// fully detach via `nohup … &`; on Windows we launch with no window and no
    /// inherited std handles.
    /// </summary>
    public static void Start(string file, IEnumerable<string> args, bool shellExecute = false)
    {
        try
        {
            if (shellExecute)
            {
                // Interactive launches (installers/UAC, xdg 'start') keep ShellExecute.
                var psi = new ProcessStartInfo(file) { UseShellExecute = true };
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo(file)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // Redirect (and never read) so the child does not inherit the console.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
            }
            else
            {
                // nohup <cmd …> >/dev/null 2>&1 &   — fully detached, no held stdio.
                var cmd = new StringBuilder("nohup ").Append(ShQuote(file));
                foreach (var a in args) cmd.Append(' ').Append(ShQuote(a));
                cmd.Append(" >/dev/null 2>&1 &");
                var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(cmd.ToString());
                using var p = Process.Start(psi);
                p?.WaitForExit(); // sh backgrounds the job and returns at once
            }
        }
        catch (Exception ex) { Config.Log($"start '{file}' failed: {ex.Message}"); }
    }

    static string ShQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    public static bool Which(string name) =>
        Run(OperatingSystem.IsWindows() ? "where" : "which", name).code == 0;
}
