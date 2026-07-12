using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class Libc
{
    [DllImport(Libraries.LibC, SetLastError = true)]
    public static extern int chown(string path, int owner, int group);    
}
