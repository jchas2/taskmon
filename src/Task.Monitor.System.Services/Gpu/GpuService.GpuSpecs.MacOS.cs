#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
    private const string AppleVendor           = "Apple";
    private const string AppleGpuDescription   = "Apple GPU";
    private const string IntegratedAdapterType = "Integrated";

    private static int GetGpuCoreCount()
    {
        using IOObjectScope service = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching("AGXAccelerator")));

        return !service.IsNull && IOKit.SearchProperty(service, "gpu-core-count", IOKit.IterateRecursively) is long cores
            ? (int)cores
            : 0;
    }

    private void OnStartGpuSpecs(GpuSpecs specs)
    {
        bool memoryRead  = TryReadIOAcceleratorMemory(out long allocMemory, out _);
        long totalMemory = memoryRead ? allocMemory : 0;

        specs.GpuCores = GetGpuCoreCount();

        Trace.WriteLine(memoryRead
            ? $"{ServiceName}: IOAccelerator memory statistics OK"
            : $"{ServiceName}: IOAccelerator memory statistics NOT FOUND");

        Trace.WriteLine(
            $"{ServiceName}: adapter 0 {AppleGpuDescription}, {IntegratedAdapterType}, {specs.GpuCores} cores " +
            $"(AGXAccelerator), {totalMemory / (1024 * 1024)} MB allocated");
        specs.TotalGpuMemory = totalMemory;

        specs.Devices.Clear();
        specs.Devices.Add(new GpuDevice {
            Index                = 0,
            Vendor               = AppleVendor,
            // A precise model string ("Apple M1 Pro") would require Metal (MTLDevice.name); the
            // IORegistry "model" property is inconsistent across SoCs, so use a default for now.
            Description          = AppleGpuDescription,
            AdapterType          = IntegratedAdapterType,
            DedicatedVideoMemory = 0,
            SharedSystemMemory   = totalMemory
        });
    }
}
#endif
