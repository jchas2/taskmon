namespace Task.Monitor.System.Services.Memory;

public sealed class MemoryMetrics
{
    public ulong  AvailablePhysical      { get; set; }
    public ulong  AvailablePageFile      { get; set; }
    public ulong  AvailableVirtual       { get; set; }
    public ulong  TotalPhysical          { get; set; }
    public ulong  TotalPageFile          { get; set; }
    public ulong  TotalVirtual           { get; set; }
    public double AvailablePhysicalRatio { get; set; }
    public double AvailablePageFileRatio { get; set; }
    public double AvailableVirtualRatio  { get; set; }
    
    public double CommitLimit            { get; set; }
    public double CommitTotal            { get; set; }
    public double SystemCache            { get; set; }
    public double KernelNonPagedPool     { get; set; }
    public double KernelPagedPool        { get; set; }
    
    public double PageFaultsPerSecond    { get; set; }
    
    public ulong  InUseBytes             { get; set; }
    public ulong  ModifiedBytes          { get; set; }
    public ulong  StandbyBytes           { get; set; }
    public ulong  FreeBytes              { get; set; }
    
    public double  InUseBytesRatio       { get; set; }
    public double  ModifiedBytesRatio    { get; set; }
    public double  StandbyBytesRatio     { get; set; }
    public double  FreeBytesRatio        { get; set; }
}
