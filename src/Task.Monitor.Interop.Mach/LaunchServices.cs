using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// LSSharedFileList (LaunchServices/LSSharedFileList.h, via the CoreServices umbrella). Deprecated
// since 10.11 but still the only non-root API exposing the user's "Open at Login" items; verified
// working on macOS 26. Should Apple remove it, the exports fail to bind and no items are returned.
public static class LaunchServices
{
    private const uint kLSSharedFileListNoUserInteraction = 1;
    private const uint kLSSharedFileListDoNotMountVolumes = 2;

    private static readonly Lazy<IntPtr> SessionLoginItems = new(() =>
        DyLib.ReadExportedPointer(Libraries.CoreServices, "kLSSharedFileListSessionLoginItems"));

    public readonly record struct LoginItem(string Name, string? Path);

    [DllImport(Libraries.CoreServices)]
    private static extern IntPtr LSSharedFileListCreate(IntPtr allocator, IntPtr listType, IntPtr listOptions);

    [DllImport(Libraries.CoreServices)]
    private static extern IntPtr LSSharedFileListCopySnapshot(IntPtr list, out uint seed);

    [DllImport(Libraries.CoreServices)]
    private static extern IntPtr LSSharedFileListItemCopyDisplayName(IntPtr item);

    [DllImport(Libraries.CoreServices)]
    private static extern IntPtr LSSharedFileListItemCopyResolvedURL(IntPtr item, uint flags, IntPtr error);

    // The current user's "Open at Login" items (System Settings > General > Login Items).
    public static List<LoginItem> GetSessionLoginItems()
    {
        List<LoginItem> items = new();

        IntPtr listType = SessionLoginItems.Value;

        if (listType == IntPtr.Zero) {
            return items;
        }

        IntPtr listRef;

        try {
            listRef = LSSharedFileListCreate(IntPtr.Zero, listType, IntPtr.Zero);
        }
        catch (EntryPointNotFoundException) {
            return items;
        }

        using CFScope list = new(listRef);

        if (list.IsNull) {
            return items;
        }

        using CFScope snapshot = new(LSSharedFileListCopySnapshot(list, out _));

        if (snapshot.IsNull) {
            return items;
        }

        long count = CoreFoundation.CFArrayGetCount(snapshot);

        for (long i = 0; i < count; i++) {
            IntPtr item = CoreFoundation.CFArrayGetValueAtIndex(snapshot, i);
            string? path = CopyResolvedPath(item);
            string? name = CopyDisplayName(item) ?? (path is null ? null : Path.GetFileNameWithoutExtension(path));

            if (name is { Length: > 0 }) {
                items.Add(new LoginItem(name, path));
            }
        }

        return items;
    }

    private static string? CopyDisplayName(IntPtr item)
    {
        using CFScope name = new(LSSharedFileListItemCopyDisplayName(item));

        return CoreFoundation.GetString(name);
    }

    private static string? CopyResolvedPath(IntPtr item)
    {
        // Never prompt or mount: an item on an unmounted volume simply has no path.
        using CFScope url = new(LSSharedFileListItemCopyResolvedURL(
            item,
            kLSSharedFileListNoUserInteraction | kLSSharedFileListDoNotMountVolumes,
            IntPtr.Zero));

        return CoreFoundation.GetPath(url);
    }
}
