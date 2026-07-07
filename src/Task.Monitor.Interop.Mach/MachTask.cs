using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class MachTask
{
    public const int KERN_SUCCESS = 0;
    public const int TASK_DYLD_INFO = 17;

    [StructLayout(LayoutKind.Sequential)]
    public struct task_dyld_info
    {
        public ulong all_image_info_addr;
        public ulong all_image_info_size;
        public int   all_image_info_format;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct dyld_all_image_infos_head
    {
        public uint  version;
        public uint  infoArrayCount;
        public ulong infoArray;         // Pointer into the target process.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct dyld_image_info
    {
        public ulong imageLoadAddress;  // Pointer into the target process.
        public ulong imageFilePath;     // Pointer into the target process.
        public ulong imageFileModDate;
    }

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern uint task_self_trap();

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int task_for_pid(uint targetTport, int pid, out uint task);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int task_info(
        uint targetTask, 
        int flavor, 
        ref task_dyld_info taskInfoOut, 
        ref uint taskInfoCount);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern unsafe int mach_vm_read_overwrite(
        uint targetTask, 
        ulong address, 
        ulong size, 
        ulong data, 
        ulong* outSize);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int mach_port_deallocate(uint task, uint name);
}
