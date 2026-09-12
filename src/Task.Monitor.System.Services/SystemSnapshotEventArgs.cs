namespace Task.Monitor.System.Services;

public sealed class SystemSnapshotEventArgs(SystemSnapshot snapshot) : EventArgs
{
    public SystemSnapshot Snapshot { get; } = snapshot;
}
