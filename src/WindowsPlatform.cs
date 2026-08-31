using System.Runtime.InteropServices;

namespace ClaudeSwitch;

/// <summary>
/// Windows implementation. Handler via HKCU (shelled through reg.exe), chooser via
/// a Win32 message box, watcher via the HKCU Run key, and — the full experience —
/// per-account taskbar colouring: AUMID-tagged .lnk launchers plus live window
/// tagging (AppUserModel.ID + per-account icon + regroup). The COM/native interop
/// for that lives in <see cref="WindowsInterop"/>, implemented with NativeAOT's
/// source-generated COM ([GeneratedComInterface]).
///
/// Windows-only; validated on Windows (the interop cannot run on Linux/macOS).
/// </summary>
sealed class WindowsPlatform : IPlatform
{
    public string Name => "windows";

    static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Startup => Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    const string HandlerKey = @"HKCU\Software\Classes\claude";
    static string HandlerCommand => $"\"{Config.EnginePath}\" handle \"%1\"";

    // ---- claude:// handler (via reg.exe) ----
    public void Register()
    {
        Sh.Run("reg", "add", HandlerKey, "/ve", "/d", "URL:Claude Protocol", "/f");
        Sh.Run("reg", "add", HandlerKey, "/v", "URL Protocol", "/d", "", "/f");
        Sh.Run("reg", "add", HandlerKey + @"\shell\open\command", "/ve", "/d", HandlerCommand, "/f");
        Config.Log("registered as claude:// handler (windows)");
    }

    public void Unregister() => Sh.Run("reg", "delete", HandlerKey, "/f");

    public string? CurrentHandler()
    {
        var (code, output) = Sh.Run("reg", "query", HandlerKey + @"\shell\open\command", "/ve");
        if (code != 0) return null;
        int i = output.IndexOf("REG_SZ", StringComparison.Ordinal);
        return i >= 0 ? output[(i + 6)..].Trim() : null;
    }

    public bool HandlerIsOurs() =>
        string.Equals(CurrentHandler(), HandlerCommand, StringComparison.OrdinalIgnoreCase);

    // ---- launchers (AUMID-tagged .lnk via IShellLink) ----
    public void MakeLaunchers()
    {
        string? exeDir = Path.GetDirectoryName(FindClaudeExe() ?? Config.EnginePath);
        foreach (var name in Config.Accounts)
        {
            string lnk = Path.Combine(Desktop, $"Claude ({name}).lnk");
            string ico = Config.IconFor(name);
            // Same AppUserModelID as the running window, so a pinned shortcut and
            // its window collapse into ONE coloured taskbar button per account.
            WindowsInterop.CreateShortcut(lnk, Config.EnginePath, $"launch {name}",
                File.Exists(ico) ? ico : Config.EnginePath, $"Claude {name}", exeDir, "ClaudeRouter." + name);
            Console.WriteLine($"  created launcher: Claude ({name})");
        }
    }

    public void RemoveLaunchers()
    {
        foreach (var name in Config.Accounts)
            try { File.Delete(Path.Combine(Desktop, $"Claude ({name}).lnk")); } catch { }
    }

    public IEnumerable<string> LaunchersPresent() =>
        Config.Accounts.Where(n => File.Exists(Path.Combine(Desktop, $"Claude ({n}).lnk")));

    // ---- watcher (HKCU Run key) ----
    public void InstallWatcher() =>
        Sh.Run("reg", "add", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
               "/v", "ClaudeRouterWatcher", "/d", $"\"{Config.EnginePath}\" watch", "/f");

    public void RemoveWatcher() =>
        Sh.Run("reg", "delete", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
               "/v", "ClaudeRouterWatcher", "/f");

    // ---- launch + UI ----
    public void LaunchAccount(string account, string? url)
    {
        string dir = Config.DataDirFor(account);
        Directory.CreateDirectory(dir);
        string? exe = FindClaudeLauncher(out bool isMsix);
        if (exe == null) { Config.Log("no claude.exe found"); return; }
        var args = new List<string> { $"--user-data-dir={dir}" };
        if (!string.IsNullOrEmpty(url)) args.Add(url);
        // Launching the MSIX alias activates Claude with package identity (needed
        // for Cowork); a legacy exe does not. Best-effort — whether a second
        // MSIX instance with its own data dir keeps identity is unverified.
        Config.Log($"launching '{account}' via {(isMsix ? "MSIX alias" : "legacy exe")}: {exe}");
        Sh.Start(exe, args);
    }

    public string? ChooseAccount()
    {
        // Two-account chooser via a native message box (Yes = first, No = second).
        // For more than two accounts a custom Win32 dialog would be needed; the
        // console prompt below is the fallback.
        if (Config.Accounts.Length == 2)
        {
            const uint MB_YESNO = 0x4, MB_ICONQUESTION = 0x20, MB_TOPMOST = 0x40000;
            int r = MessageBoxW(IntPtr.Zero,
                $"Yes = {Config.Accounts[0]}   •   No = {Config.Accounts[1]}",
                "Which account are you signing into?", MB_YESNO | MB_ICONQUESTION | MB_TOPMOST);
            return r == 6 /*IDYES*/ ? Config.Accounts[0] : r == 7 /*IDNO*/ ? Config.Accounts[1] : null;
        }
        Console.Write($"Which account? [{string.Join("/", Config.Accounts)}]: ");
        return Console.ReadLine()?.Trim();
    }

