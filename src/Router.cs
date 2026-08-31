namespace ClaudeSwitch;

/// <summary>
/// OS-agnostic orchestration: the subcommands (setup/launch/handle/watch/…)
/// expressed once against <see cref="IPlatform"/>.
/// </summary>
sealed class Router
{
    readonly IPlatform _os;
    public Router(IPlatform os) => _os = os;

    public int Dispatch(string cmd, string[] rest)
    {
        switch (cmd)
        {
            case "status": Status(); break;
            case "register": _os.Register(); Console.WriteLine("Registered as the claude:// handler."); break;
            case "unregister": _os.Unregister(); Console.WriteLine("Handed the claude:// handler back."); break;
            case "setup":
            case "install": return Setup(rest);
            case "uninstall": Uninstall(); break;
            case "launch": return Launch(Arg(rest, 0, "account name"));
            case "handle": return Handle(Arg(rest, 0, "url"));
            case "watch": Watch(); break;
            case "tag": _os.Tag(); Console.WriteLine("Re-asserted per-account launcher identity."); break;
            case "primary": return SetPrimary(rest);
            case "test": Test(); break;
            default:
                Console.Error.WriteLine($"Unknown command: {cmd}");
                Console.Error.WriteLine("Commands: status register unregister setup install uninstall launch watch tag primary test handle");
                return 1;
        }
        return 0;
    }

    static string Arg(string[] rest, int i, string what)
    {
        if (rest.Length <= i) { Console.Error.WriteLine($"{what} required"); Environment.Exit(1); }
        return rest[i];
    }

    int Setup(string[] rest)
    {
        // Optional: choose which account owns Claude's default profile (and thus
        // gets Cowork on Windows): `setup --primary Work`.
        for (int i = 0; i + 1 < rest.Length; i++)
            if (rest[i] == "--primary" && Config.IsAccount(rest[i + 1]))
                Config.SetPrimary(rest[i + 1]);

        Directory.CreateDirectory(Config.RouterHome);
        InstallEngineAndIcons();
        if (!_os.EnsureClaude()) return 1;
        _os.Register();
        _os.MakeLaunchers();
        _os.InstallWatcher();
        StartWatcherNow();
        _os.Tag();
        Console.WriteLine();
        Console.WriteLine("All set.");
        Console.WriteLine("  • Open your accounts from the 'Claude (Personal)' and 'Claude (Work)' launchers.");
        Console.WriteLine("  • Each account keeps its own data folder and its own taskbar/dock icon.");
        Console.WriteLine("  • At sign-in, pick the account in the small chooser.");
        if (OperatingSystem.IsWindows())
            Console.WriteLine($"  • Cowork works in the '{Config.Primary}' account (change with:  ClaudeRouter primary <account>).");
        return 0;
    }

    // Choose which account owns Claude's default profile (and thus Cowork on
    // Windows). Persisted; takes effect on the accounts' next launch.
    int SetPrimary(string[] rest)
    {
        string name = Arg(rest, 0, "account name");
        if (!Config.IsAccount(name))
        {
            Console.Error.WriteLine($"Unknown account: {name} (known: {string.Join(", ", Config.Accounts)})");
            return 1;
        }
        Config.SetPrimary(name);
        string msg = $"'{name}' now uses Claude's default profile — it's the Cowork-capable account.\\n\\n" +
                     "Close and re-open both accounts for this to take effect. Note: the accounts' " +
                     "profiles are reassigned, so you may need to sign in again.";
        _os.Notify(msg);
        Console.WriteLine($"Primary (Cowork) account set to '{name}'.");
        return 0;
    }

    int Launch(string name)
    {
        if (!Config.IsAccount(name))
        {
            Console.Error.WriteLine($"Unknown account: {name} (known: {string.Join(", ", Config.Accounts)})");
            return 1;
        }
        _os.LaunchAccount(name, null);
        Thread.Sleep(3000);
        EnsureRegistered(); // Claude grabs claude:// at startup; take it back
        _os.Tag();
        Config.Log($"launched '{name}' and re-asserted broker");
        return 0;
    }

    int Handle(string url)
    {
        Config.Log($"callback: {url}");
        if (url.Contains("router-test", StringComparison.OrdinalIgnoreCase))
        {
            _os.Notify($"SUCCESS — broker intercepted:\n{url}");
            return 0;
        }
        string? choice = _os.ChooseAccount();
        if (string.IsNullOrEmpty(choice)) { Config.Log("no target chosen; dropped"); return 0; }
        if (!Config.IsAccount(choice)) { Config.Log($"bad choice: {choice}"); return 0; }
        Config.Log($"forwarding to '{choice}'");
        _os.LaunchAccount(choice, url);
        Config.Log("forward launched");
        return 0;
    }

