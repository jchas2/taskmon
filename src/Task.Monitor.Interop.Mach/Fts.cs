using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// fts(3): walks a file hierarchy natively, for walks where .NET would allocate an object per file -
// summing a large app bundle visits hundreds of thousands of them. The layouts are the arm64 ones
// (64-bit inodes, plain fts_* symbols); Intel Macs aren't supported.
public static unsafe class Fts
{
    public const int    FTS_NOCHDIR  = 0x004;   /* don't change directories */
    public const int    FTS_PHYSICAL = 0x010;   /* physical walk: lstat, don't follow symbolic links */
    public const ushort FTS_F        = 8;       /* regular file */

    [StructLayout(LayoutKind.Sequential)]
    public struct Timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    // struct stat (sys/stat.h).
    [StructLayout(LayoutKind.Sequential)]
    public struct Stat
    {
        public int      st_dev;
        public ushort   st_mode;
        public ushort   st_nlink;
        public ulong    st_ino;
        public uint     st_uid;
        public uint     st_gid;
        public int      st_rdev;
        public Timespec st_atimespec;
        public Timespec st_mtimespec;
        public Timespec st_ctimespec;
        public Timespec st_birthtimespec;
        public long     st_size;
        public long     st_blocks;
        public int      st_blksize;
        public uint     st_flags;
        public uint     st_gen;
        public int      st_lspare;
        public long     st_qspare0;
        public long     st_qspare1;
    }

    // FTSENT (fts.h), without the variable-length fts_name that follows it.
    [StructLayout(LayoutKind.Sequential)]
    public struct FTSENT
    {
        public FTSENT* fts_cycle;
        public FTSENT* fts_parent;
        public FTSENT* fts_link;
        public nint    fts_number;
        public void*   fts_pointer;
        public byte*   fts_accpath;
        public byte*   fts_path;
        public int     fts_errno;
        public int     fts_symfd;
        public ushort  fts_pathlen;
        public ushort  fts_namelen;
        public ulong   fts_ino;
        public int     fts_dev;
        public ushort  fts_nlink;
        public short   fts_level;
        public ushort  fts_info;
        public ushort  fts_flags;
        public ushort  fts_instr;
        public Stat*   fts_statp;
    }

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern int fts_close(nint ftsp);

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern nint fts_open(byte** path_argv, int options, nint compar);

    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern FTSENT* fts_read(nint ftsp);
}
