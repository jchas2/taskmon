using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

public partial class ModuleService
{
#if __WIN32__
    private bool GetModulesInternal(SysDiag::Process process, out List<ModuleInfo> moduleInfos)
    {
        moduleInfos = new List<ModuleInfo>();

        foreach (SysDiag::ProcessModule module in process.Modules) {
            ModuleInfo moduleInfo = new() {
                ModuleName = module.ModuleName,
                FileName = module.FileName
            };
            
            moduleInfos.Add(moduleInfo);
        }
        
        return true;
    }
#endif
}