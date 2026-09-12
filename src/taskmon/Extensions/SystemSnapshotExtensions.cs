using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Extensions;

public static class SystemSnapshotExtensions
{
    public static string ToCpuKernelUserPercentage(this CpuMetrics metrics)
    {
        double totalCpu = metrics.CpuPercentKernelTime + metrics.CpuPercentUserTime;
        return $"CPU {totalCpu:000.0%} Kernel {metrics.CpuPercentKernelTime:000.0%} User {metrics.CpuPercentUserTime:000.0%}";
    }
    
    public static string ToCpuPercentage(this CpuMetrics metrics)
    {
        double totalCpu = metrics.CpuPercentKernelTime + metrics.CpuPercentUserTime;
        return $"{totalCpu:000.0%}";
    }

    public static string ToCpuFrequencyGhz(this CpuSpecs specs) =>
        $"{specs.CpuFrequency / 1000.0:0.00} GHz";

    public static string ToCpuSocketCount(this CpuSpecs specs) =>
        specs.CpuSockets == 0 ? "1" : specs.CpuSockets.ToString();

    // Firmware bit only; a running hypervisor can hide it. See ProcessThreadsApi.PF_VIRT_FIRMWARE_ENABLED.
    public static string ToCpuVirtualization(this CpuSpecs specs) =>
        specs.CpuVirtualizationFirmwareEnabled ? "Enabled" : "Disabled";

    public static string ToCpuL1Cache(this CpuSpecs specs) => specs.CpuL1CacheBytes.ToFormattedByteSize();
    public static string ToCpuL2Cache(this CpuSpecs specs) => specs.CpuL2CacheBytes.ToFormattedByteSize();
    public static string ToCpuL3Cache(this CpuSpecs specs) => specs.CpuL3CacheBytes.ToFormattedByteSize();

    public static string ToMemoryCapacity(this MemoryDevice memoryDevice) =>
        $"{memoryDevice.SizeInMegabytes / 1024.0:F2} GB";

    public static string ToGpuMemoryCapacity(this GpuSpecs specs) =>
        specs.TotalGpuMemory.ToFormattedByteSize();
    
    public static string ToGpuMemoryAvailableFormattedBytes(this GpuMetrics metrics) =>
        metrics.AvailableGpuMemory.ToFormattedByteSize();
    
    public static double ToGpuMemoryRatio(this GpuMetrics metrics) =>
        metrics.TotalGpuMemory > 0
            ? 1.0 - (double)(metrics.AvailableGpuMemory) / (double)(metrics.TotalGpuMemory)
            : 0.0;

    public static string ToGpuMemoryPercentage(this GpuMetrics metrics) =>
        $"{metrics.ToGpuMemoryRatio():000.0%}";
    
    public static string ToGpuMemoryRatioFormattedBytes(this GpuMetrics metrics) =>
        (metrics.TotalGpuMemory - metrics.AvailableGpuMemory).ToFormattedByteSize() + "/" +
         metrics.TotalGpuMemory.ToFormattedByteSize();

    public static string ToSharedGpuMemoryCapacity(this GpuMetrics metrics) =>
        metrics.TotalSharedGpuMemory.ToFormattedByteSize();

    public static string ToSharedGpuMemoryAvailableFormattedBytes(this GpuMetrics metrics) =>
        metrics.AvailableSharedGpuMemory.ToFormattedByteSize();

    public static double ToSharedGpuMemoryRatio(this GpuMetrics metrics) =>
        metrics.TotalSharedGpuMemory > 0
            ? 1.0 - (double)(metrics.AvailableSharedGpuMemory) / (double)(metrics.TotalSharedGpuMemory)
            : 0.0;

    public static string ToSharedGpuMemoryPercentage(this GpuMetrics metrics) =>
        $"{metrics.ToSharedGpuMemoryRatio():000.0%}";

    public static string ToSharedGpuMemoryRatioFormattedBytes(this GpuMetrics metrics) =>
        (metrics.TotalSharedGpuMemory - metrics.AvailableSharedGpuMemory).ToFormattedByteSize() + "/" +
         metrics.TotalSharedGpuMemory.ToFormattedByteSize();

    public static string ToCombinedGpuMemoryCapacity(this GpuMetrics metrics) =>
        metrics.TotalCombinedGpuMemory.ToFormattedByteSize();

