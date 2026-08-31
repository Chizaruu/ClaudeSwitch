namespace ClaudeSwitch;

/// <summary>
/// Everything that differs by OS lives behind this interface. The shared
/// orchestration in <see cref="Router"/> never branches on the OS itself —
/// it just calls these. Each concrete platform maps them to the correct
/// native mechanism (registry / xdg-mime / duti, etc.).
/// </summary>
interface IPlatform
{
    string Name { get; }

    // --- claude:// handler ---
    void Register();
    void Unregister();
    string? CurrentHandler();
    bool HandlerIsOurs();

    // --- per-account launchers (each with its own icon) ---
    void MakeLaunchers();
    void RemoveLaunchers();
    IEnumerable<string> LaunchersPresent();

    // --- self-heal watcher ---
    void InstallWatcher();
    void RemoveWatcher();

    // --- launching + UI ---
    void LaunchAccount(string account, string? url);
    string? ChooseAccount();
    void Notify(string message);

    // --- Claude presence / auto-install ---
    bool ClaudePresent();
    bool EnsureClaude();

    // --- best-effort per-account taskbar/dock identity refresh ---
    void Tag();

    /// <summary>Human-readable line for `status` describing where Claude is.</summary>
    string ClaudeLocationLine();
}

static class PlatformFactory
{
    public static IPlatform Current()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatform();
        if (OperatingSystem.IsMacOS()) return new MacPlatform();
        if (OperatingSystem.IsLinux()) return new LinuxPlatform();
        throw new PlatformNotSupportedException("ClaudeSwitch supports Windows, macOS and Linux.");
    }
}