    void Watch()
    {
        Directory.CreateDirectory(Config.RouterHome);
        if (WatcherRunning() && ReadPid() != Environment.ProcessId)
        {
            Config.Log($"watcher already running (pid {ReadPid()}); exiting");
            return;
        }
        File.WriteAllText(Config.PidFile, Environment.ProcessId.ToString());
        Config.Log($"watcher started (pid {Environment.ProcessId})");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDelete(Config.PidFile);
        while (true)
        {
            try { EnsureRegistered(); _os.Tag(); } catch (Exception ex) { Config.Log($"watch loop err: {ex.Message}"); }
            Thread.Sleep(5000);
        }
    }

    void Uninstall()
    {
        _os.Unregister();
        StopWatcher();
        _os.RemoveWatcher();
        _os.RemoveLaunchers();
        Console.WriteLine("Removed the ClaudeSwitch handler, launchers and watcher.");
        Console.WriteLine($"Your account data and {Config.LogFile} were left untouched.");
        Console.WriteLine("To go back to a single Claude, open Claude once so it re-claims claude://.");
    }

    void Status()
    {
        Console.WriteLine($"===== ClaudeSwitch (unified .NET) status — {_os.Name} =====");
        Console.WriteLine($"Router home : {Config.RouterHome}");
        Console.WriteLine($"Installed   : {(File.Exists(Config.InstalledEnginePath) ? "yes" : "no")}");
        foreach (var a in Config.Accounts)
            Console.WriteLine($"{a,-8} dir : {Config.DataDirFor(a)}{(a == Config.Primary ? "   [primary — Cowork]" : "")}");
        Console.WriteLine(_os.ClaudeLocationLine());
        Console.WriteLine($"claude:// handler : {_os.CurrentHandler() ?? "(none)"}");
        Console.WriteLine($"Handler is ours   : {(_os.HandlerIsOurs() ? "yes" : "no")}");
        Console.WriteLine($"Watcher running   : {(WatcherRunning() ? "yes" : "no")}");
        var present = _os.LaunchersPresent().ToArray();
        Console.WriteLine($"Launchers present : {(present.Length == 0 ? "none" : string.Join(" ", present))}");
    }

    void Test()
    {
        string url = "claude://router-test-12345";
        if (OperatingSystem.IsWindows()) Sh.Start("cmd", new[] { "/c", "start", "", url }, shellExecute: true);
        else if (OperatingSystem.IsMacOS()) Sh.Run("open", url);
        else Sh.Run("xdg-open", url);
        Console.WriteLine($"Fired {url} — a success box should appear if the broker is registered.");
    }

    // --- shared helpers ---
    void EnsureRegistered()
    {
        if (!_os.HandlerIsOurs()) { _os.Register(); Config.Log("re-claimed claude:// handler"); }
    }

    // Copy this executable + the icons into the stable install location, so the
    // handler, launchers and watcher all point at one path.
    void InstallEngineAndIcons()
    {
        try
        {
            string self = Environment.ProcessPath ?? "";
            string dest = Config.InstalledEnginePath;
            if (!string.IsNullOrEmpty(self) &&
                !string.Equals(Path.GetFullPath(self), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, dest, true);

            // Icons ship next to the binary under ./assets (repo assets/ at publish time).
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            foreach (var a in Config.Accounts)
            {
                string src = Path.Combine(assets, $"{a}.{Config.IconExt}");
                if (File.Exists(src)) File.Copy(src, Config.IconFor(a), true);
            }
        }
        catch (Exception ex) { Config.Log($"install engine/icons: {ex.Message}"); }
    }

    void StartWatcherNow()
    {
        if (WatcherRunning()) return;
        Sh.Start(Config.EnginePath, new[] { "watch" });
    }

    static bool WatcherRunning()
    {
        try
        {
            if (!File.Exists(Config.PidFile)) return false;
            if (!int.TryParse(File.ReadAllText(Config.PidFile).Trim(), out int pid)) return false;
            try { System.Diagnostics.Process.GetProcessById(pid); return true; }
            catch { return false; }
        }
        catch { return false; }
    }

    static int ReadPid()
    {
        try { return int.Parse(File.ReadAllText(Config.PidFile).Trim()); } catch { return -1; }
    }

    void StopWatcher()
    {
        try
        {
            if (WatcherRunning())
            {
                int pid = ReadPid();
                if (pid > 0) { try { System.Diagnostics.Process.GetProcessById(pid).Kill(); } catch { } }
            }
        }
        catch { }
        TryDelete(Config.PidFile);
    }

    static void TryDelete(string p) { try { File.Delete(p); } catch { } }
}
