using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class Libc
{
    public const ulong ST_RDONLY = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    public struct StatVfs
    {
        public ulong f_bsize;
        public ulong f_frsize;
        public uint  f_blocks;
        public uint  f_bfree;
        public uint  f_bavail;
        public uint  f_files;
        public uint  f_ffree;
        public uint  f_favail;
        public ulong f_fsid;
        public ulong f_flag;
        public ulong f_namemax;
    }

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern int chown(string path, int owner, int group);

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern int statvfs(string path, out StatVfs buffer);
}
