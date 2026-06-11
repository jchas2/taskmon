namespace Task.Monitor.System.Process;

public struct ProcessTimeInfo
{
    public long KernelTime;
    public long UserTime;
    public long DiskOperations;

    public void Clear()
    {
        KernelTime = 0;
        UserTime = 0;
        DiskOperations = 0;
    }
}
