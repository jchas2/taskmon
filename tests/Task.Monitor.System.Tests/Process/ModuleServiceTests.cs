using System.Runtime.Versioning;
using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public sealed class ModuleServiceTests
{
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_Return_Modules_For_Current_Process_Windows() 
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ModuleInfo> modules = new ModuleService().GetModules(currentProcess.Id);
        
        Assert.InRange(modules.Count, 1, int.MaxValue);
    }
    
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_Enumerate_Module_Properties_For_Current_Process()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ModuleInfo> modules = new ModuleService().GetModules(currentProcess.Id);

        foreach (ModuleInfo moduleInfo in modules) {
            Assert.NotNull(moduleInfo.ModuleName);
            Assert.NotEmpty(moduleInfo.ModuleName);

            Assert.NotNull(moduleInfo.FileName);
            Assert.NotEmpty(moduleInfo.FileName);
        }
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Return_Modules_For_Current_Process_MacOS()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ModuleInfo> modules = new ModuleService().GetModules(currentProcess.Id);

        // The test host has the runtime dylibs mapped, so the walk must find some.
        Assert.InRange(modules.Count, 1, int.MaxValue);
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Enumerate_Module_Properties_For_Current_Process_MacOS()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ModuleInfo> modules = new ModuleService().GetModules(currentProcess.Id);

        foreach (ModuleInfo moduleInfo in modules) {
            Assert.NotEmpty(moduleInfo.FileName);
            Assert.NotEmpty(moduleInfo.ModuleName);
            Assert.Equal(Path.GetFileName(moduleInfo.FileName), moduleInfo.ModuleName);
        }

        // The runtime always maps at least one .dylib, so the name filter must fire.
        Assert.Contains(modules, m => m.FileName.EndsWith(".dylib", StringComparison.Ordinal));

        // Paths are deduped.
        Assert.Equal(modules.Select(m => m.FileName).Distinct().Count(), modules.Count);
    }
}