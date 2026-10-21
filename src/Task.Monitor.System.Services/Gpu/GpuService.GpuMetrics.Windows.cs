using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Gpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class GpuService
{
#if __WIN32__
    private const string GpuEngineCounterPath = @"\GPU Engine(*)\Utilization Percentage";

    private PdhQuery? engineQuery;
    private nint      engineCounter;
    private bool      counterPrimed;

    private readonly Dictionary<long, double> engineUtilisationByLuid = new();

    private const uint MaxAdapterSegments = 64;
    private int segmentIdOffset = -1;

    private static unsafe void InitQueryStatistics(byte* buffer, uint type, long adapterLuid)
    {
        new Span<byte>(buffer, D3DKmt.QueryStatisticsBufferSize).Clear();

        *(uint*)(buffer + D3DKmt.OffsetType) = type;
        *(long*)(buffer + D3DKmt.OffsetAdapterLuid) = adapterLuid;
    }

    private static unsafe bool IsSegmentIdOffset(byte* buffer, long adapterLuid, int offset) =>
        QuerySegmentStatistics(
            buffer, 
            adapterLuid, 
            offset, 
            D3DKmt.InvalidSegmentId) != D3DKmt.STATUS_SUCCESS &&
        QuerySegmentStatistics(
            buffer, 
            adapterLuid, 
            offset, 
            0) == D3DKmt.STATUS_SUCCESS;

    private unsafe bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo)
    {
        nint factoryPtr     = nint.Zero;
        long totalDedicated = 0;
        long usedDedicated  = 0;
        long totalShared    = 0;
        long usedShared     = 0;
        bool queried        = false;

        try
        {
            Guid factoryIid = Dxgi.IID_IDXGIFactory1;
            int  hr         = Dxgi.CreateDXGIFactory1(ref factoryIid, out factoryPtr);

            if (hr < 0) {
                PInvokeErrorHelpers.TraceOnceOnHResult(
                    nameof(Dxgi.CreateDXGIFactory1),
                    $"Failed {nameof(OnDoWorkGpuMemoryMetrics)}",
                    hr);

                return false;
            }

            byte* buffer      = stackalloc byte[D3DKmt.QueryStatisticsBufferSize];
            uint adapterIndex = 0;

            while (Dxgi.EnumAdapters1(factoryPtr, adapterIndex, out nint adapter1Ptr) == Dxgi.S_OK)
            {
                try
                {
                    int descHr = Dxgi.GetDesc1(adapter1Ptr, out Dxgi.DXGI_ADAPTER_DESC1 desc);

                    if (descHr < 0) {
                        PInvokeErrorHelpers.TraceOnceOnHResult(
                            nameof(Dxgi.GetDesc1),
                            $"Failed {nameof(Dxgi.GetDesc1)}",
                            descHr);

                        continue;
                    }

                    if ((desc.Flags & Dxgi.DXGI_ADAPTER_FLAG_SOFTWARE) != 0) {
                        continue;
                    }

                    if (!TryGetBytesResident(
                        buffer,
                        desc.AdapterLuid,
                        out long dedicatedResident,
                        out long sharedResident)) {

                        continue;
                    }

                    totalDedicated += (long)(ulong)desc.DedicatedVideoMemory;
                    usedDedicated  += dedicatedResident;
                    totalShared    += (long)(ulong)desc.SharedSystemMemory;
                    usedShared     += sharedResident;
                    
                    queried = true;

                    long deviceDedicated = (long)(ulong)desc.DedicatedVideoMemory;
                    long deviceShared    = (long)(ulong)desc.SharedSystemMemory;

                    GpuDeviceMetrics metrics = new();
                    // Not inline declared to assist with debugging.
                    metrics.Index                      = (int)adapterIndex;
                    metrics.AdapterLuid                = desc.AdapterLuid;
                    metrics.GpuPercentTime             = Math.Clamp(engineUtilisationByLuid.GetValueOrDefault(desc.AdapterLuid) / 100.0, 0.0, 1.0);
                    metrics.TotalGpuMemory             = deviceDedicated;
                    metrics.AvailableGpuMemory         = deviceDedicated - dedicatedResident;
                    metrics.TotalSharedGpuMemory       = deviceShared;
                    metrics.AvailableSharedGpuMemory   = deviceShared - sharedResident;
                    metrics.TotalCombinedGpuMemory     = deviceDedicated + deviceShared;
                    metrics.AvailableCombinedGpuMemory = (deviceDedicated + deviceShared) - (dedicatedResident + sharedResident);

                    gpuInfo.Metrics.Devices.Add(metrics);
                }
                finally
                {
                    Marshal.Release(adapter1Ptr);
                    adapterIndex++;
                }
            }
        }
        catch (Exception ex)
        {
            TraceEx.WriteLineOnce(
                nameof(OnDoWorkGpuMemoryMetrics),
                $"Failed {nameof(OnDoWorkGpuMemoryMetrics)}: {ex.Message}");

            return false;
        }
        finally
        {
            if (factoryPtr != nint.Zero) {
                Marshal.Release(factoryPtr);
            }
        }

        if (!queried) {
            return false;
        }

        if (gpuInfo.Specs.TotalGpuMemory <= 0) {
            gpuInfo.Specs.TotalGpuMemory = totalDedicated;
        }

        long dedicatedTotal = gpuInfo.Specs.TotalGpuMemory;

        if (dedicatedTotal <= 0) {
            return false;
        }

        gpuInfo.Metrics.TotalGpuMemory             = dedicatedTotal;
        gpuInfo.Metrics.AvailableGpuMemory         = dedicatedTotal - usedDedicated;
        gpuInfo.Metrics.TotalSharedGpuMemory       = totalShared;
        gpuInfo.Metrics.AvailableSharedGpuMemory   = totalShared - usedShared;
        gpuInfo.Metrics.TotalCombinedGpuMemory     = dedicatedTotal + totalShared;
        gpuInfo.Metrics.AvailableCombinedGpuMemory = (dedicatedTotal + totalShared) - (usedDedicated + usedShared);

        return true;
    }

    private unsafe void OnDoWorkGpuPidMetrics(GpuInfo gpuInfo)
    {
        gpuInfo.Metrics.GpuPercentTime = 0.0;

        engineUtilisationByLuid.Clear();

        if (engineQuery is null || !engineQuery.Collect()) {
            return;
        }

        if (!counterPrimed) {
            counterPrimed = true;
            return;
        }

        if (!engineQuery.TryReadFormattedDoubles(engineCounter, out uint itemCount)) {
            return;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)engineQuery.Buffer;

        Dictionary<string, double> engineTotals  = new();
        Dictionary<int, double>    processTotals = new();

        for (uint i = 0; i < itemCount; i++) {
            Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item = items[i];

            if (item.CStatus != Pdh.PDH_CSTATUS_VALID_DATA &&
                item.CStatus != Pdh.PDH_CSTATUS_NEW_DATA) {
                continue;
            }

            string? instanceName = Marshal.PtrToStringUni(item.szName);

            if (string.IsNullOrEmpty(instanceName)) {
                continue;
            }

            string? engineKey = GpuDeviceParser.ParseEngineFromInstance(instanceName);

            if (engineKey == null) {
                continue;
            }

            engineTotals[engineKey] = engineTotals.GetValueOrDefault(engineKey) + item.doubleValue;
            int pid = GpuDeviceParser.ParsePidFromInstance(instanceName);

            if (pid >= 0 && item.doubleValue > 0.0) {
                processTotals[pid] = Math.Max(processTotals.GetValueOrDefault(pid), item.doubleValue);
            }
        }

        double busiestEngine = 0.0;

        foreach ((string engineKey, double engineTotal) in engineTotals) {
            busiestEngine = Math.Max(busiestEngine, engineTotal);

            if (GpuDeviceParser.TryParseAdapterLuid(engineKey, out long luid)) {
                engineUtilisationByLuid[luid] =
                    Math.Max(engineUtilisationByLuid.GetValueOrDefault(luid), engineTotal);
            }
        }

        gpuInfo.Metrics.GpuPercentTime = Math.Clamp(busiestEngine / 100.0, 0.0, 1.0);

        foreach ((int pid, double percent) in processTotals) {
            gpuInfo.Metrics.ProcessPercentTime[pid] = Math.Clamp(percent / 100.0, 0.0, 1.0);
        }
    }

    private void OnStartGpuPidMetrics()
    {
        engineQuery   = PdhQuery.Open(nameof(GpuService), GpuEngineCounterPath);
        engineCounter = engineQuery?.Counters[0] ?? nint.Zero;
        counterPrimed = false;
    }

    private void OnStopGpuPidMetrics()
    {
        engineQuery?.Dispose();
        engineQuery   = null;
        engineCounter = nint.Zero;
        counterPrimed = false;
    }

    private static unsafe int QuerySegmentStatistics(
        byte* buffer,
        long  adapterLuid,
        int   offset,
        uint  segmentId)
    {
        InitQueryStatistics(buffer, D3DKmt.D3DKMT_QUERYSTATISTICS_SEGMENT, adapterLuid);
        *(uint*)(buffer + offset) = segmentId;

        return D3DKmt.D3DKMTQueryStatistics((nint)buffer);
    }

    private unsafe int ResolveSegmentIdOffset(byte* buffer, long adapterLuid)
    {
        if (segmentIdOffset > 0) {
            return segmentIdOffset;
        }

        int expected = D3DKmt.OffsetResult + D3DKmt.DefaultResultSize;

        if (IsSegmentIdOffset(buffer, adapterLuid, expected)) {
            segmentIdOffset = expected;
            return expected;
        }

        for (int offset = D3DKmt.OffsetResult;
             offset <= D3DKmt.QueryStatisticsBufferSize - sizeof(ulong);
             offset += sizeof(uint)) {

            if (!IsSegmentIdOffset(buffer, adapterLuid, offset)) {
                continue;
            }

            TraceEx.WriteLineOnce(
                nameof(ResolveSegmentIdOffset),
                $"D3DKMT_QUERYSTATISTICS SegmentId resolved to offset {offset}, expected {expected}");

            segmentIdOffset = offset;
            return offset;
        }

        TraceEx.WriteLineOnce(
            nameof(ResolveSegmentIdOffset),
            "Failed to locate the D3DKMT_QUERYSTATISTICS SegmentId offset");

        return -1;
    }

    private unsafe bool TryGetBytesResident(
        byte*    buffer,
        long     adapterLuid,
        out long dedicatedBytesResident,
        out long sharedBytesResident)
    {
        dedicatedBytesResident = 0;
        sharedBytesResident    = 0;

        InitQueryStatistics(buffer, D3DKmt.D3DKMT_QUERYSTATISTICS_ADAPTER, adapterLuid);
        int status = D3DKmt.D3DKMTQueryStatistics((nint)buffer);

        if (status != D3DKmt.STATUS_SUCCESS) {
            TraceEx.WriteLineOnce(
                $"{nameof(D3DKmt.D3DKMTQueryStatistics)} adapter 0x{adapterLuid:X16}",
                $"Failed {nameof(D3DKmt.D3DKMTQueryStatistics)} adapter query: NTSTATUS 0x{status:X8}");

            return false;
        }

        uint segmentCount = *(uint*)(buffer + D3DKmt.OffsetResult + D3DKmt.OffsetAdapterNbSegments);

        if (segmentCount == 0 || segmentCount > MaxAdapterSegments) {
            TraceEx.WriteLineOnce(
                $"{nameof(D3DKmt.D3DKMTQueryStatistics)} segments 0x{adapterLuid:X16}",
                $"Implausible segment count {segmentCount} for adapter 0x{adapterLuid:X16}");

            return false;
        }

        int offset = ResolveSegmentIdOffset(buffer, adapterLuid);

        if (offset < 0) {
            return false;
        }

        for (uint segment = 0; segment < segmentCount; segment++) {
            if (QuerySegmentStatistics(
                    buffer, 
                    adapterLuid, 
                    offset, 
                    segment) != D3DKmt.STATUS_SUCCESS) {
                
                continue;
            }

            long resident = (long)*(ulong*)(buffer + D3DKmt.OffsetResult + D3DKmt.OffsetSegmentBytesResident);

            if (*(uint*)(buffer + D3DKmt.OffsetResult + D3DKmt.OffsetSegmentAperture) != 0) {
                sharedBytesResident += resident;
            }
            else {
                dedicatedBytesResident += resident;
            }
        }

        return true;
    }

#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
