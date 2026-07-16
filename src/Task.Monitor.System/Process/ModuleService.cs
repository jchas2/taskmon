using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

public partial class ModuleService : IModuleService
{
    public virtual List<ModuleInfo> GetModules(int pid)
    {
        if (!ProcessUtils.TryGetProcessByPid(pid, out SysDiag::Process? process) ||
            process == null) {
            SysDiag::Trace.WriteLine($"Failed TryGetProcessByPid for Pid {pid} in {nameof(ModuleService)}.");
            return [];
        }
        
        GetModulesInternal(process, out List<ModuleInfo> moduleInfos);
        process.Dispose();
        return moduleInfos;
    }
}