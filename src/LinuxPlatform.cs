namespace ClaudeSwitch;

/// <summary>
/// Linux implementation — the reference implementation for this spike, and the
/// one exercised end-to-end. Uses xdg-mime + .desktop files, zenity/kdialog for
/// the chooser, a distinct --class (WM_CLASS) per account for taskbar grouping,
/// and a ~/.config/autostart entry for the watcher.
/// </summary>
sealed class LinuxPlatform : IPlatform
{
    public string Name => "linux";

    static string Home => Environment.GetEnvironmentVariable("HOME")!;
    static string AppsDir => Path.Combine(Home, ".local", "share", "applications");
    static string AutostartDir => Path.Combine(Home, ".config", "autostart");
    const string HandlerDesktop = "claude-router.desktop";

    // ---- claude:// handler ----
    public void Register()
    {
        Directory.CreateDirectory(AppsDir);
        File.WriteAllText(Path.Combine(AppsDir, HandlerDesktop),
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=Claude Login Router\n" +
            $"Exec=\"{Config.EnginePath}\" handle %u\n" +
            "NoDisplay=true\n" +
            "MimeType=x-scheme-handler/claude;\n");
        Sh.Run("update-desktop-database", AppsDir);
        Sh.Run("xdg-mime", "default", HandlerDesktop, "x-scheme-handler/claude");
        Config.Log("registered as claude:// handler (linux)");
    }

    public void Unregister()
    {
        try { File.Delete(Path.Combine(AppsDir, HandlerDesktop)); } catch { }
        Sh.Run("update-desktop-database", AppsDir);
    }

    public string? CurrentHandler()
    {
        var (code, output) = Sh.Run("xdg-mime", "query", "default", "x-scheme-handler/claude");
        return code == 0 && output.Length > 0 ? output : null;
    }

    public bool HandlerIsOurs() => CurrentHandler() == HandlerDesktop;

    // ---- launchers ----
    public void MakeLaunchers()
    {
        Directory.CreateDirectory(AppsDir);
        foreach (var name in Config.Accounts)
        {
            string ico = Config.IconFor(name);
            var lines = new List<string>
            {
                "[Desktop Entry]",
                "Type=Application",
                $"Name=Claude ({name})",
                $"Comment=Claude Desktop — {name} account",
                $"Exec=\"{Config.EnginePath}\" launch {name}",
                "Terminal=false",
                $"StartupWMClass={Config.Slug(name)}",
            };
            if (File.Exists(ico)) lines.Add($"Icon={ico}");
            lines.Add("Categories=Network;InstantMessaging;");
            File.WriteAllText(Path.Combine(AppsDir, $"claude-{name}.desktop"), string.Join("\n", lines) + "\n");
            Console.WriteLine($"  created launcher: Claude ({name})");
        }
        Sh.Run("update-desktop-database", AppsDir);
    }

    public void RemoveLaunchers()
    {
        foreach (var name in Config.Accounts)
            try { File.Delete(Path.Combine(AppsDir, $"claude-{name}.desktop")); } catch { }
        Sh.Run("update-desktop-database", AppsDir);
    }

    public IEnumerable<string> LaunchersPresent() =>
        Config.Accounts.Where(n => File.Exists(Path.Combine(AppsDir, $"claude-{n}.desktop")));

    // ---- watcher ----
    public void InstallWatcher()
    {
        Directory.CreateDirectory(AutostartDir);
        File.WriteAllText(Path.Combine(AutostartDir, "claude-router-watch.desktop"),
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=Claude Router Watcher\n" +
            "Comment=Keeps ClaudeSwitch owning the claude:// login link\n" +
            $"Exec=\"{Config.EnginePath}\" watch\n" +
            "Terminal=false\n" +
            "X-GNOME-Autostart-enabled=true\n" +
            "NoDisplay=true\n");
    }

    public void RemoveWatcher()
    {
        try { File.Delete(Path.Combine(AutostartDir, "claude-router-watch.desktop")); } catch { }
    }

    // ---- launch + UI ----
    public void LaunchAccount(string account, string? url)
    {
        string dir = Config.DataDirFor(account);
        Directory.CreateDirectory(dir);
        string? bin = FindClaudeBin();
        if (bin == null) { Config.Log("no Claude binary found (set CLAUDE_BIN)"); return; }
        var args = new List<string> { $"--class={Config.Slug(account)}", $"--user-data-dir={dir}" };
        if (!string.IsNullOrEmpty(url)) args.Add(url);
        Sh.Start(bin, args);
    }

    public string? ChooseAccount()
    {
        if (Sh.Which("zenity"))
        {
            var a = new List<string> { "--list", "--title=Claude login", "--text=Which account are you signing into?", "--column=Account" };
            a.AddRange(Config.Accounts);
            var (code, output) = Sh.Run("zenity", a.ToArray());
            return code == 0 ? output.Trim() : null;
        }
        if (Sh.Which("kdialog"))
        {
            var a = new List<string> { "--title", "Claude login", "--menu", "Which account are you signing into?" };
            foreach (var n in Config.Accounts) { a.Add(n); a.Add(n); }
            var (code, output) = Sh.Run("kdialog", a.ToArray());
            return code == 0 ? output.Trim() : null;
        }
        Console.Write($"Which account? [{string.Join("/", Config.Accounts)}]: ");
        return Console.ReadLine()?.Trim();
    }

    public void Notify(string message)
    {
        if (Sh.Which("zenity")) Sh.Run("zenity", "--info", "--title=Claude Router", $"--text={message}");
        else Console.WriteLine(message.Replace("\\n", "\n"));
    }

    // ---- Claude presence ----
    public bool ClaudePresent() => FindClaudeBin() != null;

    public bool EnsureClaude()
    {
        if (ClaudePresent()) return true;
        Console.WriteLine();
        Console.WriteLine("Claude Desktop was not found on this machine.");
        Console.WriteLine("There is no official one-click Linux installer, so point this tool at your");
        Console.WriteLine("Claude binary or AppImage and re-run setup, e.g.:");
        Console.WriteLine($"  CLAUDE_BIN=\"$HOME/Applications/Claude.AppImage\" \"{Config.EnginePath}\" setup");
        return false;
    }

    public void Tag() => Sh.Run("update-desktop-database", AppsDir);

    public string ClaudeLocationLine() => $"Claude bin  : {FindClaudeBin() ?? "(not found — set CLAUDE_BIN)"}";

    // Resolve a usable Claude binary/AppImage: $CLAUDE_BIN, PATH, then common spots.
    static string? FindClaudeBin()
    {
        string? env = Environment.GetEnvironmentVariable("CLAUDE_BIN");
        if (!string.IsNullOrEmpty(env))
        {
            if (File.Exists(env)) return env;
            var (c, o) = Sh.Run("which", env);
            if (c == 0 && o.Length > 0) return o;
        }
        foreach (var candidate in new[] { "claude", "claude-desktop", "Claude" })
        {
            var (c, o) = Sh.Run("which", candidate);
            if (c == 0 && o.Length > 0) return o.Split('\n')[0].Trim();
        }
        string[] dirs = { Path.Combine(Home, "Applications"), Path.Combine(Home, ".local", "bin"),
                          Path.Combine(Home, "bin"), Path.Combine(Home, "Downloads"), "/opt", "/usr/local/bin" };
        foreach (var d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(d))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.StartsWith("claude", StringComparison.OrdinalIgnoreCase) &&
                        (fn.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) || !fn.Contains('.')))
                        return f;
                }
            }
            catch { }
        }
        return null;
    }
}
