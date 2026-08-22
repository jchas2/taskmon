using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
    
    private unsafe ProcessInfo? CreateProcessInfo(Kernel32.PROCESSENTRY32W* entry, long gpuTime)
    {
        nint hProcess = Kernel32.OpenProcess(
            Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, 
            bInheritHandle: false, 
            entry->th32ProcessID);

        if (hProcess == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(Kernel32.OpenProcess)}_{entry->th32ProcessID}",
                $"Failed to open process for pid {entry->th32ProcessID}");
            
            return null;
        }

        SafeProcessHandle processHandle = new(hProcess, ownsHandle: false);

        ProcessInfo processInfo = new() {
            Pid = (int)entry->th32ProcessID
        };

        string exeFile = new string(entry->szExeFile);
        
        processInfo.ProcessName = exeFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
            ? exeFile.Substring(0, exeFile.Length - 4) 
            : exeFile;
        
        processInfo.FileName = GetProcessPath(hProcess);
        
        processInfo.FileDescription = GetProcessProductName(
            entry->th32ProcessID, 
            processInfo.FileName, 
            processInfo.ProcessName);
        
        processInfo.ModuleName = Path.GetFileName(exeFile);
        processInfo.IsDaemon = ServiceUtils.GetService((int)entry->th32ProcessID, out ServiceInfo? _);
        processInfo.IsLowPriority = entry->pcPriClassBase < 8;
        processInfo.UserName = GetProcessUserName(processHandle);
        processInfo.CmdLine = GetProcessCommandLine((int)entry->th32ProcessID, processInfo.FileName);
        processInfo.ThreadCount = (int)entry->cntThreads;
        processInfo.HandleCount = 0;
        processInfo.BasePriority = entry->pcPriClassBase;

        PsApi.PROCESS_MEMORY_COUNTERS memCounters = new();
        GetProcessMemCounters(hProcess, &memCounters);    
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
            if (ServiceUtils.GetService(pid, out ServiceInfo? serviceInfo)) {
                string imagePath = ServiceUtils.GetServiceImagePath(serviceInfo!.ServiceName) ?? defaultValue;
                return Environment.ExpandEnvironmentVariables(imagePath);
            }

            // TODO: Kernel PEB + Commandline offset. 
            return defaultValue;
        }
        catch {
            return defaultValue;
        } 
    }

    private unsafe ProcessInfo? GetProcessInfoInternal(int pid)
    {
        ProcessInfo? processInfo = null;
        nint hSnapshot = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

        if (hSnapshot == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.CreateToolhelp32Snapshot));
            return null;
        }
        
        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(hSnapshot, &entry)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.Process32FirstW));
            return null;
        }

        do {
            if (entry.th32ProcessID != (uint)pid) {
                continue;
            }

            Dictionary<int, long> gpuStats = GpuService.GetProcessStats(); 
            long gpuTime = gpuStats.GetValueOrDefault((int)entry.th32ProcessID, 0);            
            processInfo = CreateProcessInfo(&entry, gpuTime);

            if (processInfo != null) {
                break;
            }
            
            entry.dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>();

        } while (Kernel32.Process32NextW(hSnapshot, &entry));
        
        Kernel32.CloseHandle(hSnapshot);
        return processInfo;
    }
    
    private unsafe List<ProcessInfo> GetProcessInfosInternal()
    {
        List<ProcessInfo> processInfos = new();
        Dictionary<int, long> gpuStats = GpuService.GetProcessStats(); 
        nint hSnapshot = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

        if (hSnapshot == nint.Zero) {
            PInvokeErrorHelpers.TraceOnLastError(nameof(Kernel32.CreateToolhelp32Snapshot));
            return processInfos; 
        }
        
        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(hSnapshot, &entry)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.Process32FirstW));
            Kernel32.CloseHandle(hSnapshot);
            return processInfos;
        }

        do {
            long gpuTime = gpuStats.GetValueOrDefault((int)entry.th32ProcessID, 0);            
            ProcessInfo? processInfo = CreateProcessInfo(&entry, gpuTime);

            if (processInfo != null) {
                processInfos.Add(processInfo);
            }
            
            entry.dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>();

        } while (Kernel32.Process32NextW(hSnapshot, &entry));
        
        Kernel32.CloseHandle(hSnapshot);
        return processInfos;
    }
    
    private static unsafe void GetProcessIoOperations(
        nint hProcess, 
        out ulong readBytes, 
        out ulong writeBytes)
    {
        readBytes = 0;
        writeBytes = 0;

        WinNt.IO_COUNTERS counters = new();
        
        if (!WinNt.GetProcessIoCounters(hProcess, &counters)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(WinNt.GetProcessIoCounters));
            return;
        }

        readBytes = counters.ReadTransferCount; 
        writeBytes = counters.WriteTransferCount;
    }

    private unsafe void GetProcessMemCounters(nint hProcess, PsApi.PROCESS_MEMORY_COUNTERS* counters)
    {
        counters->cb = (uint)Marshal.SizeOf<PsApi.PROCESS_MEMORY_COUNTERS>();

        if (!PsApi.GetProcessMemoryInfo(
            hProcess,
            counters,
            counters->cb)) {
            
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(PsApi.GetProcessMemoryInfo));
        }
    }

    private unsafe void GetProcessorTimes(
        nint hProcess, 
        out long kernelTime, 
        out long userTime)
    {
        MinWinBase.FILETIME creationFileTime = new();
        MinWinBase.FILETIME exitFileTime = new();
        MinWinBase.FILETIME kernelFileTime = new();
        MinWinBase.FILETIME userFileTime = new();
        
        kernelTime = 0;
        userTime = 0;
        
        if (!Kernel32.GetProcessTimes(hProcess,
            &creationFileTime,
            &exitFileTime,
            &kernelFileTime,
            &userFileTime)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.GetProcessTimes));
            return;
        }
        
        kernelTime = kernelFileTime.ToLong();
        userTime = userFileTime.ToLong();
    }
    
    private unsafe string GetProcessPath(nint hProcess, uint flags = Kernel32.PROCESS_NAME_WIN32)
    {
        uint size = 1024;
        Span<char> buffer = stackalloc char[(int)size];
        
        fixed (char* pBuffer = &MemoryMarshal.GetReference(buffer)) {
            if (!Kernel32.QueryFullProcessImageNameW(
                hProcess,
                flags,
                pBuffer,
                &size)) {

                PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.QueryFullProcessImageNameW));
                return string.Empty;
            }

            return buffer.Slice(0, (int)size).ToString();
        }
    }
    
    private static string GetProcessProductName(
        uint pid,
        string processPath,
        string defaultValue)
    {
        if (ServiceUtils.GetService((int)pid, out ServiceInfo? serviceInfo)) {
            return serviceInfo?.DisplayName ?? defaultValue;
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
        if (!ProcessThreadsApi.OpenProcessToken(
            processHandle,
            0x8u,
            out SafeProcessHandle tokenHandle)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(ProcessThreadsApi.OpenProcessToken));
            return null;
        }

        if (GetProcessTokenSid(tokenHandle, out SecurityIdentifier sid)) {
            return sid;
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
                        sid = new SecurityIdentifier(new nint(sidPtr));
                    }
                }
                else {
                    PInvokeErrorHelpers.TraceOnceOnLastError(nameof(GetProcessTokenSid));
                }

                return result;
            }
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return result;
        }
    }
#endif
}
#pragma warning restore CA1416
