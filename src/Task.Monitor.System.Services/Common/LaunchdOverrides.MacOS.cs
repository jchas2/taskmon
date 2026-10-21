#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Common;

public sealed class LaunchdOverrides
{
    private const string DatabasePath = "/private/var/db/com.apple.xpc.launchd";

    public static readonly LaunchdOverrides Empty = new(new Dictionary<string, bool>());

    private readonly IReadOnlyDictionary<string, bool> disabledByLabel;

    private LaunchdOverrides(IReadOnlyDictionary<string, bool> disabledByLabel) =>
        this.disabledByLabel = disabledByLabel;

    public bool Contains(string? label) => label is not null && disabledByLabel.ContainsKey(label);

    public bool IsDisabled(string? label, bool? plistDisabled, bool? approved = null)
    {
        if (approved == false) {
            return true;
        }

        return label is not null && disabledByLabel.TryGetValue(label, out bool overridden)
            ? overridden
            : plistDisabled ?? false;
    }

    public static LaunchdOverrides Parse(object? plist)
    {
        if (plist is not IReadOnlyDictionary<string, object?> overrides) {
            return Empty;
        }

        Dictionary<string, bool> disabledByLabel = new(StringComparer.Ordinal);

        foreach ((string label, object? value) in overrides) {
            if (value is bool disabled) {
                disabledByLabel[label] = disabled;
            }
        }

        return new LaunchdOverrides(disabledByLabel);
    }

    private static LaunchdOverrides Read(string fileName) =>
        Parse(PropertyList.ReadFile(Path.Combine(DatabasePath, fileName)));

    public static LaunchdOverrides ReadSystem() => Read("disabled.plist");

    public static LaunchdOverrides ReadUser(uint uid) => Read($"disabled.{uid}.plist");
}
#endif
