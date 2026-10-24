#if __APPLE__
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.System.Services.Soc;

// Reads the H11ANEIn driver's DeviceProperties dictionary.
internal static class NeuralEngineParser
{
    private const string ArchitectureKey = "ANEDevicePropertyTypeANEArchitectureTypeStr";
    private const string CoresKey        = "ANEDevicePropertyNumANECores";
    private const string MinorVersionKey = "ANEDevicePropertyANEMinorVersion";
    private const string VersionKey      = "ANEDevicePropertyANEVersion";

    public static NpuDevice? Parse(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.GetValueOrDefault(CoresKey) is not long cores || cores <= 0) {
            return null;
        }

        string version = properties.GetValueOrDefault(VersionKey) is long major
            ? properties.GetValueOrDefault(MinorVersionKey) is long minor ? $"{major}.{minor}" : $"{major}"
            : string.Empty;

        return new NpuDevice {
            Index        = 0,
            Description  = "Apple Neural Engine",
            Vendor       = "Apple",
            Cores        = (int)cores,
            Architecture = properties.GetValueOrDefault(ArchitectureKey) as string ?? string.Empty,
            Version      = version
        };
    }
}
#endif
