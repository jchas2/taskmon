namespace Task.Monitor.System.Process;

public interface IThreadService
{
    List<ThreadInfo> GetThreads(int pid);
}