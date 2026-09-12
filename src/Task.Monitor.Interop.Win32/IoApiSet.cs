using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class IoApiSet
{
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern unsafe bool DeviceIoControl(
        nint  hDevice,
        uint  dwIoControlCode,
        void* lpInBuffer,
        uint  nInBufferSize,
        void* lpOutBuffer,
        uint  nOutBufferSize,
        uint* lpBytesReturned,
        nint  lpOverlapped);
}
