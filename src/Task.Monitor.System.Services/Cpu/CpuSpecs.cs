namespace Task.Monitor.System.Services.Cpu;

public struct CpuSpecs
{
    public double CpuFrequency            { get; set; }
    public double CpuPerformanceFrequency { get; set; }
    public double CpuEfficiencyFrequency  { get; set; }
    public double CpuSuperFrequency       { get; set; }

    public ulong  CpuCores                { get; set; }
    public ulong  CpuPerformanceCores     { get; set; }
    public ulong  CpuEfficiencyCores      { get; set; }
    public ulong  CpuSuperCores           { get; set; }

    public uint   CpuSockets              { get; set; }

    public ulong  CpuL1CacheBytes         { get; set; }
    public ulong  CpuL2CacheBytes         { get; set; }
    public ulong  CpuL3CacheBytes         { get; set; }

    public bool   CpuVirtualizationFirmwareEnabled { get; set; }

    public string CpuName                 { get; set; }
}
