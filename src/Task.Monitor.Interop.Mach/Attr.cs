using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// getattrlist(2): file attributes stat doesn't expose, such as the time an item was added to its
// folder - what Finder shows as "Date Added".
public static unsafe class Attr
{
    public const ushort ATTR_BIT_MAP_COUNT      = 5;
    public const uint   ATTR_CMN_ADDEDTIME      = 0x10000000;
    public const uint   ATTR_CMN_RETURNED_ATTRS = 0x80000000;
    public const uint   FSOPT_NOFOLLOW          = 0x00000001;
    public const uint   FSOPT_PACK_INVAL_ATTRS  = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    public struct AttrList
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
    public struct AddedTimeBuffer
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

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern int getattrlist(
        string       path,
        ref AttrList attrList,
        void*        attrBuf,
        nuint        attrBufSize,
        uint         options);
}
