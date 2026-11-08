using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __APPLE__
    private const int    KernelTaskPid  = 0;
    private const string VmmapPath      = "/usr/bin/vmmap";
    private const int    VmmapTimeoutMs = 3000;

    private static string? ExtractTrailingPath(string line)
    {
        int index = line.IndexOf(" /", StringComparison.Ordinal);
        return index < 0 ? null : line[(index + 1)..].Trim();
    }

    private static bool GetCurrentProcessModules(List<ModuleInfo> moduleInfos)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        uint count = DyLib._dyld_image_count();

        for (uint i = 0; i < count; i++) {
            IntPtr namePtr = DyLib._dyld_get_image_name(i);

            if (namePtr == IntPtr.Zero) {
                continue;
            }

            string path = Marshal.PtrToStringUTF8(namePtr) ?? string.Empty;

            if (path.Length > 0 && seen.Add(path)) {
                moduleInfos.Add(new ModuleInfo {
                    FileName = path,
                    ModuleName = Path.GetFileName(path)
                });
            }
        }

        return moduleInfos.Count > 0;
    }

    private static unsafe bool GetMappedFileModules(int pid, List<ModuleInfo> moduleInfos)
    {
        // Walk the process mapped VM regions via proc_pidinfo, reporting each region's backing
        // file path. Match on name or VM_MEMORY_DYLIB tag.
        HashSet<string> seen = new(StringComparer.Ordinal);

        int size = sizeof(ProcInfo.proc_regionwithpathinfo);
        ulong address = 0;

        while (true) {
            ProcInfo.proc_regionwithpathinfo info;

            int result = ProcInfo.proc_pidinfo(
                pid,
                ProcInfo.PROC_PIDREGIONPATHINFO,
                address,
                &info,
                size);

            if (result <= 0) {
                break;
            }

            string path = Marshal.PtrToStringUTF8((IntPtr)info.prp_vip_path) ?? string.Empty;

            if (path.Length > 0 && IsLoadedImage(path, info.prp_prinfo.pri_user_tag) && seen.Add(path)) {
                moduleInfos.Add(new ModuleInfo {
                    FileName = path,
                    ModuleName = Path.GetFileName(path)
                });
            }

            ulong next = info.prp_prinfo.pri_address + info.prp_prinfo.pri_size;

            // Stop if the walk fails to advance (or wraps) to avoid an infinite loop.
            if (next <= address) {
                break;
            }

            address = next;
        }

        return moduleInfos.Count > 0;
    }

    // Initial path for process modules as the call to task_for_pid can't open SIP-protected / hardened apps.
    // Shell out to Apple's vmmap, which is signed with com.apple.system-task-ports.
    private static bool GetModulesFromVmmap(int pid, List<ModuleInfo> moduleInfos)
    {
        try {
            using SysDiag::Process proc = new() {
                StartInfo = new SysDiag::ProcessStartInfo {
                FileName = VmmapPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true }
            };

            proc.StartInfo.ArgumentList.Add(pid.ToString());

            SysDiag::Trace.WriteLine($"Spawn {VmmapPath} {pid}");

            if (!proc.Start()) {
                SysDiag::Trace.WriteLine($"Failed to start {VmmapPath}");
                return false;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);

            // Each loaded Mach-O image has a "__TEXT" (code) region row in vmmap's output
            // whose trailing token is the backing file path.
            for (string? line = proc.StandardOutput.ReadLine(); line is not null; line = proc.StandardOutput.ReadLine()) {
                if (!line.StartsWith("__TEXT", StringComparison.Ordinal)) {
                    continue;
                }

                string? path = ExtractTrailingPath(line);

                if (!string.IsNullOrEmpty(path) && seen.Add(path)) {
                    moduleInfos.Add(new ModuleInfo {
                        FileName = path,
                        ModuleName = Path.GetFileName(path)
                    });
                }
            }

            // Drain stderr so vmmap can't block on a full pipe.
            _ = proc.StandardError.ReadToEnd();

            if (!proc.WaitForExit(VmmapTimeoutMs)) {
                SysDiag::Trace.WriteLine($"Timeout of {VmmapTimeoutMs} expired for {VmmapPath}.");
                proc.Kill(entireProcessTree: true);
            }

            SysDiag::Trace.WriteLine($"Proc {VmmapPath} with Pid {proc.Id} exited with {proc.ExitCode}.");

            return moduleInfos.Count > 0;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"An Exception occurred processing {VmmapPath}");
            return false;
        }
    }

    private static bool GetModulesInternal(SysDiag::Process process, out List<ModuleInfo> moduleInfos)
    {
        moduleInfos = new List<ModuleInfo>();

        // macOS rejects this read of kernel_task, even as root, and logs a crash report for it.
        if (process.Id == KernelTaskPid) {
            return false;
        }

        if (process.Id == Environment.ProcessId) {
            return GetCurrentProcessModules(moduleInfos);
        }

        // This is nasty. SIP on MacOS protects calls via task_for_pid unless our process is signed with
        // com.apple.system-task-ports.read (so we can see shared system dylibs).
        // We initially shell out to vmmap which is signed and can return what we're looking for.
        // Failing that, we fall back to what we can see via task_for_pid. Failing that, we fallback
        // to proc_pidinfo for a region walk which only shows file-backed non-shared cached libs.
        return GetModulesFromVmmap (process.Id, moduleInfos)
            || TryGetDyldModules(process.Id, moduleInfos)
            || GetMappedFileModules(process.Id, moduleInfos);
    }

    private static bool IsLoadedImage(string path, uint userTag) =>
        path.EndsWith(".dylib", StringComparison.Ordinal) ||
        userTag == ProcInfo.VM_MEMORY_DYLIB;

    private static unsafe bool ReadTaskMemory(uint task, ulong address, void* buffer, ulong size)
    {
        ulong outSize;

        int result = MachTask.mach_vm_read_overwrite(
            task,
            address,
            size,
            (ulong)buffer,
            &outSize);

        return result == MachTask.KERN_SUCCESS && outSize == size;
    }

    // Reads a NUL-terminated UTF-8 path from the target, bounded to the current
    // page so the read can't run off into an unmapped page.
    private static unsafe string ReadTaskString(uint task, ulong address)
    {
        int pageSize = Environment.SystemPageSize;
        ulong toPageEnd = (ulong)pageSize - (address & (ulong)(pageSize - 1));
        int length = (int)Math.Min((ulong)ProcInfo.MAXPATHLEN, toPageEnd);

        Span<byte> buffer = stackalloc byte[ProcInfo.MAXPATHLEN];
        Span<byte> slice = buffer[..length];

        fixed (byte* p = slice) {
            if (!ReadTaskMemory(task, address, p, (ulong)length)) {
                return string.Empty;
            }
        }

        int nul = slice.IndexOf((byte)0);

        return Encoding.UTF8.GetString(nul < 0
            ? slice
            : slice[..nul]);
    }

    private static unsafe bool TryGetDyldModules(int pid, List<ModuleInfo> moduleInfos)
    {
        if (MachTask.task_for_pid(MachTask.task_self_trap(), pid, out uint task) != MachTask.KERN_SUCCESS) {
            SysDiag::Trace.WriteLine($"Failed MachTask.task_for_pid {pid} in {nameof(ProcessService)}.");
            return false;
        }

        try {
            MachTask.task_dyld_info dyldInfo = default;
            uint count = (uint)(sizeof(MachTask.task_dyld_info) / sizeof(uint));

            if (MachTask.task_info(
                task,
                MachTask.TASK_DYLD_INFO,
                ref dyldInfo,
                ref count) != MachTask.KERN_SUCCESS ||
                dyldInfo.all_image_info_addr == 0) {
                SysDiag::Trace.WriteLine($"Failed MachTask.task_info for pid {pid} in {nameof(ProcessService)}.");
                return false;
            }

            MachTask.dyld_all_image_infos_head head;

            if (!ReadTaskMemory(
                task,
                dyldInfo.all_image_info_addr,
                &head,
                (ulong)sizeof(MachTask.dyld_all_image_infos_head)) ||
                head.infoArrayCount == 0 || head.infoArray == 0) {
                SysDiag::Trace.WriteLine($"Failed ReadTaskMemory for pid {pid} in {nameof(ProcessService)}.");
                return false;
            }

            var images = new MachTask.dyld_image_info[head.infoArrayCount];

            fixed (MachTask.dyld_image_info* imagePtr = images) {
                if (!ReadTaskMemory(
                    task,
                    head.infoArray,
                    imagePtr,
                    (ulong)(images.Length * sizeof(MachTask.dyld_image_info)))) {
                    SysDiag::Trace.WriteLine($"Failed ReadTaskMemory for dylib_image_info for pid {pid} in {nameof(ProcessService)}.");
                    return false;
                }
            }

            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (MachTask.dyld_image_info image in images) {
                if (image.imageFilePath == 0) {
                    continue;
                }

                string path = ReadTaskString(task, image.imageFilePath);

                if (path.Length > 0 && seen.Add(path)) {
                    moduleInfos.Add(new ModuleInfo {
                        FileName = path,
                        ModuleName = Path.GetFileName(path)
                    });
                }
            }

            return moduleInfos.Count > 0;
        }
        finally {
            MachTask.mach_port_deallocate(MachTask.task_self_trap(), task);
        }
    }
#endif
}
