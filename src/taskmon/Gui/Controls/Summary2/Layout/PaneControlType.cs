namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Every control a SummaryControl2 pane can host. Empty is a pane with nothing assigned yet -
// only reachable via the layout designer (Milestone 5), modelled now so a tree is always valid
// even mid-edit.
public enum PaneControlType
{
    Empty,
    Cpu,
    Memory,
    Gpu,
    Disk,
    NetworkSent,
    NetworkReceived,
    GpuMemory,
    VirtualMemory,
    Process,
    Drivers,
    Services,
    Startup,
    InstalledApps,
    SystemInfo,
    DiskSpace,
    Thermals,
}
