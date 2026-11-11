using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.WindowsServices;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class ProcessService
{
#if __WIN32__
    private static readonly ConcurrentDictionary<string, string> UserMap = new();

    private static unsafe ProcessSample? CreateProcessSample(
        Kernel32.PROCESSENTRY32W* entry,
        Dictionary<int, int>?     openFailures)
    {
        nint hProcess = Kernel32.OpenProcess(
            Kernel32.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            entry->th32ProcessID);

        if (hProcess == nint.Zero) {
            int error = Marshal.GetLastPInvokeError();

            if (openFailures != null && entry->th32ProcessID != 0) {
                openFailures[error] = openFailures.GetValueOrDefault(error) + 1;
            }

            return null;
        }

        using SafeProcessHandle processHandle = new(hProcess, ownsHandle: true);

        ProcessSample sample = new() {
            Pid = (int)entry->th32ProcessID
        };

        string exeFile = Win32StringHelper.FromNullTerminated(
            new ReadOnlySpan<char>(entry->szExeFile, Kernel32.MAX_PATH));

        sample.ProcessName = exeFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeFile.Substring(0, exeFile.Length - 4)
            : exeFile;

        sample.FileName = GetProcessPath(hProcess);

        sample.FileDescription = GetProcessProductName(
            entry->th32ProcessID,
            sample.FileName,
            sample.ProcessName);

        sample.ModuleName    = Path.GetFileName(exeFile);
        sample.IsDaemon      = WindowsServiceLookup.GetService((int)entry->th32ProcessID, out WindowsServiceInfo? _);
        sample.IsLowPriority = entry->pcPriClassBase < 8;
        sample.UserName      = GetProcessUserName(processHandle);
        sample.CmdLine       = GetProcessCommandLine((int)entry->th32ProcessID, sample.FileName);
        sample.ThreadCount   = (int)entry->cntThreads;
        sample.HandleCount   = 0;
        sample.BasePriority  = entry->pcPriClassBase;

        PsApi.PROCESS_MEMORY_COUNTERS memCounters = new();
        GetProcessMemCounters(hProcess, &memCounters);
        sample.UsedMemory      = (long)memCounters.WorkingSetSize;
        sample.CommittedMemory = (long)memCounters.PagefileUsage;

        GetProcessorTimes(
            hProcess,
            out long kernelTime,
            out long userTime);

        sample.KernelTime = kernelTime;
        sample.UserTime = userTime;

        GetProcessIoOperations(
            hProcess,
            out ulong diskReadBytes,
            out ulong diskWriteBytes);

        sample.DiskReadBytes = diskReadBytes;
        sample.DiskWriteBytes = diskWriteBytes;

        return sample;
    }

    internal static string GetAccountName(SecurityIdentifier sid)
    {
        IdentityReference identityRef;

        try {
            identityRef = sid.Translate(typeof(NTAccount));
        }
        catch (IdentityNotMappedException) {
            TraceEx.WriteLineOnce(nameof(GetAccountName), $"Failed to get account name for {sid}");
            return sid.Value;
        }

        string userName = identityRef.ToString();

        if (UserMap.TryGetValue(userName, out string? name)) {
            return name;
        }

        int domainIndex = userName.IndexOf('\\');

        if (domainIndex != -1) {
            string abbrevUserName = userName.Substring(domainIndex + 1);
            UserMap.TryAdd(userName, abbrevUserName);
        }

        return userName;
    }

    private static string GetProcessCommandLine(int pid, in string defaultValue)
    {
        try {
            if (WindowsServiceLookup.GetService(pid, out WindowsServiceInfo? serviceInfo)) {
                string imagePath = WindowsServiceLookup.GetServiceImagePath(serviceInfo!.ServiceName) ?? defaultValue;
                return ImagePathResolver.Expand(imagePath) ?? defaultValue;
            }

            // TODO: Kernel PEB + Commandline offset.
            return defaultValue;
        }
        catch {
            return defaultValue;
        }
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

    private static unsafe void GetProcessMemCounters(nint hProcess, PsApi.PROCESS_MEMORY_COUNTERS* counters)
    {
        counters->cb = (uint)Marshal.SizeOf<PsApi.PROCESS_MEMORY_COUNTERS>();

        if (!PsApi.GetProcessMemoryInfo(
            hProcess,
            counters,
            counters->cb)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(PsApi.GetProcessMemoryInfo));
        }
    }

    private static unsafe void GetProcessorTimes(
        nint hProcess,
        out long kernelTime,
        out long userTime)
    {
        MinWinBase.FILETIME creationFileTime = new();
        MinWinBase.FILETIME exitFileTime     = new();
        MinWinBase.FILETIME kernelFileTime   = new();
        MinWinBase.FILETIME userFileTime     = new();

        kernelTime = 0;
        userTime   = 0;

        if (!Kernel32.GetProcessTimes(hProcess,
            &creationFileTime,
            &exitFileTime,
            &kernelFileTime,
            &userFileTime)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.GetProcessTimes));
            return;
        }

        kernelTime = kernelFileTime.ToLong();
        userTime   = userFileTime.ToLong();
    }

    private static unsafe string GetProcessPath(nint hProcess, uint flags = Kernel32.PROCESS_NAME_WIN32)
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
        if (WindowsServiceLookup.GetService((int)pid, out WindowsServiceInfo? serviceInfo)) {
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

    private static unsafe ProcessSample? GetProcessSampleInternal(int pid)
    {
        ProcessSample? sample = null;

        using KernelHandleScope snapshot = new(Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0));

        if (snapshot.IsInvalid) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.CreateToolhelp32Snapshot));
            return null;
        }

        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(snapshot, &entry)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.Process32FirstW));
            return null;
        }

        do {
            if (entry.th32ProcessID != (uint)pid) {
                continue;
            }

            sample = CreateProcessSample(&entry, openFailures: null);

            if (sample != null) {
                break;
            }

        } while (Kernel32.Process32NextW(snapshot, &entry));

        return sample;
    }

    private static unsafe List<ProcessSample> GetProcessSamples(Dictionary<int, int>? openFailures = null)
    {
        List<ProcessSample> samples = new();

        using KernelHandleScope snapshot = new(Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0));

        if (snapshot.IsInvalid) {
            PInvokeErrorHelpers.TraceOnLastError(nameof(Kernel32.CreateToolhelp32Snapshot));
            return samples;
        }

        Kernel32.PROCESSENTRY32W entry = new() {
            dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>()
        };

        if (!Kernel32.Process32FirstW(snapshot, &entry)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.Process32FirstW));
            return samples;
        }

        do {
            ProcessSample? sample = CreateProcessSample(&entry, openFailures);

            if (sample != null) {
                samples.Add(sample);
            }

            entry.dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32W>();

        } while (Kernel32.Process32NextW(snapshot, &entry));

        return samples;
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

        using (tokenHandle) {
            return GetProcessTokenSid(tokenHandle, out SecurityIdentifier sid)
                ? sid
                : null;
        }
    }

    private static unsafe bool GetProcessTokenSid(SafeProcessHandle processHandle, out SecurityIdentifier sid)
    {
        bool result = false;
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

    private static string GetProcessUserName(SafeProcessHandle processHandle)
    {
        SecurityIdentifier? sid = GetProcessSecurityIdentifier(processHandle);

        return sid == null
            ? string.Empty
            : GetAccountName(sid);
    }
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
