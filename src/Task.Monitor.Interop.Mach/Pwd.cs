using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class Pwd
{
    public unsafe struct Passwd
    {
        public const int InitialBufferSize = 256;

        public byte* Name;
        public byte* Password;
        public uint  UserId;
        public uint  GroupId;
        public byte* UserInfo;
        public byte* HomeDirectory;
        public byte* Shell;
    }

    [DllImport(Libraries.LibSystemNative, EntryPoint = "SystemNative_GetPwUidR", SetLastError = false)]
    public static extern unsafe int GetPwUidR(uint uid, out Passwd pwd, byte* buf, int bufLen);
}