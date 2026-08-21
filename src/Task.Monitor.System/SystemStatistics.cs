namespace Task.Monitor.System;

public struct SystemStatistics()
{
    // Memory.
    public ulong AvailablePhysical { get; set; } = 0;
    public ulong AvailablePageFile { get; set; } = 0;
    public ulong AvailableVirtual { get; set; } = 0;
    public ulong TotalPhysical { get; set; } = 0;
    public ulong TotalPageFile { get; set; } = 0;
    public ulong TotalVirtual { get; set; } = 0;

    // Cpu Info.
    public double CpuFrequency { get; set; } = 0;
    public double CpuPerformanceFrequency { get; set; } = 0;
    public double CpuEfficiencyFrequency { get; set; } = 0;
    public double CpuSuperFrequency { get; set; } = 0;
    public ulong CpuCores { get; set; } = 0;
    public ulong CpuPerformanceCores { get; set; } = 0;
    public ulong CpuEfficiencyCores { get; set; } = 0;
    public ulong CpuSuperCores { get; set; } = 0;
    public string CpuName { get; set; } = string.Empty;

    // Cpu % Times.
    public double CpuPercentIdleTime { get; set; } = 0;
    public double CpuPercentKernelTime { get; set; } = 0;
    public double CpuPercentUserTime { get; set; } = 0;
    
    // Gpu.
    public int GpuCores { get; set; } = 0;
    public long TotalGpuMemory { get; set; } = 0;
    public long AvailableGpuMemory { get; set; } = 0;
    public double GpuPercentTime { get; set; } = 0;

    // System.
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    
    // Network.
    public string PublicIPv4Address { get; set; } = string.Empty;
    public string PrivateIPv4Address { get; set; } = string.Empty;
    public ulong TotalNetworkBytesSent { get; set; } = 0;
    public ulong TotalNetworkBytesReceived { get; set; } = 0;
    public ulong TotalNetworkPacketsSent { get; set; } = 0;
    public ulong TotalNetworkPacketsReceived { get; set; } = 0;
    public ulong NetworkBytesSendTime { get; set; } = 0;
    public ulong NetworkBytesReceiveTime { get; set; } = 0;
    public ulong NetworkPacketsSendTime { get; set; } = 0;
    public ulong NetworkPacketsReceiveTime { get; set; } = 0;
    
    // Disk.
    public long TotalDiskReadBytes { get; set; }
    public long TotalDiskWriteBytes { get; set; }
    public long DiskUsage { get; set; } = 0;
    
    // Processes.
    public int ProcessCount { get; set; } = 0;
    public int ThreadCount { get; set; } = 0;
    public int RunningCount { get; set; } = 0;
}
