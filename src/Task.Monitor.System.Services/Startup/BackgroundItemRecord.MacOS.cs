#if __APPLE__
namespace Task.Monitor.System.Services.Startup;

// The values sfltool dumpbtm prints as "Type: legacy agent (0x10008)" etc.
[Flags]
public enum BackgroundItemType : long
{
    None      = 0,
    App       = 0x2,     
    LoginItem = 0x4,     
    Agent     = 0x8,
    Daemon    = 0x10,
    Developer = 0x20,    
    Legacy    = 0x10000  
}

[Flags]
public enum BackgroundItemDisposition : long
{
    None     = 0,
    Enabled  = 0x1,
    Allowed  = 0x2,
    Notified = 0x8
}

public sealed class BackgroundItemRecord
{
    public string?                   Name             { get; init; }
    public string?                   DeveloperName    { get; init; }
    public string?                   Identifier       { get; init; }
    public string?                   ParentIdentifier { get; init; }
    public string?                   BundleIdentifier { get; init; }
    public string?                   ExecutablePath   { get; init; }
    public string?                   Url              { get; init; }
    public BackgroundItemType        Type             { get; init; }
    public BackgroundItemDisposition Disposition      { get; init; }

    public bool IsApproved =>
        Disposition.HasFlag(BackgroundItemDisposition.Enabled) &&
        Disposition.HasFlag(BackgroundItemDisposition.Allowed);

    public bool IsLegacy => Type.HasFlag(BackgroundItemType.Legacy);

    public bool Is(BackgroundItemType type) => (Type & type) == type;

    public string? ResolvePath(string? parentPath)
    {
        if (string.IsNullOrEmpty(Url)) {
            return null;
        }

        if (Uri.TryCreate(Url, UriKind.Absolute, out Uri? absolute)) {
            return absolute.IsFile ? absolute.LocalPath.TrimEnd('/') : null;
        }

        return parentPath is null
            ? null
            : Path.Combine(parentPath, Uri.UnescapeDataString(Url)).TrimEnd('/');
    }
}
#endif
