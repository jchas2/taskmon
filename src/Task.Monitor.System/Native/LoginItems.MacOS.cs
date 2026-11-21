#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class LoginItems
{
    private static readonly Lazy<nint> SessionLoginItems = new(() =>
        NativeExports.ReadPointer(Libraries.CoreServices, LaunchServices.kLSSharedFileListSessionLoginItems));

    private static string? CopyDisplayName(nint item)
    {
        using CFScope name = new(LaunchServices.LSSharedFileListItemCopyDisplayName(item));

        return CFConvert.GetString(name);
    }

    private static string? CopyResolvedPath(nint item)
    {
        // Never prompt or mount: an item on an unmounted volume simply has no path.
        using CFScope url = new(LaunchServices.LSSharedFileListItemCopyResolvedURL(
            item,
            LaunchServices.kLSSharedFileListNoUserInteraction | LaunchServices.kLSSharedFileListDoNotMountVolumes,
            nint.Zero));

        return CFConvert.GetPath(url);
    }

    public static List<LoginItem> GetSessionItems()
    {
        List<LoginItem> items = new();
        nint listType         = SessionLoginItems.Value;

        if (listType == nint.Zero) {
            return items;
        }

        nint listRef;

        try {
            listRef = LaunchServices.LSSharedFileListCreate(nint.Zero, listType, nint.Zero);
        }
        catch (EntryPointNotFoundException) {
            return items;
        }

        using CFScope list = new(listRef);

        if (list.IsNull) {
            return items;
        }

        using CFScope snapshot = new(LaunchServices.LSSharedFileListCopySnapshot(list, out _));

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
}
#endif
