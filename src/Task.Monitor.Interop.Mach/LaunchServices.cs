using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// LSSharedFileList (LaunchServices/LSSharedFileList.h).
// Deprecated since 10.11 but still the only non-root API exposing the user's "Open at Login" items.
// Should Apple remove it, the exports fail to bind and no items are returned.
public static class LaunchServices
{
    private const uint kLSSharedFileListNoUserInteraction = 1;
    private const uint kLSSharedFileListDoNotMountVolumes = 2;

    private static readonly Lazy<IntPtr> SessionLoginItems = new(() =>
        DyLib.ReadExportedPointer(Libraries.CoreServices, "kLSSharedFileListSessionLoginItems"));

    public readonly record struct LoginItem(string Name, string? Path);

    private static string? CopyDisplayName(nint item)
    {
        using CFScope name = new(LSSharedFileListItemCopyDisplayName(item));

        return CoreFoundation.GetString(name);
    }

    private static string? CopyResolvedPath(nint item)
    {
        // Never prompt or mount: an item on an unmounted volume simply has no path.
        using CFScope url = new(LSSharedFileListItemCopyResolvedURL(
            item,
            kLSSharedFileListNoUserInteraction | kLSSharedFileListDoNotMountVolumes,
            nint.Zero));

        return CoreFoundation.GetPath(url);
    }

    public static List<LoginItem> GetSessionLoginItems()
    {
        List<LoginItem> items = new();
        nint listType         = SessionLoginItems.Value;

        if (listType == nint.Zero) {
            return items;
        }

        nint listRef;

        try {
            listRef = LSSharedFileListCreate(nint.Zero, listType, nint.Zero);
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
            nint item    = CoreFoundation.CFArrayGetValueAtIndex(snapshot, i);
            string? path = CopyResolvedPath(item);
            
            string? name = CopyDisplayName(item) ?? (path is null 
                ? null 
                : Path.GetFileNameWithoutExtension(path));

            if (name is { Length: > 0 }) {
                items.Add(new LoginItem(name, path));
            }
        }

        return items;
    }

    [DllImport(Libraries.CoreServices)]
    private static extern nint LSSharedFileListCopySnapshot(nint list, out uint seed);

    [DllImport(Libraries.CoreServices)]
    private static extern nint LSSharedFileListCreate(nint allocator, nint listType, nint listOptions);

    [DllImport(Libraries.CoreServices)]
    private static extern nint LSSharedFileListItemCopyDisplayName(nint item);

    [DllImport(Libraries.CoreServices)]
    private static extern nint LSSharedFileListItemCopyResolvedURL(nint item, uint flags, nint error);
}
