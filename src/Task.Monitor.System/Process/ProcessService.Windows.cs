using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

#pragma warning disable CA1416

public sealed partial class ProcessService
{
#if __WIN32__
    private static Dictionary<string, string> userMap = new();
    
    private ProcessInfo? CreateProcessInfo(ref Kernel32.PROCESSENTRY32W entry, long gpuTime)
    {
        IntPtr hProcess = Kernel32.OpenProcess(
            Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, 
            bInheritHandle: false, 
            entry.th32ProcessID);

        if (hProcess == IntPtr.Zero) {
            return null;
        }

        SafeProcessHandle processHandle = new(hProcess, ownsHandle: false);

        ProcessInfo processInfo = new() {
            Pid = (int)entry.th32ProcessID
        };

        processInfo.ProcessName = entry.szExeFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
            ? entry.szExeFile.Substring(0, entry.szExeFile.Length - 4) 
            : entry.szExeFile;
        
        processInfo.FileName = GetProcessPath(hProcess);
        
        processInfo.FileDescription = GetProcessProductName(
            entry.th32ProcessID, 
            processInfo.FileName, 
            processInfo.ProcessName);
        
        processInfo.ModuleName = Path.GetFileName(entry.szExeFile);
        processInfo.IsDaemon = ServiceUtils.GetService((int)entry.th32ProcessID, out ServiceController? _);
        processInfo.IsLowPriority = entry.pcPriClassBase < 8;
        processInfo.UserName = GetProcessUserName(processHandle);
        processInfo.CmdLine = GetProcessCommandLine((int)entry.th32ProcessID, processInfo.FileName);
        processInfo.ThreadCount = (int)entry.cntThreads;
        processInfo.HandleCount = 0;
        processInfo.BasePriority = entry.pcPriClassBase;

        PsApi.PROCESS_MEMORY_COUNTERS memCounters = new();
        GetProcessMemCounters(hProcess, ref memCounters);    
        processInfo.UsedMemory = (long)memCounters.WorkingSetSize;

        GetProcessorTimes(
            hProcess,
            out long kernelTime,
            out long userTime);
        
        processInfo.KernelTime = kernelTime;
        processInfo.UserTime = userTime;
        
        processInfo.GpuTime = gpuTime;
        
        GetProcessIoOperations(
            hProcess,
            out ulong diskReadBytes,
            out ulong diskWriteBytes);

        processInfo.DiskReadBytes = diskReadBytes;
        processInfo.DiskWriteBytes = diskWriteBytes;
        processInfo.DiskOperations = diskReadBytes + diskReadBytes;

        Kernel32.CloseHandle(hProcess);
        return processInfo;
    }
    
    private static string GetProcessCommandLine(int pid, in string defaultValue)
    {
        try {
            if (ServiceUtils.GetService(pid, out ServiceController? serviceController)) {
                string imagePath = ServiceUtils.GetServiceImagePath(serviceController!.ServiceName) ?? defaultValue;
                return Environment.ExpandEnvironmentVariables(imagePath);
            }

            // TODO: Kernel PEB + Commandline offset. 
            return defaultValue;
        }
        catch {
            return defaultValue;
        } 
    }

    private ProcessInfo? GetProcessInfoInternal(int pid)
    {
        ProcessInfo? processInfo = null;
        IntPtr hSnapshot = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

        if (hSnapshot == IntPtr.Zero) {
            return null;
        }
        
        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(hSnapshot, ref entry)) {
            return null;
        }

        do {
            if (entry.th32ProcessID != (uint)pid) {
                continue;
            }

            Dictionary<int, long> gpuStats = GpuService.GetProcessStats(); 
            long gpuTime = gpuStats.GetValueOrDefault((int)entry.th32ProcessID, 0);            
            processInfo = CreateProcessInfo(ref entry, gpuTime);

            if (processInfo != null) {
                break;
            }
            
            entry.dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>();

        } while (Kernel32.Process32NextW(hSnapshot, ref entry));
        
