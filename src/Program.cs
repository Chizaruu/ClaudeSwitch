using ClaudeSwitch;

// Entry point. Dispatch is OS-agnostic; the platform is chosen once here.
string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
string[] rest = args.Length > 1 ? args[1..] : Array.Empty<string>();

try
{
    var router = new Router(PlatformFactory.Current());
    return router.Dispatch(cmd, rest);
}
catch (Exception ex)
{
    Config.Log($"{cmd} error: {ex.Message}");
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
