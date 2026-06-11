using Task.Monitor.Cli.Utils;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

public static class ProcessUtils
{
    public static bool EndTask(int pid, int timeOutMilliseconds)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process) || process == null) {
            return false;
        }
        
        process.Kill(entireProcessTree: true);
        bool result = process.WaitForExit(timeOutMilliseconds);
        process.Dispose();
        return result;
    }
    
    internal static bool TryGetProcessByPid(int pid, out SysDiag::Process? process)
    {
        try {
            process = SysDiag::Process.GetProcessById(pid);
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex, $"Failed GetProcessById() for Pid {pid}");
            process = null;
            return false;
        }
    }
}
