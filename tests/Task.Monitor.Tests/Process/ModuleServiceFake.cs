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

    public List<ModuleInfo> GetModules(int pid)
    {
        GetModulesCallCount++;
        RequestedPids.Add(pid);
        return moduleInfos;
    }

    public int GetModulesCallCount { get; private set; }

    public List<int> RequestedPids { get; } = [];
}