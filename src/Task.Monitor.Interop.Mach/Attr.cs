using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// getattrlist(2): file attributes stat doesn't expose, such as the time an item was added to its
// folder - what Finder shows as "Date Added".
public static unsafe class Attr
{
    private const ushort ATTR_BIT_MAP_COUNT      = 5;
    private const uint   ATTR_CMN_ADDEDTIME      = 0x10000000;
    private const uint   ATTR_CMN_RETURNED_ATTRS = 0x80000000;
    private const uint   FSOPT_NOFOLLOW          = 0x00000001;
    private const uint   FSOPT_PACK_INVAL_ATTRS  = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct AttrList
    {
        public ushort bitmapcount;
        public ushort reserved;
        public uint   commonattr;
        public uint   volattr;
        public uint   dirattr;
        public uint   fileattr;
        public uint   forkattr;
    }

    // The buffer getattrlist fills for ATTR_CMN_RETURNED_ATTRS | ATTR_CMN_ADDEDTIME: the length,
    // the attribute_set_t of attributes actually returned, then the added time as a timespec.
    [StructLayout(LayoutKind.Sequential)]
    private struct AddedTimeBuffer
    {
        public uint length;
        public uint returnedCommonAttr;
        public uint returnedVolAttr;
        public uint returnedDirAttr;
        public uint returnedFileAttr;
        public uint returnedForkAttr;
        public long tv_sec;
        public long tv_nsec;
    }

    // Null when the file system doesn't record an added time for the item.
    public static DateTime? GetAddedTime(string path)
    {
        AttrList request = new() {
            bitmapcount = ATTR_BIT_MAP_COUNT,
            commonattr  = ATTR_CMN_RETURNED_ATTRS | ATTR_CMN_ADDEDTIME
        };

        AddedTimeBuffer buffer = default;

        int result = getattrlist(
            path,
            ref request,
            &buffer,
            (nuint)sizeof(AddedTimeBuffer),
            FSOPT_NOFOLLOW | FSOPT_PACK_INVAL_ATTRS);

        if (result != 0 || (buffer.returnedCommonAttr & ATTR_CMN_ADDEDTIME) == 0 || buffer.tv_sec <= 0) {
            return null;
        }

        return DateTime.UnixEpoch.AddSeconds(buffer.tv_sec);
    }

    [DllImport(Libraries.LibC, SetLastError = true)]
    private static extern int getattrlist(
        string       path,
        ref AttrList attrList,
        void*        attrBuf,
        nuint        attrBufSize,
        uint         options);
}
