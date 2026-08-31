// ClaudeRouter.exe  —  native claude:// login router + per-account colored
// taskbar buttons + background watcher, in one small exe.
//
// Commands:
//   ClaudeRouter.exe setup        register handler, make shortcuts, start watcher
//   ClaudeRouter.exe handle <url>  (run by Windows on a claude:// callback)
//   ClaudeRouter.exe launch <name> start Personal|Work + reclaim the handler
//   ClaudeRouter.exe watch         resident watcher (auto-colors windows)
//   ClaudeRouter.exe tag           color the open windows once
//   ClaudeRouter.exe register / unregister / status
//
// Build with build.bat (uses the C# compiler that ships with Windows).

using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class ClaudeRouter
{
    // ---------- locations ----------
    static readonly string LOCAL   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string RouterHome  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeRouter");
    static readonly string PersonalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");
    static readonly string WorkDir     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude-Work");
    static readonly string[] Accounts  = { "Personal", "Work" };
    static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeRouter", "router.log");
    static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }
    // The stable installed location everything persistent points at (registry
    // handler, shortcuts, relaunch commands) - independent of where a given run
    // happens to launch from.
    static readonly string InstalledExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeRouter", "ClaudeRouter.exe");

    static string PortableExe()
    {
        string known = Path.Combine(LOCAL, "ClaudePortable", "app", "claude.exe");
        if (File.Exists(known)) return known;
        string dir = Path.Combine(LOCAL, "ClaudePortable");
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "claude*.exe", SearchOption.AllDirectories)) return f;
        return known;
    }
    static string DirFor(string name) { return name == "Work" ? WorkDir : PersonalDir; }
    static void Log(string m)
    {
        try { Directory.CreateDirectory(RouterHome); File.AppendAllText(LogFile, DateTime.Now.ToString("o") + "  " + m + Environment.NewLine); } catch {}
    }

    // ================= entry point =================
    [STAThread]
    static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        string arg = args.Length > 1 ? args[1] : "";
        try
        {
            switch (cmd)
            {
                case "handle":     Handle(arg); break;
                case "launch":     Launch(arg); break;
                case "watch":      try { Directory.CreateDirectory(RouterHome); Directory.SetCurrentDirectory(RouterHome); } catch {} Watch(); break;
                case "tag":        TagAll(); break;
                case "register":   Register(); break;
                case "unregister": Unregister(); break;
                case "setup":      Setup(); break;
                case "install":    Install(); break;
                case "uninstall":  UninstallAll(); break;
                case "makeportable":
                case "portable":   MakePortable(); break;
                case "status":     Status(); break;
                default:           Install(); break;   // double-click = install
            }
        }
        catch (Exception ex) { Log(cmd + " error: " + ex.Message); }
        return 0;
    }

    // ================= claude:// registration =================
    static string HandlerCommand { get { return "\"" + InstalledExe + "\" handle \"%1\""; } }

    static void Register()
    {
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\claude"))
        {
            k.SetValue("", "URL:Claude Protocol");
            k.SetValue("URL Protocol", "");
            using (var c = k.CreateSubKey(@"shell\open\command")) c.SetValue("", HandlerCommand);
        }
    }
    static void Unregister()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\claude", false); } catch {}
    }
    static string CurrentHandler()
    {
        try { using (var c = Registry.CurrentUser.OpenSubKey(@"Software\Classes\claude\shell\open\command"))
              return c == null ? null : (c.GetValue("") as string); }
        catch { return null; }
    }
    static void EnsureRegistered()
    {
        if (!string.Equals(CurrentHandler(), HandlerCommand, StringComparison.OrdinalIgnoreCase))
        { Register(); Log("re-claimed claude:// handler"); }
    }

    // ================= the callback handler =================
    static void Handle(string url)
    {
        Log("callback: " + url);
        if (url.IndexOf("router-test", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MessageBox.Show("SUCCESS - intercepted:\n\n" + url, "Claude Router",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string choice = ShowChooser();
        if (choice == null) { Log("no target chosen; dropped"); return; }
        string dir = DirFor(choice);
        Log("forwarding to '" + choice + "'");
        StartClaude(dir, url);
    }

    static string ShowChooser()
    {
        string result = null;
        using (var f = new Form())
        {
            f.Text = "Claude login"; f.Width = 340; f.Height = 140;
            f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true;
            f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false;
            var lbl = new Label(); lbl.Text = "Which account are you signing into?"; lbl.Left = 18; lbl.Top = 15; lbl.Width = 300;
            f.Controls.Add(lbl);
            int x = 25;
            foreach (string name in Accounts)
            {
                var b = new Button(); b.Text = name; b.Left = x; b.Top = 50; b.Width = 130; b.Height = 34; x += 145;
                string captured = name;
                b.Click += delegate { result = captured; f.Close(); };
                f.Controls.Add(b);
            }
            f.ShowDialog();
        }
        return result;
    }

    // ================= launching Claude =================
    static void StartClaude(string dir, string url)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = PortableExe();
        psi.Arguments = "--user-data-dir=\"" + dir + "\"" + (string.IsNullOrEmpty(url) ? "" : " \"" + url + "\"");
        psi.UseShellExecute = false;
        Process.Start(psi);
    }

    static void Launch(string name)
    {
        if (Array.IndexOf(Accounts, name) < 0) { Log("unknown account: " + name); return; }
        StartClaude(DirFor(name), null);
        Thread.Sleep(3000);
        EnsureRegistered();
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(700);
            bool found = false;
            foreach (var p in Process.GetProcessesByName("claude"))
                try { if (p.MainWindowHandle != IntPtr.Zero && AccountFor(p.Id) == name) { found = true; break; } } catch {}
            if (found) break;
        }
        TagAll();
    }

    // ================= the watcher =================
    static void Watch()
    {
        Log("watcher started (pid " + Process.GetCurrentProcess().Id + ")");
        while (true)
        {
            try { EnsureRegistered(); TagAll(); } catch (Exception ex) { Log("watch loop err: " + ex.Message); }
            Thread.Sleep(3000);
        }
    }

    // ================= per-account taskbar identity =================
    static string AccountFor(int pid)
    {
        try
        {
            using (var s = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + pid))
                foreach (ManagementObject mo in s.Get())
                {
                    string dir = ExtractDir(mo["CommandLine"] as string);
                    if (dir == null) return null;
                    dir = dir.TrimEnd('\\');
                    if (string.Equals(dir, PersonalDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return "Personal";
                    if (string.Equals(dir, WorkDir.TrimEnd('\\'),     StringComparison.OrdinalIgnoreCase)) return "Work";
                }
        }
        catch {}
        return null;
    }
    static string ExtractDir(string cl)
    {
        if (string.IsNullOrEmpty(cl)) return null;
        int i = cl.IndexOf("--user-data-dir=", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += "--user-data-dir=".Length;
        if (i < cl.Length && cl[i] == '"') { int j = cl.IndexOf('"', i + 1); if (j < 0) return null; return cl.Substring(i + 1, j - i - 1); }
        int k = cl.IndexOf(' ', i); if (k < 0) k = cl.Length; return cl.Substring(i, k - i);
    }

    static void TagAll()
    {
        foreach (var p in Process.GetProcessesByName("claude"))
        {
            IntPtr h;
            try { h = p.MainWindowHandle; } catch { continue; }
            if (h == IntPtr.Zero) continue;
            string acct = AccountFor(p.Id);
            if (acct == null) continue;
            string want = "ClaudeRouter." + acct;
            if (GetAumid(h) == want) continue;
            string ico = Path.Combine(RouterHome, acct + ".ico");
            string iconRes = File.Exists(ico) ? (ico + ",0") : "";
            string relaunch = "\"" + InstalledExe + "\" launch " + acct;
            Tag(h, want, relaunch, iconRes, "Claude (" + acct + ")");
            if (iconRes != "") SetIcon(h, ico);
            Regroup(h);
            Log("tagged window as '" + acct + "'");
        }
    }

    // ================= setup / status =================
    static void KillLegacy()
    {
        // stop the previous separate native watcher, if any
        foreach (var p in Process.GetProcessesByName("ClaudeRouterWatcher")) { try { p.Kill(); } catch {} }
        // stop any old script-host launcher/watcher from an earlier version
        try
        {
            using (var s = new ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='wscript.exe' OR Name='cscript.exe'"))
                foreach (ManagementObject mo in s.Get())
                {
                    string cl = (mo["CommandLine"] as string) ?? "";
                    if (cl.IndexOf("watch.vbs",   StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cl.IndexOf("runner.vbs",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cl.IndexOf("handler.vbs", StringComparison.OrdinalIgnoreCase) >= 0)
                        try { Process.GetProcessById(Convert.ToInt32(mo["ProcessId"])).Kill(); } catch {}
                }
        }
        catch {}
        // remove old script-based helper files
        foreach (string f in new[] { "handler.vbs", "runner.vbs", "watch.vbs", "ClaudeRouterWatcher.exe" })
            try { File.Delete(Path.Combine(RouterHome, f)); } catch {}
        // stop any other copies of THIS watcher (avoid duplicates)
        int me = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName("ClaudeRouter")) { if (p.Id != me) try { p.Kill(); } catch {} }
    }

    // Build the portable Claude copy the launchers run from, by copying the
    // currently-running Claude out to %LOCALAPPDATA%\ClaudePortable. Silent;
    // returns true if the portable exe exists afterwards.
    static bool TryBuildPortable()
    {
        string exePath = null;
        foreach (var p in Process.GetProcessesByName("claude"))
        { try { exePath = p.MainModule.FileName; break; } catch {} }
        if (string.IsNullOrEmpty(exePath)) return false;

        string appDir = Path.GetDirectoryName(exePath);
        string src, dest;
        if (string.Equals(Path.GetFileName(appDir), "app", StringComparison.OrdinalIgnoreCase))
        { src = Path.GetDirectoryName(appDir); dest = Path.Combine(LOCAL, "ClaudePortable"); }
        else
        { src = appDir; dest = Path.Combine(LOCAL, "ClaudePortable", "app"); }

        foreach (var p in Process.GetProcessesByName("claude")) { try { p.Kill(); } catch {} }
        Thread.Sleep(1500);
        try { Directory.CreateDirectory(dest); } catch {}

        var psi = new ProcessStartInfo();
        psi.FileName = "robocopy";
        psi.Arguments = "\"" + src + "\" \"" + dest + "\" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP";
        psi.UseShellExecute = false; psi.CreateNoWindow = true;
        try { var pr = Process.Start(psi); pr.WaitForExit(); } catch (Exception ex) { Log("robocopy err: " + ex.Message); }
        return File.Exists(PortableExe());
    }

    // Download and install Claude Desktop when none is present, then wait for it
    // to come up. Returns true once a claude.exe is running.
    static bool TryDownloadAndInstallClaude()
    {
        try
        {
            string arch = "x64";
            string pa = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            string pa2 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "";
            if (pa.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pa2.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0) arch = "arm64";

            string url = "https://claude.ai/api/desktop/win32/" + arch + "/setup/latest/redirect";
            string dst = Path.Combine(Path.GetTempPath(), "ClaudeSetup.exe");

            // .NET Framework defaults can be too old for the CDN; force TLS 1.2.
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch {}

            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "Mozilla/5.0");
                wc.DownloadFile(url, dst);
            }
            if (!File.Exists(dst) || new FileInfo(dst).Length < 100000) { Log("download too small"); return false; }

            var psi = new ProcessStartInfo(dst); psi.UseShellExecute = true;   // allow its own UI/UAC
            Process.Start(psi);

            // The installer lands and usually auto-launches Claude; wait for it.
            for (int i = 0; i < 180; i++)
            {
                Thread.Sleep(1000);
                if (Process.GetProcessesByName("claude").Length > 0) { Thread.Sleep(4000); return true; }
            }
            return Process.GetProcessesByName("claude").Length > 0;
        }
        catch (Exception ex) { Log("download/install err: " + ex.Message); return false; }
    }

    // Silently uninstall the standalone Claude app, so it stops claiming the
    // claude:// login link and can't clash with the portable copies. Handles both
    // the Squirrel install (Update.exe --uninstall) and a Store/MSIX install.
    static bool TryRemoveInstalledClaude()
    {
        bool did = false;

        // 1) Squirrel install (what claude.ai/download installs) -> its own uninstaller
        try
        {
            string updater = Path.Combine(LOCAL, "AnthropicClaude", "Update.exe");
            if (File.Exists(updater))
            {
                foreach (var p in Process.GetProcessesByName("claude")) { try { p.Kill(); } catch {} }
                Thread.Sleep(800);
                var psi = new ProcessStartInfo(updater, "--uninstall -s");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                var pr = Process.Start(psi); if (pr != null) pr.WaitForExit(90000);
                try { Directory.Delete(Path.Combine(LOCAL, "AnthropicClaude"), true); } catch {}
                did = true;
                Log("uninstalled Squirrel Claude");
            }
        }
        catch (Exception ex) { Log("squirrel uninstall err: " + ex.Message); }

        // 2) Store / MSIX install -> winget (best-effort)
        try
        {
            var psi = new ProcessStartInfo("winget",
                "uninstall --name Claude --silent --disable-interactivity --accept-source-agreements");
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            var pr = Process.Start(psi);
            if (pr != null) { pr.WaitForExit(60000); if (pr.HasExited && pr.ExitCode == 0) did = true; }
        }
        catch (Exception ex) { Log("winget uninstall err: " + ex.Message); }

        return did;
    }

    // Manual entry point kept for convenience (setup does this automatically now).
    static void MakePortable() { Setup(); }

    // Self-installer: copy this exe + icons into place, then run setup. This is
    // what runs when the user double-clicks ClaudeRouterSetup.exe.
    static void Install()
    {
        Directory.CreateDirectory(RouterHome);
        ExtractIcons(RouterHome);
        string targetExe = InstalledExe;

        if (!string.Equals(ExePath, targetExe, StringComparison.OrdinalIgnoreCase))
        {
            // stop any running installed copy so we can overwrite it
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("ClaudeRouter")) if (p.Id != me) { try { p.Kill(); } catch {} }
            Exception last = null;
            bool copied = false;
            for (int i = 0; i < 10 && !copied; i++)
            {
                Thread.Sleep(300);
                try { File.Copy(ExePath, targetExe, true); copied = true; }
                catch (Exception ex) { last = ex; }
            }
            if (!copied)
            {
                MessageBox.Show("Could not install to:\n" + targetExe + "\n\n" + (last == null ? "" : last.Message),
                                "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // hand off to the installed copy so it registers under its own path
            var psi = new ProcessStartInfo(); psi.FileName = targetExe; psi.Arguments = "setup"; psi.UseShellExecute = false;
            psi.WorkingDirectory = RouterHome;   // don't inherit (and hold) the unzip folder
            Process.Start(psi);
        }
        else
        {
            Setup();
        }
    }

    static void ExtractIcons(string dir)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        foreach (string name in Accounts)
        {
            string res = name + ".ico";
            try { using (var s = asm.GetManifestResourceStream(res)) {
                if (s == null) continue;
                using (var f = File.Create(Path.Combine(dir, res))) s.CopyTo(f);
            } } catch {}
        }
    }

    static void TryDelete(string p) { try { File.Delete(p); } catch {} }

    static void UninstallAll()
    {
        Unregister();
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        TryDelete(Path.Combine(startup, "ClaudeRouterWatcher.lnk"));
        foreach (string n in Accounts) TryDelete(Path.Combine(desktop, "Claude (" + n + ").lnk"));
        int me = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName("ClaudeRouter")) if (p.Id != me) { try { p.Kill(); } catch {} }
        MessageBox.Show("Removed the router and its shortcuts.\n\nReinstall Claude from https://claude.ai/download to go back to a single app.\nYour login backups remain in:\n" + RouterHome,
                        "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // Remove dead shortcuts left by an earlier version (ones that launch a
    // now-deleted .vbs) from the Desktop, Startup, and the pinned taskbar.
    static void ScrubStaleShortcuts()
    {
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] folders = {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Path.Combine(appdata, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar")
        };
        foreach (string folder in folders)
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string lnk in Directory.GetFiles(folder, "*.lnk"))
                {
                    try
                    {
                        var link = (IShellLinkW)new ShellLink();
                        ((IPersistFile)link).Load(lnk, 0);
                        var sbP = new StringBuilder(1024); link.GetPath(sbP, sbP.Capacity, IntPtr.Zero, 0);
                        var sbA = new StringBuilder(1024); link.GetArguments(sbA, sbA.Capacity);
                        Marshal.ReleaseComObject(link);
                        string tgt = sbP.ToString().ToLowerInvariant();
                        string args = sbA.ToString().ToLowerInvariant();
                        bool stale = (tgt.EndsWith("wscript.exe") || tgt.EndsWith("cscript.exe")) &&
                                     (args.Contains("runner.vbs") || args.Contains("watch.vbs") || args.Contains("handler.vbs"));
                        if (stale) { try { File.Delete(lnk); } catch {} }
                    }
                    catch {}
                }
            }
            catch {}
        }
    }

    static void Setup()
    {
        Directory.CreateDirectory(RouterHome);
        ExtractIcons(RouterHome);
        KillLegacy();
        ScrubStaleShortcuts();

        // If the portable Claude the launchers run from is missing, build it
        // automatically from the running Claude. If Claude isn't present at all,
        // download and install it first - no manual step needed.
        if (!File.Exists(PortableExe()))
        {
            if (!TryBuildPortable())
            {
                MessageBox.Show(
                    "Claude isn't installed on this PC, so I'll download and install it now.\n\n" +
                    "This takes a minute or two. Click OK to start - a Claude installer may appear;\n" +
                    "let it finish, then everything else happens on its own.",
                    "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (!TryDownloadAndInstallClaude() || !TryBuildPortable())
                {
                    MessageBox.Show(
                        "I couldn't finish automatically. Please install Claude from\n" +
                        "https://claude.ai/download , open it once, then double-click build.bat again.",
                        "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        Register();

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string exeDir  = Path.GetDirectoryName(PortableExe());
        foreach (string name in Accounts)
        {
            string ico = Path.Combine(RouterHome, name + ".ico");
            string lnk = Path.Combine(desktop, "Claude (" + name + ").lnk");
            // Same AppUserModelID as the running window, so a pinned shortcut and
            // its window collapse into ONE colored taskbar button per account.
            try
            {
                CreateShortcut(lnk, InstalledExe, "launch " + name, File.Exists(ico) ? ico : PortableExe(),
                               "Claude " + name, exeDir, "ClaudeRouter." + name);
                Log("created shortcut: " + lnk);
            }
            catch (Exception ex) { Log("shortcut FAILED (" + lnk + "): " + ex.Message); }
        }
        string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        try
        {
            CreateShortcut(Path.Combine(startup, "ClaudeRouterWatcher.lnk"),
                           InstalledExe, "watch", InstalledExe, "Keeps Claude taskbar icons colored/separated", RouterHome, null);
        }
        catch (Exception ex) { Log("startup shortcut FAILED: " + ex.Message); }

        var psi = new ProcessStartInfo(); psi.FileName = InstalledExe; psi.Arguments = "watch"; psi.UseShellExecute = false;
        psi.WorkingDirectory = RouterHome;   // never hold the user's unzip folder open
        Process.Start(psi);
        TagAll();

        // Uninstall the standalone Claude app so it stops intercepting logins.
        bool removed = TryRemoveInstalledClaude();
        string tail = removed
            ? ""
            : "\n\n(If a 'Claude' app somehow still shows under Settings > Apps, you can remove it there.)";

        MessageBox.Show(
            "All set.\n\n" +
            "• Open your accounts from the 'Claude (Personal)' and 'Claude (Work)' desktop icons.\n" +
            "• Each gets its own colored taskbar button automatically.\n" +
            "• At sign-in, pick the account in the small chooser." + tail,
            "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void Status()
    {
        bool watching = false;
        int me = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName("ClaudeRouter")) if (p.Id != me) watching = true;
        MessageBox.Show(
            "claude:// handler:\n  " + (CurrentHandler() ?? "(none)") + "\n\n" +
            "Ours: " + (string.Equals(CurrentHandler(), HandlerCommand, StringComparison.OrdinalIgnoreCase) ? "yes" : "no") + "\n" +
            "Watcher running: " + (watching ? "yes" : "no") + "\n\n" +
            "Personal: " + PersonalDir + "\nWork: " + WorkDir,
            "Claude Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ================= shortcut (.lnk) creation via IShellLink =================
    static void CreateShortcut(string lnkPath, string target, string args, string icon, string desc, string workdir, string aumid)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(target);
        if (!string.IsNullOrEmpty(args))    link.SetArguments(args);
        if (!string.IsNullOrEmpty(workdir)) link.SetWorkingDirectory(workdir);
        if (!string.IsNullOrEmpty(icon))    link.SetIconLocation(icon, 0);
        if (!string.IsNullOrEmpty(desc))    link.SetDescription(desc);
        if (!string.IsNullOrEmpty(aumid))
        {
            try
            {
                var store = (IPropertyStore)link;   // the ShellLink also implements IPropertyStore
                SetStr(store, 5, aumid);            // PKEY_AppUserModel_ID
                store.Commit();
            }
            catch (Exception ex) { Log("shortcut aumid skipped: " + ex.Message); }
        }
        ((IPersistFile)link).Save(lnkPath, true);
        Marshal.ReleaseComObject(link);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder f, int cch, IntPtr pfd, uint flags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short w);
        void SetHotkey(short w);
        void GetShowCmd(out int cmd);
        void SetShowCmd(int cmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int cch, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string rel, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    interface IPersistFile
    {
        void GetClassID(out Guid id);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string f, int mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string f, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string f);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string f);
    }

    // ================= window-identity interop (AUMID + icon + regroup) =================
    [DllImport("shell32.dll")]
    static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pvar);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadImage(IntPtr h, string name, uint type, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);

    [StructLayout(LayoutKind.Sequential)]
    struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public uint pid; public PROPERTYKEY(Guid f, uint p){ fmtid = f; pid = p; } }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        int GetCount(out uint c);
        int GetAt(uint i, out PROPERTYKEY k);
        int GetValue(ref PROPERTYKEY k, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY k, ref PROPVARIANT pv);
        int Commit();
    }
    static readonly Guid AppFmt = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    static readonly Guid StoreIid = new Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

    static void SetStr(IPropertyStore s, uint pid, string v)
    {
        if (string.IsNullOrEmpty(v)) return;
        var k = new PROPERTYKEY(AppFmt, pid);
        var pv = new PROPVARIANT(); pv.vt = 31; pv.p = Marshal.StringToCoTaskMemUni(v);
        try { s.SetValue(ref k, ref pv); } finally { PropVariantClear(ref pv); }
    }
    static string GetAumid(IntPtr h)
    {
        Guid iid = StoreIid; IPropertyStore s;
        if (SHGetPropertyStoreForWindow(h, ref iid, out s) != 0) return null;
        try
        {
            var k = new PROPERTYKEY(AppFmt, 5); PROPVARIANT pv;
            if (s.GetValue(ref k, out pv) != 0) return null;
            try { if (pv.p != IntPtr.Zero && (pv.vt == 31 || pv.vt == 8)) return Marshal.PtrToStringUni(pv.p); return null; }
            finally { PropVariantClear(ref pv); }
        }
        finally { Marshal.ReleaseComObject(s); }
    }
    static void Tag(IntPtr h, string aumid, string relaunch, string iconRes, string disp)
    {
        Guid iid = StoreIid; IPropertyStore s;
        if (SHGetPropertyStoreForWindow(h, ref iid, out s) != 0) return;
        SetStr(s, 5, aumid); SetStr(s, 2, relaunch); SetStr(s, 3, iconRes); SetStr(s, 4, disp);
        s.Commit(); Marshal.ReleaseComObject(s);
    }
    static void SetIcon(IntPtr h, string ico)
    {
        const uint IMAGE_ICON = 1, LR = 0x10, WM_SETICON = 0x80;
        IntPtr big = LoadImage(IntPtr.Zero, ico, IMAGE_ICON, 32, 32, LR);
        IntPtr sm  = LoadImage(IntPtr.Zero, ico, IMAGE_ICON, 16, 16, LR);
        if (big != IntPtr.Zero) SendMessage(h, WM_SETICON, (IntPtr)1, big);
        if (sm  != IntPtr.Zero) SendMessage(h, WM_SETICON, (IntPtr)0, sm);
    }
    static void Regroup(IntPtr h)
    {
        const int SW_HIDE = 0, SW_MAX = 3, SW_NORM = 1;
        bool mx = IsZoomed(h); ShowWindow(h, SW_HIDE); Thread.Sleep(80); ShowWindow(h, mx ? SW_MAX : SW_NORM);
    }
}
