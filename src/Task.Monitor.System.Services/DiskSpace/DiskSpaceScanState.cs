namespace Task.Monitor.System.Services.DiskSpace;

public enum DiskSpaceScanState
{
    Idle,
    Scanning,
    Cancelling,
    Completed,
    Cancelled,
    Faulted
}
