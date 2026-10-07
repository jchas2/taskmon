#if __APPLE__
namespace Task.Monitor.System.Services.Startup;

public sealed class LaunchdOverrides
{
    public static readonly LaunchdOverrides Empty = new(new Dictionary<string, bool>());

    private readonly IReadOnlyDictionary<string, bool> disabledByLabel;

    private LaunchdOverrides(IReadOnlyDictionary<string, bool> disabledByLabel) =>
        this.disabledByLabel = disabledByLabel;

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

    public bool Contains(string? label) => label is not null && disabledByLabel.ContainsKey(label);

    public StartupEntryState ResolveState(string? label, bool? plistDisabled, bool? approved = null)
    {
        if (approved == false) {
            return StartupEntryState.Disabled;
        }

        bool disabled = label is not null && disabledByLabel.TryGetValue(label, out bool overridden)
            ? overridden
            : plistDisabled ?? false;

        return disabled 
            ? StartupEntryState.Disabled 
            : StartupEntryState.Enabled;
    }
}
#endif