    public static string ToCombinedGpuMemoryAvailableFormattedBytes(this GpuMetrics metrics) =>
        metrics.AvailableCombinedGpuMemory.ToFormattedByteSize();

    public static double ToCombinedGpuMemoryRatio(this GpuMetrics metrics) =>
        metrics.TotalCombinedGpuMemory > 0
            ? 1.0 - (double)(metrics.AvailableCombinedGpuMemory) / (double)(metrics.TotalCombinedGpuMemory)
            : 0.0;

    public static string ToCombinedGpuMemoryPercentage(this GpuMetrics metrics) =>
        $"{metrics.ToCombinedGpuMemoryRatio():000.0%}";

    public static string ToCombinedGpuMemoryRatioFormattedBytes(this GpuMetrics metrics) =>
        (metrics.TotalCombinedGpuMemory - metrics.AvailableCombinedGpuMemory).ToFormattedByteSize() + "/" +
         metrics.TotalCombinedGpuMemory.ToFormattedByteSize();

    public static string ToGpuPercentage(this GpuMetrics metrics) =>
        $"{metrics.GpuPercentTime:000.0%}";
    
    public static string ToMemoryConfiguredSpeed(this ref ushort speedInMhz) =>
        $"{speedInMhz} MT/s";
    
    public static string ToMemorySpeed(this ref ushort speedInMhz) =>
        $"{speedInMhz} MT/s";
    
    public static string ToMemoryCapacity(this ref uint sizeInMegabytes) =>
        $"{sizeInMegabytes / 1024.0:F2} GB";
    
    public static string ToMemoryAvailableFormattedBytes(this MemoryMetrics metrics) =>
        metrics.AvailablePhysical.ToFormattedByteSize();
    
    public static double ToMemoryRatio(this MemoryMetrics metrics) =>
        metrics.TotalPhysical > 0
            ? 1.0 - (double)(metrics.AvailablePhysical) / (double)(metrics.TotalPhysical)
            : 0.0;

    public static string ToMemoryPercentage(this MemoryMetrics metrics) =>
        $"{metrics.ToMemoryRatio():000.0%}";
    
    public static string ToMemoryRatioFormattedBytes(this MemoryMetrics metrics) =>
        (metrics.TotalPhysical - metrics.AvailablePhysical).ToFormattedByteSize() + "/" +
         metrics.TotalPhysical.ToFormattedByteSize();

    public static string ToPageFileMemoryAvailableFormattedBytes(this MemoryMetrics metrics) =>
        metrics.AvailablePageFile.ToFormattedByteSize();

    public static double ToPageFileMemoryRatio(this MemoryMetrics metrics) =>
        metrics.TotalPageFile > 0
            ? 1.0 - (double)(metrics.AvailablePageFile) / (double)(metrics.TotalPageFile)
            : 0.0;

    public static string ToPageFileMemoryPercentage(this MemoryMetrics metrics) =>
        $"{metrics.ToPageFileMemoryRatio():000.0%}";
    
    public static string ToPageFileMemoryRatioFormattedBytes(this MemoryMetrics metrics) =>
        (metrics.TotalPageFile - metrics.AvailablePageFile).ToFormattedByteSize() + "/" +
         metrics.TotalPageFile.ToFormattedByteSize();

    // Every disk figure below is already combined across all installed drives: the service takes
    // active time from the busiest disk and the byte rates from the Pdh "_Total" instance.
    public static double ToDiskActiveTimeRatio(this DiskMetrics metrics) =>
        Math.Clamp(metrics.PercentActiveTime / 100.0, 0.0, 1.0);

    public static string ToDiskActiveTimePercentage(this DiskMetrics metrics) =>
        $"{metrics.ToDiskActiveTimeRatio():000.0%}";

    public static double ToDiskTransferBytesPerSecond(this DiskMetrics metrics) =>
        metrics.ReadBytesPerSecond + metrics.WriteBytesPerSecond;

    public static string ToDiskTransferRate(this DiskMetrics metrics) =>
        metrics.ToDiskTransferBytesPerSecond().ToFormattedByteRate();

    public static string ToDiskReadRate(this DiskMetrics metrics) =>
        metrics.ReadBytesPerSecond.ToFormattedByteRate();

    public static string ToDiskWriteRate(this DiskMetrics metrics) =>
        metrics.WriteBytesPerSecond.ToFormattedByteRate();

