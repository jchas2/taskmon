using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// LSSharedFileList (LaunchServices/LSSharedFileList.h).
// Deprecated since 10.11 but still the only non-root API exposing the user's "Open at Login" items.
// Should Apple remove it, the exports fail to bind and no items are returned.
public static class LaunchServices
{
    public const uint kLSSharedFileListNoUserInteraction = 1;
    public const uint kLSSharedFileListDoNotMountVolumes = 2;

    public const string kLSSharedFileListSessionLoginItems = "kLSSharedFileListSessionLoginItems";

    [DllImport(Libraries.CoreServices)]
    public static extern nint LSSharedFileListCopySnapshot(nint list, out uint seed);

    [DllImport(Libraries.CoreServices)]
    public static extern nint LSSharedFileListCreate(nint allocator, nint listType, nint listOptions);

    [DllImport(Libraries.CoreServices)]
    public static extern nint LSSharedFileListItemCopyDisplayName(nint item);

    [DllImport(Libraries.CoreServices)]
    public static extern nint LSSharedFileListItemCopyResolvedURL(nint item, uint flags, nint error);
}
