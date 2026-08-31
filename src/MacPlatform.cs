namespace ClaudeSwitch;

/// <summary>
/// macOS implementation. Uses an AppleScript applet + duti/Launch Services for
/// the handler, osascript for the chooser, per-account wrapper .app bundles as
/// launchers, and a launchd LaunchAgent for the watcher. Auto-installs Claude
/// from the official .dmg when it is absent.
///
/// Written against the correct native tools; runnable only on macOS (the tools
/// osacompile / duti / launchctl / hdiutil don't exist elsewhere).
/// </summary>
sealed class MacPlatform : IPlatform
{
    public string Name => "mac";

    static string Home => Environment.GetEnvironmentVariable("HOME")!;
    static string ClaudeApp => Environment.GetEnvironmentVariable("CLAUDE_APP") ?? "/Applications/Claude.app";
    static string DesktopDir => Path.Combine(Home, "Desktop");
    static string LaunchAgents => Path.Combine(Home, "Library", "LaunchAgents");
    static string RouterApp => Path.Combine(Config.RouterHome, "ClaudeRouter.app");
    const string Lsregister =
        "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";

    // ---- claude:// handler ----
    public void Register()
    {
        Directory.CreateDirectory(Config.RouterHome);
        try { Directory.Delete(RouterApp, true); } catch { }

        // AppleScript applet that forwards the incoming URL to this engine.
        string script =
            "on open location this_URL\n" +
            $"  do shell script quoted form of \"{Config.EnginePath}\" & \" handle \" & quoted form of this_URL\n" +
            "end open location\n";
        string tmp = Path.Combine(Path.GetTempPath(), "claude-router-applet.applescript");
        File.WriteAllText(tmp, script);
        Sh.Run("osacompile", "-o", RouterApp, tmp);

        string plist = Path.Combine(RouterApp, "Contents", "Info.plist");
        void PB(string cmd) => Sh.Run("/usr/libexec/PlistBuddy", "-c", cmd, plist);
        PB($"Set :CFBundleIdentifier {Config.HandlerId}");
        PB("Add :CFBundleURLTypes array");
        PB("Add :CFBundleURLTypes:0 dict");
        PB("Add :CFBundleURLTypes:0:CFBundleURLName string Claude");
        PB("Add :CFBundleURLTypes:0:CFBundleURLSchemes array");
        PB("Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string claude");

        if (File.Exists(Lsregister)) Sh.Run(Lsregister, "-f", RouterApp);
        if (Sh.Which("duti")) { Sh.Run("duti", "-s", Config.HandlerId, "claude", "all"); Config.Log("registered claude:// (mac, duti)"); }
        else Console.WriteLine("Note: install duti (brew install duti) so the handler sticks: duti -s " + Config.HandlerId + " claude all");
    }

    public void Unregister()
    {
        if (Sh.Which("duti"))
        {
            var (c, id) = Sh.Run("osascript", "-e", "id of app \"Claude\"");
            if (c == 0 && id.Length > 0) Sh.Run("duti", "-s", id.Trim(), "claude", "all");
        }
        try { Directory.Delete(RouterApp, true); } catch { }
    }

    public string? CurrentHandler()
    {
        if (!Sh.Which("duti")) return null;
        var (c, o) = Sh.Run("duti", "-x", "claude");
        return c == 0 && o.Length > 0 ? o.Split('\n').Last().Trim() : null;
    }

    public bool HandlerIsOurs()
    {
        string? h = CurrentHandler();
        return h != null && (h.Contains("ClaudeRouter.app") || h.Contains(Config.HandlerId));
    }

    // ---- launchers: per-account wrapper .app bundles ----
    public void MakeLaunchers()
    {
        foreach (var name in Config.Accounts) MakeWrapperApp(name);
    }