        Kernel32.CloseHandle(hSnapshot);
        return processInfo;
    }
    
    private IEnumerable<ProcessInfo> GetProcessInfosInternal()
    {
        Dictionary<int, long> gpuStats = GpuService.GetProcessStats(); 
        IntPtr hSnapshot = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

        if (hSnapshot == IntPtr.Zero) {
            yield break;
        }
        
        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(hSnapshot, ref entry)) {
            yield break;
        }

        do {
            long gpuTime = gpuStats.GetValueOrDefault((int)entry.th32ProcessID, 0);            
            ProcessInfo? processInfo = CreateProcessInfo(ref entry, gpuTime);

            if (processInfo != null) {
                yield return processInfo;
            }
            
            entry.dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>();

        } while (Kernel32.Process32NextW(hSnapshot, ref entry));
        
        Kernel32.CloseHandle(hSnapshot);
    }
    
    private static void GetProcessIoOperations(
        IntPtr hProcess, 
        out ulong readBytes, 
        out ulong writeBytes)
    {
        readBytes = 0;
        writeBytes = 0;

        if (!WinNt.GetProcessIoCounters(hProcess, out WinNt.IO_COUNTERS counters)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(WinNt.GetProcessIoCounters));
            return;
        }

        readBytes = counters.ReadTransferCount; 
        writeBytes = counters.WriteTransferCount;
    }

    private void GetProcessMemCounters(IntPtr hProcess, ref PsApi.PROCESS_MEMORY_COUNTERS counters)
    {
        counters.cb  = (uint)Marshal.SizeOf<PsApi.PROCESS_MEMORY_COUNTERS>();

        if (!PsApi.GetProcessMemoryInfo(
            hProcess,
            ref counters,
            counters.cb)) {
            
            PInvokeErrorHelpers.AssertOnLastError(nameof(PsApi.GetProcessMemoryInfo));
        }
    }

    private void GetProcessorTimes(
        IntPtr hProcess, 
        out long kernelTime, 
        out long userTime)
    {
        kernelTime = 0;
        userTime = 0;
        
        if (!Kernel32.GetProcessTimes(hProcess,
            out MinWinBase.FILETIME creation,
            out MinWinBase.FILETIME exit,
            out MinWinBase.FILETIME kernel,
            out MinWinBase.FILETIME user)) {

            return;
        }

        kernelTime = kernel.ToLong();
        userTime = user.ToLong();
    }
    
    private string GetProcessPath(IntPtr hProcess, uint flags = Kernel32.PROCESS_NAME_WIN32)
    {
        uint size = 1024;
        var sb = new StringBuilder((int)size);

        if (!Kernel32.QueryFullProcessImageNameW(
            hProcess,
            flags,
            sb,
            ref size)) {
            
            return string.Empty;
        }

        return sb.ToString(0, (int)size);
    }
    
    private static string GetProcessProductName(
        uint pid,
        string processPath,
        string defaultValue)
    {
        if (ServiceUtils.GetService((int)pid, out ServiceController? serviceController)) {
            return serviceController?.DisplayName ?? defaultValue;
        }

        if (string.IsNullOrWhiteSpace(processPath)) {
            return defaultValue;
        }

        try {
            SysDiag::FileVersionInfo versionInfo = SysDiag::FileVersionInfo.GetVersionInfo(processPath);
            
            return string.IsNullOrWhiteSpace(versionInfo.FileDescription) 
                ? defaultValue 
                : versionInfo.FileDescription;
        }
        catch {
            return defaultValue;            
        }
    }
    
    private static string GetProcessUserName(SafeProcessHandle processHandle)
    {
        SecurityIdentifier? sid = GetProcessSecurityIdentifier(processHandle);

        if (sid == null) {
            return string.Empty;
        }
        
        IdentityReference identityRef = sid.Translate(typeof(NTAccount));
        string userName = identityRef.ToString();

        if (userMap.TryGetValue(userName, out string? name)) {
            return name;
        }
        
        int domainIndex = userName.IndexOf('\\');
        
        if (domainIndex != -1) {
            string abbrevUserName = userName.Substring(domainIndex + 1);
            userMap.Add(userName, abbrevUserName);
        }
        
        return userName;
    }

    private static SecurityIdentifier? GetProcessSecurityIdentifier(SafeProcessHandle processHandle)
    {
        if (ProcessThreadsApi.OpenProcessToken(
            processHandle,
            0x8u,
            out SafeProcessHandle tokenHandle)) {

            if (GetProcessTokenSid(tokenHandle, out SecurityIdentifier sid)) {
                return sid;
            }
        }

        return null;
    }
    
    private static unsafe bool GetProcessTokenSid(SafeProcessHandle processHandle, out SecurityIdentifier sid)
    {
        var result = false;
        const int BufferLength = 256;
        const int TokenUser = 1;
        
        sid = new SecurityIdentifier(WellKnownSidType.NullSid, null);

        try {
            byte[] buffer = new byte[BufferLength];
            fixed (byte* tokenInfo = &buffer[0]) {
                uint bufLength = BufferLength;

                result = SecurityBaseApi.GetTokenInformation(
                    processHandle,
                    TokenUser,
                    (uint*)tokenInfo,
                    BufferLength,
                    &bufLength);

                if (result) {
                    WinNt.TOKEN_USER* tokenUser = (WinNt.TOKEN_USER*)tokenInfo;
                    
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        uint* sidPtr = tokenUser->sidAndAttributes.Sid;
                        sid = new SecurityIdentifier(new IntPtr(sidPtr));
                    }
                }

                return result;
            }
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
            return result;
        }
    }
#endif
}
#pragma warning restore CA1416