    public void Notify(string message) =>
        MessageBoxW(IntPtr.Zero, message.Replace("\\n", "\n"), "Claude Router", 0x40 /*MB_ICONINFORMATION*/);

    // ---- Claude presence / auto-install ----
    public bool ClaudePresent() => FindClaudeExe() != null;

    public bool EnsureClaude()
    {
        if (ClaudePresent()) return true;
        // Deliberately no auto-install: the old flow fetched the Squirrel setup,
        // which is exactly the "legacy" install Cowork now rejects. Point the user
        // at the modern (MSIX) installer instead.
        Notify("Claude Desktop isn't installed.\\n\\nInstall it with the modern installer from " +
               "https://claude.ai/download (the MSIX build — required for Cowork), then run setup again.");
        return false;
    }

    public void Tag()
    {
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("claude"))
        {
            IntPtr h;
            try { h = p.MainWindowHandle; } catch { continue; }
            if (h == IntPtr.Zero) continue;
            string? acct = AccountFor(p.Id);
            if (acct == null) continue;
            string want = "ClaudeRouter." + acct;
            if (WindowsInterop.GetWindowAumid(h) == want) continue; // already tagged
            string ico = Config.IconFor(acct);
            string iconRes = File.Exists(ico) ? ico + ",0" : "";
            string relaunch = $"\"{Config.EnginePath}\" launch {acct}";
            WindowsInterop.TagWindow(h, want, relaunch, iconRes, $"Claude ({acct})");
            if (iconRes.Length > 0) WindowsInterop.SetWindowIcon(h, ico);
            WindowsInterop.Regroup(h);
            Config.Log($"tagged window as '{acct}'");
        }
    }

    // Which account a running claude.exe belongs to, by its --user-data-dir.
    static string? AccountFor(int pid)
    {
        string? dir = ExtractDir(WindowsInterop.GetCommandLine(pid));
        if (dir == null) return null;
        dir = dir.TrimEnd('\\');
        foreach (var name in Config.Accounts)
            if (string.Equals(dir, Config.DataDirFor(name).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return name;
        return null;
    }

    static string? ExtractDir(string? cl)
    {
        if (string.IsNullOrEmpty(cl)) return null;
        const string key = "--user-data-dir=";
        int i = cl.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += key.Length;
        if (i < cl.Length && cl[i] == '"')
        {
            int j = cl.IndexOf('"', i + 1);
            return j < 0 ? null : cl.Substring(i + 1, j - i - 1);
        }
        int k = cl.IndexOf(' ', i);
        if (k < 0) k = cl.Length;
        return cl.Substring(i, k - i);
    }

    public string ClaudeLocationLine()
    {
        string? exe = FindClaudeLauncher(out bool msix);
        string kind = exe == null ? "" : msix ? "  (MSIX / modern — Cowork-capable)" : "  (legacy install — Cowork unavailable)";
        return $"Claude exe  : {exe ?? "(not found)"}{kind}";
    }

    static string? FindClaudeExe() => FindClaudeLauncher(out _);

    // Find the best way to launch Claude, and report whether it's the MSIX
    // ("modern") install. Order matters: the MSIX execution alias is preferred
    // because launching it activates Claude WITH package identity — which
    // Cowork's "modern installer" check requires. A Squirrel/portable exe
    // launches without identity, so Cowork stays disabled in that window.
    static string? FindClaudeLauncher(out bool isMsix)
    {
        isMsix = false;

        // 1. MSIX app-execution alias (a reparse point under WindowsApps). Running
        //    it starts Claude inside its package container, with identity.
        string alias = Path.Combine(Local, "Microsoft", "WindowsApps", "claude.exe");
        if (File.Exists(alias)) { isMsix = true; return alias; }

        // 2. Portable copy left by an older ClaudeSwitch (legacy, no identity).
        string portable = Path.Combine(Local, "ClaudePortable", "app", "claude.exe");
        if (File.Exists(portable)) return portable;

        // 3. Squirrel install under %LOCALAPPDATA%\AnthropicClaude (legacy).
        string anthropic = Path.Combine(Local, "AnthropicClaude");
        if (Directory.Exists(anthropic))
        {
            try
            {
                string? f = Directory.EnumerateFiles(anthropic, "claude.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (f != null) return f;
            }
            catch { }
        }

        // 4. Anything else on PATH (a WindowsApps hit here is also MSIX).
        var (c, o) = Sh.Run("where", "claude");
        if (c == 0 && o.Length > 0)
        {
            string p = o.Split('\n')[0].Trim();
            if (p.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0) isMsix = true;
            return p;
        }
        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