    void MakeWrapperApp(string name)
    {
        string app = Path.Combine(Config.RouterHome, $"Claude ({name}).app");
        try { Directory.Delete(app, true); } catch { }
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));

        string launcher = Path.Combine(app, "Contents", "MacOS", "launcher");
        File.WriteAllText(launcher, $"#!/bin/bash\nexec \"{Config.EnginePath}\" launch \"{name}\"\n");
        Sh.Run("chmod", "+x", launcher);

        string ico = Config.IconFor(name);
        if (File.Exists(ico)) File.Copy(ico, Path.Combine(app, "Contents", "Resources", "icon.icns"), true);

        File.WriteAllText(Path.Combine(app, "Contents", "Info.plist"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n<dict>\n" +
            $"  <key>CFBundleName</key><string>Claude ({name})</string>\n" +
            $"  <key>CFBundleDisplayName</key><string>Claude ({name})</string>\n" +
            $"  <key>CFBundleIdentifier</key><string>{Config.AppId}.{name.ToLowerInvariant()}</string>\n" +
            "  <key>CFBundleExecutable</key><string>launcher</string>\n" +
            "  <key>CFBundlePackageType</key><string>APPL</string>\n" +
            "  <key>CFBundleIconFile</key><string>icon.icns</string>\n" +
            "  <key>CFBundleShortVersionString</key><string>1.0</string>\n" +
            "  <key>LSUIElement</key><false/>\n" +
            "</dict>\n</plist>\n");

        if (File.Exists(Lsregister)) Sh.Run(Lsregister, "-f", app);
        Sh.Run("ln", "-sfn", app, Path.Combine(DesktopDir, $"Claude ({name}).app"));
        Console.WriteLine($"  created launcher: {app}");
    }

    public void RemoveLaunchers()
    {
        foreach (var name in Config.Accounts)
        {
            try { Directory.Delete(Path.Combine(Config.RouterHome, $"Claude ({name}).app"), true); } catch { }
            try { File.Delete(Path.Combine(DesktopDir, $"Claude ({name}).app")); } catch { }
        }
    }

    public IEnumerable<string> LaunchersPresent() =>
        Config.Accounts.Where(n => Directory.Exists(Path.Combine(Config.RouterHome, $"Claude ({n}).app")));

    // ---- watcher: launchd LaunchAgent ----
    public void InstallWatcher()
    {
        Directory.CreateDirectory(LaunchAgents);
        string plist = Path.Combine(LaunchAgents, $"{Config.WatcherId}.plist");
        File.WriteAllText(plist,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n<dict>\n" +
            $"  <key>Label</key><string>{Config.WatcherId}</string>\n" +
            "  <key>ProgramArguments</key>\n  <array>\n" +
            $"    <string>{Config.EnginePath}</string>\n    <string>watch</string>\n  </array>\n" +
            "  <key>RunAtLoad</key><true/>\n  <key>KeepAlive</key><true/>\n" +
            $"  <key>StandardErrorPath</key><string>{Path.Combine(Config.RouterHome, "watcher.err.log")}</string>\n" +
            $"  <key>StandardOutPath</key><string>{Path.Combine(Config.RouterHome, "watcher.out.log")}</string>\n" +
            "</dict>\n</plist>\n");
        Sh.Run("launchctl", "unload", plist);
        Sh.Run("launchctl", "load", plist);
    }

    public void RemoveWatcher()
    {
        string plist = Path.Combine(LaunchAgents, $"{Config.WatcherId}.plist");
        Sh.Run("launchctl", "unload", plist);
        try { File.Delete(plist); } catch { }
    }

    // ---- launch + UI ----
    public void LaunchAccount(string account, string? url)
    {
        string dir = Config.DataDirFor(account);
        Directory.CreateDirectory(dir);
        var args = new List<string> { "-n", "-a", ClaudeApp, "--args", $"--user-data-dir={dir}" };
        if (!string.IsNullOrEmpty(url)) args.Add(url);
        Sh.Start("open", args);
    }

    public string? ChooseAccount()
    {
        string buttons = string.Join(",", Config.Accounts.Select(a => $"\"{a}\""));
        string last = Config.Accounts[^1];
        var (c, o) = Sh.Run("osascript", "-e",
            $"button returned of (display dialog \"Which account are you signing into?\" buttons {{{buttons}}} default button \"{last}\" with title \"Claude login\")");
        return c == 0 ? o.Trim() : null;
    }

    public void Notify(string message) =>
        Sh.Run("osascript", "-e", $"display dialog \"{message}\" buttons {{\"OK\"}} with title \"Claude Router\"");

    // ---- Claude presence / auto-install ----
    public bool ClaudePresent() => Directory.Exists(ClaudeApp);

    public bool EnsureClaude()
    {
        if (ClaudePresent()) return true;
        Notify("Claude isn't installed, so I'll download and install it now. This takes a minute.");
        if (TryInstall() && ClaudePresent()) return true;
        Notify("Couldn't install Claude automatically. Install it from https://claude.ai/download , then run setup again.");
        return false;
    }

    static bool TryInstall()
    {
        try
        {
            string tmp = Directory.CreateTempSubdirectory().FullName;
            string dmg = Path.Combine(tmp, "Claude.dmg");
            if (Sh.Run("curl", "-fL", "--retry", "2", "-o", dmg,
                    "https://claude.ai/api/desktop/darwin/universal/dmg/latest/redirect").code != 0) return false;
            string mnt = Path.Combine(tmp, "mnt");
            Directory.CreateDirectory(mnt);
            if (Sh.Run("hdiutil", "attach", "-nobrowse", "-quiet", "-mountpoint", mnt, dmg).code != 0) return false;
            string? srcApp = Directory.EnumerateDirectories(mnt, "*.app").FirstOrDefault();
            bool ok = false;
            if (srcApp != null) ok = Sh.Run("cp", "-R", srcApp, "/Applications/").code == 0;
            Sh.Run("hdiutil", "detach", mnt, "-quiet");
            return ok;
        }
        catch (Exception ex) { Config.Log($"mac install err: {ex.Message}"); return false; }
    }

    public void Tag()
    {
        if (!File.Exists(Lsregister)) return;
        foreach (var name in Config.Accounts)
            Sh.Run(Lsregister, "-f", Path.Combine(Config.RouterHome, $"Claude ({name}).app"));
    }

    public string ClaudeLocationLine() => $"Claude app  : {ClaudeApp} {(ClaudePresent() ? "(found)" : "(missing)")}";
}