    // The raw capacity the drives report, which is larger than the sum of the formatted volume
    // capacities they host.
    public static long ToDiskTotalCapacity(this DiskSpecs specs)
    {
        long capacity = 0;

        foreach (DiskDevice device in specs.Devices) {
            capacity += device.Capacity;
        }

        return capacity;
    }

    // The network figures are summed across the active adapters, with tunnel and virtual adapters
    // left out because they report the same bytes as the adapter underneath them.
    public static string ToNetworkSendRate(this NetworkMetrics metrics) =>
        metrics.SendBytesPerSecond.ToFormattedByteRate();

    public static string ToNetworkReceiveRate(this NetworkMetrics metrics) =>
        metrics.ReceiveBytesPerSecond.ToFormattedByteRate();

    public static double ToNetworkThroughputBytesPerSecond(this NetworkMetrics metrics) =>
        metrics.SendBytesPerSecond + metrics.ReceiveBytesPerSecond;

    // The single address the header reports as "this machine's ip". A host routinely has several,
    // so one is picked the way the old SystemInfo.GetPreferredIpAddress did: a wired adapter first,
    // then wireless, and only adapters that count toward the aggregate, which excludes the tunnel
    // and virtual adapters that would otherwise shadow the real one.
    public static string ToPreferredIPv4Address(this NetworkSpecs specs)
    {
        const string Ethernet = "Ethernet";
        const string Wireless = "Wi-Fi";

        return FirstIPv4Address(specs, Ethernet)
            ?? FirstIPv4Address(specs, Wireless)
            ?? string.Empty;
    }

    private static string? FirstIPv4Address(NetworkSpecs specs, string connectionType)
    {
        foreach (NetworkDevice device in specs.Devices) {
            if (!device.IsActive || !device.CountsTowardAggregate) {
                continue;
            }

            if (!string.Equals(device.ConnectionType, connectionType, StringComparison.Ordinal)) {
                continue;
            }

            if (device.IPv4Addresses.Length > 0) {
                return device.IPv4Addresses[0];
            }
        }

        return null;
    }

    // Packets are a count rather than a size, so they are not run through the byte formatter.
    public static string ToNetworkPacketCount(this ulong packets) =>
        packets.ToString("N0");

    // Link speed is bits per second and 1000 based, the way an adapter reports "2.5 Gbps".
    public static string ToLinkSpeed(this ulong bitsPerSecond)
    {
        if (bitsPerSecond == 0) {
            return NetworkDeviceParser.NotAvailable;
        }

        string[] units = ["bps", "Kbps", "Mbps", "Gbps", "Tbps"];
        int index = 0;
        double rate = bitsPerSecond;

        while (rate >= 1000.0 && index < units.Length - 1) {
            index++;
            rate /= 1000.0;
        }

        return index == 0 ? $"{rate:0} {units[index]}" : $"{rate:0.#} {units[index]}";
    }

    // Zero or more addresses joined for a single list-view cell.
    public static string ToAddressList(this string[] addresses) =>
        addresses.Length > 0
            ? string.Join(", ", addresses)
            : NetworkDeviceParser.NotAvailable;

    // ---- Per-device projections -------------------------------------------------------------
    //
    // These mirror the aggregate formatters above but read one entry out of the *Metrics.Devices /
    // *Specs.Devices lists the services already publish. Used by the *PerformanceControl screens
    // when the matching AppConfig ShowXxxCombined flag is false.

    private static double UsedRatio(long total, long available) =>
        total > 0 ? 1.0 - (double)available / total : 0.0;

    private static string UsedOverTotalBytes(long total, long available) =>
        (total - available).ToFormattedByteSize() + "/" + total.ToFormattedByteSize();

    public static double ToGpuMemoryRatio(this GpuDeviceMetrics device) =>
        UsedRatio(device.TotalGpuMemory, device.AvailableGpuMemory);

    public static string ToGpuMemoryPercentage(this GpuDeviceMetrics device) =>
        $"{device.ToGpuMemoryRatio():000.0%}";

    public static string ToGpuMemoryRatioFormattedBytes(this GpuDeviceMetrics device) =>
        UsedOverTotalBytes(device.TotalGpuMemory, device.AvailableGpuMemory);

