namespace Task.Monitor.System.Process;

public interface IModuleService
{
    List<ModuleInfo> GetModules(int pid);
}