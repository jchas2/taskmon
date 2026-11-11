using System.Diagnostics;

namespace Task.Monitor.System.Services.Memory;

public sealed partial class MemoryService : WorkerService
{
    private MemorySpecs memorySpecs = new();

    private void LogDevices(MemorySpecs specs)
    {
        foreach (MemoryDevice device in specs.Devices) {
            Trace.WriteLine(device.SizeInMegabytes == 0
                ? $"{ServiceName}: slot {device.Slot} {device.DeviceLocator}, empty"
                : $"{ServiceName}: slot {device.Slot} {device.DeviceLocator}, {device.SizeInMegabytes} MB " +
                  $"{device.MemoryType} {device.FormFactor}, {device.Speed} MT/s (configured {device.ConfiguredClockSpeed}), " +
                  $"{device.Manufacturer} {device.PartNumber}");
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        MemoryInfo memoryInfo = new();
        memoryInfo.Specs = memorySpecs;

        OnDoWorkMemoryMetrics(memoryInfo);
        OnDoWorkMemoryCompressionMetrics(memoryInfo);
        OnDoWorkPressureMetrics(memoryInfo);
        Publish(memoryInfo);
    }

    protected override void OnStart()
    {
        OnStartMemorySpecs(memorySpecs);
        LogDevices(memorySpecs);
        OnStartPressureMetrics();
    }

    protected override void OnStop() =>
        OnStopPressureMetrics();
}
