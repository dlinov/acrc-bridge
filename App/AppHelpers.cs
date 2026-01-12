namespace ACRCBridge.App;

public static class AppHelpers
{
    public static bool HasArg(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}
