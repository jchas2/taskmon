using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Task.Monitor.Interop.Win32;

public static class ProcessThreadsApi
{
    // Firmware (BIOS/UEFI) has hardware virtualisation enabled. Note a running hypervisor
    // (Hyper-V, VBS, WSL2) can claim the feature and make this report false.
    public const uint PF_VIRT_FIRMWARE_ENABLED = 21;

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool GetSystemTimes(
        MinWinBase.FILETIME* lpIdleTime,
        MinWinBase.FILETIME* lpKernelTime,
        MinWinBase.FILETIME* lpUserTime);

    [DllImport(Libraries.Kernel32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessorFeaturePresent(uint processorFeature);
    
    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        uint desiredAccess,
        out SafeProcessHandle tokenHandle);
}