    public static double ToSharedGpuMemoryRatio(this GpuDeviceMetrics device) =>
        UsedRatio(device.TotalSharedGpuMemory, device.AvailableSharedGpuMemory);

    public static string ToSharedGpuMemoryRatioFormattedBytes(this GpuDeviceMetrics device) =>
        UsedOverTotalBytes(device.TotalSharedGpuMemory, device.AvailableSharedGpuMemory);

    public static double ToCombinedGpuMemoryRatio(this GpuDeviceMetrics device) =>
        UsedRatio(device.TotalCombinedGpuMemory, device.AvailableCombinedGpuMemory);

    public static string ToCombinedGpuMemoryPercentage(this GpuDeviceMetrics device) =>
        $"{device.ToCombinedGpuMemoryRatio():000.0%}";

    public static string ToCombinedGpuMemoryRatioFormattedBytes(this GpuDeviceMetrics device) =>
        UsedOverTotalBytes(device.TotalCombinedGpuMemory, device.AvailableCombinedGpuMemory);

    public static string ToGpuPercentage(this GpuDeviceMetrics device) =>
        $"{device.GpuPercentTime:000.0%}";

    public static string ToDisplayName(this GpuDevice device) =>
        device.Description is { Length: > 0 } and not "N/A"
            ? $"GPU {device.Index}  {device.Description}"
            : $"GPU {device.Index}";

    public static double ToDiskActiveTimeRatio(this DiskDeviceMetrics device) =>
        Math.Clamp(device.PercentActiveTime / 100.0, 0.0, 1.0);

    public static string ToDiskActiveTimePercentage(this DiskDeviceMetrics device) =>
        $"{device.ToDiskActiveTimeRatio():000.0%}";

    public static double ToDiskTransferBytesPerSecond(this DiskDeviceMetrics device) =>
        device.ReadBytesPerSecond + device.WriteBytesPerSecond;

    public static string ToDiskTransferRate(this DiskDeviceMetrics device) =>
        device.ToDiskTransferBytesPerSecond().ToFormattedByteRate();

    public static string ToDiskReadRate(this DiskDeviceMetrics device) =>
        device.ReadBytesPerSecond.ToFormattedByteRate();

    public static string ToDiskWriteRate(this DiskDeviceMetrics device) =>
        device.WriteBytesPerSecond.ToFormattedByteRate();

    public static string ToDisplayName(this DiskDevice device)
    {
        string volumes = string.Join(
            " ",
            device.Volumes
                .SelectMany(volume => volume.MountPoints)
                .Select(mountPoint => mountPoint.TrimEnd('\\'))
                .Where(mountPoint => mountPoint.Length > 0));

        string model = device.Model is { Length: > 0 } and not "N/A" ? device.Model : "Disk";

        return volumes.Length > 0
            ? $"Disk {device.Index}  {model} ({volumes})"
            : $"Disk {device.Index}  {model}";
    }

    public static string ToNetworkSendRate(this NetworkDeviceMetrics device) =>
        device.SendBytesPerSecond.ToFormattedByteRate();

    public static string ToNetworkReceiveRate(this NetworkDeviceMetrics device) =>
        device.ReceiveBytesPerSecond.ToFormattedByteRate();

    public static double ToNetworkThroughputBytesPerSecond(this NetworkDeviceMetrics device) =>
        device.SendBytesPerSecond + device.ReceiveBytesPerSecond;

    public static string ToDisplayName(this NetworkDevice device)
    {
        if (device.FriendlyName is { Length: > 0 }) {
            return device.FriendlyName;
        }

        return device.Description is { Length: > 0 } ? device.Description : "Adapter";
    }

    public static string ToDisplayName(this NetworkDeviceMetrics device) =>
        device.FriendlyName is { Length: > 0 } ? device.FriendlyName : $"Adapter {device.InterfaceIndex}";

    // 1024 based, matching ToFormattedByteSize and how Task Manager formats its disk speeds.
    public static string ToFormattedByteRate(this double bytesPerSecond)
    {
        string[] rateFormatters = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
        int index = 0;
        double rate = bytesPerSecond > 0.0 ? bytesPerSecond : 0.0;

        while (rate >= 1024.0 && index < rateFormatters.Length - 1) {
            index++;
            rate /= 1024.0;
        }

        // A fixed decimal place so a value keeps the same width as it moves through a range.
        return $"{rate:0.0} {rateFormatters[index]}";
    }
}