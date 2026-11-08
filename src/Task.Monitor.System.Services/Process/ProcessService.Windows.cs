using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private static bool GetModulesInternal(SysDiag::Process process, out List<ModuleInfo> moduleInfos)
    {
        moduleInfos = new List<ModuleInfo>();

        foreach (SysDiag::ProcessModule module in process.Modules) {
            ModuleInfo moduleInfo = new() {
                ModuleName = module.ModuleName,
                FileName   = module.FileName
            };

            moduleInfos.Add(moduleInfo);
        }

        return true;
    }
#endif
}
