#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class FileDates
{
    public static unsafe DateTime? GetAddedTime(string path)
    {
        Attr.AttrList request = new() {
            bitmapcount = Attr.ATTR_BIT_MAP_COUNT,
            commonattr  = Attr.ATTR_CMN_RETURNED_ATTRS | Attr.ATTR_CMN_ADDEDTIME
        };

        Attr.AddedTimeBuffer buffer = default;

        int result = Attr.getattrlist(
            path,
            ref request,
            &buffer,
            (nuint)sizeof(Attr.AddedTimeBuffer),
            Attr.FSOPT_NOFOLLOW | Attr.FSOPT_PACK_INVAL_ATTRS);

        if (result != 0 || (buffer.returnedCommonAttr & Attr.ATTR_CMN_ADDEDTIME) == 0 || buffer.tv_sec <= 0) {
            return null;
        }

        return DateTime.UnixEpoch.AddSeconds(buffer.tv_sec);
    }
}
#endif
