using System.Runtime.InteropServices;
using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public static class ProcessInfoHelpers
{
    public static void AssertProcessInfoProperties(SysDiag::Process currentProcess, ProcessInfo processInfo)
    {
        Assert.Equal(currentProcess.Id, processInfo.Pid);
        Assert.Equal(currentProcess.ProcessName, processInfo.ProcessName);
        Assert.Equal(currentProcess.MainModule?.ModuleName, processInfo.ModuleName);

        // TODO: File Description and Command Line args.
        Assert.Equal(currentProcess.BasePriority, processInfo.BasePriority);
    }

    public static void AssertProcessInfoProperties(ProcessInfo processInfo)
    {
        Assert.InRange(processInfo.Pid, 0, int.MaxValue);
        
        Assert.NotNull(processInfo.ProcessName);
        Assert.NotEmpty(processInfo.ProcessName);
        
        Assert.NotNull(processInfo.ModuleName);
        Assert.NotEmpty(processInfo.ModuleName);
        
        Assert.NotNull(processInfo.FileName);
        Assert.NotEmpty(processInfo.FileName);
        
        Assert.NotNull(processInfo.FileDescription);
        Assert.NotEmpty(processInfo.FileDescription);
        
        Assert.NotNull(processInfo.CmdLine);
        Assert.NotEmpty(processInfo.CmdLine);
        
        Assert.NotNull(processInfo.UserName);
        Assert.NotEmpty(processInfo.UserName);

        long minBasePriority = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 : -20;
        Assert.InRange(processInfo.BasePriority, minBasePriority, long.MaxValue);
        
        Assert.InRange(processInfo.DiskReadBytes, 0U, ulong.MaxValue);
        Assert.InRange(processInfo.DiskWriteBytes, 0U, ulong.MaxValue);
        Assert.InRange(processInfo.DiskOperations, 0U, ulong.MaxValue);
        Assert.InRange(processInfo.KernelTime, 0, long.MaxValue);
        Assert.InRange(processInfo.ParentPid, 0, int.MaxValue);
        Assert.InRange(processInfo.ThreadCount, 1, int.MaxValue);
        Assert.InRange(processInfo.UsedMemory, 0, long.MaxValue);
        Assert.InRange(processInfo.UserTime, 0, long.MaxValue);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Assert.InRange(processInfo.HandleCount, (uint)0, uint.MaxValue);
        }
    }
}