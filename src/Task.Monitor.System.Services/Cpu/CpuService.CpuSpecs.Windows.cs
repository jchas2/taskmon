using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Cpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class CpuService
{
#if __WIN32__
    private const string CpuRegistryPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0\";

    private static string GetCpuNameInternal()
    {
        const string RegKeyProcessorName = "ProcessorNameString";

        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(CpuRegistryPath);

        if (key == null) {
            TraceEx.WriteLineOnce(CpuRegistryPath, "Failed GetCpuNameInternal OpenSubKey");
            return string.Empty;
        }

        // REG_SZ
        object? processorName = key.GetValue(RegKeyProcessorName);

        if (processorName == null) {
            TraceEx.WriteLineOnce(RegKeyProcessorName, "Failed GetCpuNameInternal GetValue");
            return string.Empty;
        }

        return processorName.ToString() ?? string.Empty;
    }

    private void OnStartCpuSpecs(ref CpuSpecs specs)
    {
        specs.CpuCores = (ulong)Environment.ProcessorCount;
        specs.CpuFrequency            = 0;
        specs.CpuName                 = string.Empty;
        specs.CpuSockets              = 0;
        specs.CpuL1CacheBytes         = 0;
        specs.CpuL2CacheBytes         = 0;
        specs.CpuL3CacheBytes         = 0;
        specs.CpuEfficiencyCores      = 0;
        specs.CpuEfficiencyFrequency  = 0.0;
        specs.CpuPerformanceCores     = 0;
        specs.CpuPerformanceFrequency = 0.0;
        specs.CpuSuperCores           = 0;
        specs.CpuSuperFrequency       = 0.0;
        specs.CpuVirtualizationFirmwareEnabled = false;

        PopulateCpuSpecs(ref specs);
        PopulateCpuTopology(ref specs);
    }

    private static void PopulateCpuSpecs(ref CpuSpecs specs)
    {
        const string RegKeyFrequencyMhz = "~Mhz";

        specs.CpuVirtualizationFirmwareEnabled = ProcessThreadsApi.IsProcessorFeaturePresent(
            ProcessThreadsApi.PF_VIRT_FIRMWARE_ENABLED);

        specs.CpuName = GetCpuNameInternal();

        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(CpuRegistryPath);

        if (key == null) {
            Trace.WriteLine($"{nameof(CpuService)}: registry {CpuRegistryPath} FAILED, the key could not be opened");
            return;
        }

        // Reg DWORD
        object? frequency = key.GetValue(RegKeyFrequencyMhz);
        
        if (frequency == null) {
            Trace.WriteLine($"{nameof(CpuService)}: registry {CpuRegistryPath}{RegKeyFrequencyMhz} NOT FOUND");
            return;
        }

        if (!int.TryParse(frequency.ToString() ?? "0", out int frequencyInt32)) {
            Trace.WriteLine($"{nameof(CpuService)}: registry {CpuRegistryPath}{RegKeyFrequencyMhz} FAILED, \"{frequency}\" is not a number");
        }
        
        specs.CpuFrequency = frequencyInt32;
    }
    
    private static unsafe void PopulateCpuTopology(ref CpuSpecs specs)
    {
        const WinNt.LOGICAL_PROCESSOR_RELATIONSHIP RelationAll = WinNt.LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll;

        uint length = 0;

        if (SysInfoApi.GetLogicalProcessorInformationEx(RelationAll, null, &length) ||
            Marshal.GetLastPInvokeError() != SysInfoApi.ERROR_INSUFFICIENT_BUFFER ||
            length == 0) {

            Trace.WriteLine(
                $"{nameof(CpuService)}: {nameof(SysInfoApi.GetLogicalProcessorInformationEx)} FAILED " +
                $"{ErrorCodes.Describe(Marshal.GetLastPInvokeError())}, sizing the buffer");

            return;
        }

        using HGlobalScope buffer = HGlobalScope.Allocate((int)length);

        if (!SysInfoApi.GetLogicalProcessorInformationEx(RelationAll, (byte*)buffer.Value, &length)) {
            Trace.WriteLine(
                $"{nameof(CpuService)}: {nameof(SysInfoApi.GetLogicalProcessorInformationEx)} FAILED " +
                $"{ErrorCodes.Describe(Marshal.GetLastPInvokeError())}");

            return;
        }

        CpuTopology topology = CpuTopologyParser.Parse(new ReadOnlySpan<byte>((void*)buffer.Value, (int)length));

        specs.CpuSockets      = topology.SocketCount == 0 ? 1 : topology.SocketCount;
        specs.CpuL1CacheBytes = topology.L1CacheBytes;
        specs.CpuL2CacheBytes = topology.L2CacheBytes;
        specs.CpuL3CacheBytes = topology.L3CacheBytes;
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
