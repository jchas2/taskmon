using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Process;

public sealed class ModuleServiceFake : IModuleService
{
    private readonly List<ModuleInfo> moduleInfos = [];

    public ModuleServiceFake Add(ModuleInfo moduleInfo)
    {
        moduleInfos.Add(moduleInfo);
        return this;
    }

    public List<ModuleInfo> GetModules(int pid) => moduleInfos;
}