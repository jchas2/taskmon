using System.Runtime.InteropServices;
using Microsoft.Win32;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Cpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class CpuService
{
#if __WIN32__
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private void OnStartCpuSpecs(ref CpuSpecs specs)
    {
        const string RegPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0\";
        const string RegKeyProcessorName = "ProcessorNameString";
        const string RegKeyFrequencyMhz = "~Mhz";

        specs.CpuCores = (ulong)Environment.ProcessorCount;
        specs.CpuFrequency = 0;
        specs.CpuName = string.Empty;

        PopulateTopology(ref specs);

        specs.CpuVirtualizationFirmwareEnabled = ProcessThreadsApi.IsProcessorFeaturePresent(
            ProcessThreadsApi.PF_VIRT_FIRMWARE_ENABLED);

        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegPath);
        
        if (key == null) {
            TraceEx.WriteLineOnce(RegPath, "Failed GetCpuInfoInternal OpenSubKey");
            return;
        }
        
        object? processorName = key.GetValue(RegKeyProcessorName);
        
        if (processorName == null) {
            TraceEx.WriteLineOnce(RegKeyProcessorName, "Failed GetCpuInfoInternal GetValue");
            return;
        }
        
        specs.CpuName = processorName.ToString() ?? string.Empty;    

        object? frequency = key.GetValue(RegKeyFrequencyMhz);
        
        if (frequency == null) {
            TraceEx.WriteLineOnce(RegKeyFrequencyMhz, "Failed GetCpuInfoInternal GetValue");
            return;
        }

        if (!int.TryParse(frequency.ToString() ?? "0", out int frequencyInt32)) {
            TraceEx.WriteLineOnce($"FRQ{frequency}", $"Failed GetCpuInfoInternal TryParse frequency");
        }
        
        specs.CpuFrequency = frequencyInt32;
    }

    // Socket count and L1/L2/L3 cache totals, decoded from the processor relationship stream.
    private static unsafe void PopulateTopology(ref CpuSpecs specs)
    {
        const WinNt.LOGICAL_PROCESSOR_RELATIONSHIP RelationAll = WinNt.LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll;

        uint length = 0;

        if (SysInfoApi.GetLogicalProcessorInformationEx(RelationAll, null, &length) ||
            Marshal.GetLastPInvokeError() != ERROR_INSUFFICIENT_BUFFER ||
            length == 0) {

            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(SysInfoApi.GetLogicalProcessorInformationEx),
                "Failed to size the processor information buffer");

            return;
        }

        nint buffer = Marshal.AllocHGlobal((int)length);

        try {
            if (!SysInfoApi.GetLogicalProcessorInformationEx(RelationAll, (byte*)buffer, &length)) {
                PInvokeErrorHelpers.TraceOnceOnLastError(
                    nameof(SysInfoApi.GetLogicalProcessorInformationEx),
                    "Failed to read the processor information buffer");

                return;
            }

            CpuTopology topology = CpuTopologyParser.Parse(new ReadOnlySpan<byte>((void*)buffer, (int)length));

            // There is always at least one package even if the enumeration reported none.
            specs.CpuSockets      = topology.SocketCount == 0 ? 1 : topology.SocketCount;
            specs.CpuL1CacheBytes = topology.L1CacheBytes;
            specs.CpuL2CacheBytes = topology.L2CacheBytes;
            specs.CpuL3CacheBytes = topology.L3CacheBytes;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
