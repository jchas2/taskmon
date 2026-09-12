using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Gpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class GpuService
{
#if __WIN32__
    // "Utilization Percentage" is a rate counter, so Pdh derives the busy time over the interval
    // between two collections rather than us differencing "Running Time" against a nominal Delay
    // that ignores how long the cycle actually took. That requires a query which stays open across
    // ticks, so consecutive samples bracket one poll interval.
    private const string GpuEngineCounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

    private nint engineQuery = nint.Zero;
    private nint engineCounter = nint.Zero;
    private nint counterBuffer = nint.Zero;
    private uint counterBufferSize = 0;
    private bool counterPrimed = false;

    // Busiest engine per adapter LUID from the last \GPU Engine(*) collection, so the memory pass
    // can fold a utilisation figure into each per adapter GpuDeviceMetrics without opening a
    // second query on the same provider.
    private readonly Dictionary<long, double> engineUtilisationByLuid = new();

    private unsafe void OnStartGpuPidMetrics()
    {
        uint pdhResult;
        nint query = nint.Zero;
        nint counter = nint.Zero;

        if ((pdhResult = Pdh.PdhOpenQuery(
            null,
            nint.Zero,
            &query)) != Pdh.ERROR_SUCCESS) {

            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhOpenQuery),
                $"Failed {nameof(OnStartGpuPidMetrics)}",
                pdhResult);

            return;
        }

        // The wildcard is re-expanded on every PdhCollectQueryData, so engines that appear after
        // this point (a process starting up, a second adapter waking) are still picked up.
        if ((pdhResult = Pdh.PdhAddEnglishCounter(
            query,
            GpuEngineCounterPath,
            nint.Zero,
            &counter)) != Pdh.ERROR_SUCCESS) {

            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                GpuEngineCounterPath,
                $"Failed {nameof(Pdh.PdhAddEnglishCounter)}",
                pdhResult);

            Pdh.PdhCloseQuery(query);
            return;
        }

        engineQuery = query;
        engineCounter = counter;
        counterPrimed = false;
    }

    private void OnStopGpuPidMetrics()
    {
        if (counterBuffer != nint.Zero) {
            Marshal.FreeHGlobal(counterBuffer);
            counterBuffer = nint.Zero;
            counterBufferSize = 0;
        }

        if (engineQuery != nint.Zero) {
            Pdh.PdhCloseQuery(engineQuery);
            engineQuery = nint.Zero;
            engineCounter = nint.Zero;
        }

        counterPrimed = false;
    }

    private unsafe void OnDoWorkGpuPidMetrics(GpuInfo gpuInfo)
    {
        uint pdhResult;

        gpuInfo.Metrics.GpuPercentTime = 0.0;
        engineUtilisationByLuid.Clear();

        if (engineQuery == nint.Zero) {
            return;
        }

        if ((pdhResult = Pdh.PdhCollectQueryData(engineQuery)) != Pdh.ERROR_SUCCESS) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhCollectQueryData),
                $"Failed {nameof(OnDoWorkGpuPidMetrics)}",
                pdhResult);

            return;
        }

        // A rate counter has no value until a second collection supplies the interval to divide
        // by, so the first cycle only primes it and reports nothing.
        if (!counterPrimed) {
            counterPrimed = true;
            return;
        }

        if (!TryGetEngineCounterArray(out uint itemCount)) {
            return;
        }

        // Processes time-share an engine, so an engine's load is the sum of the processes on it.
        // The headline figure is then the busiest engine rather than the sum of every engine,
        // matching Task Manager: 3D and Copy both at 50% is a card at 50%, not 100%.
        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)counterBuffer;
        Dictionary<string, double> engineTotals = new();
        Dictionary<int, double> processTotals = new();

        for (uint i = 0; i < itemCount; i++) {
            Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item = items[i];

            if (item.CStatus != Pdh.PDH_CSTATUS_VALID_DATA &&
                item.CStatus != PDH_CSTATUS_NEW_DATA) {
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

            // The same array projected by process rather than by engine, so ProcessService can
            // join per pid figures that are guaranteed to agree with the headline above without
            // opening a second query on the same provider.
            //
            // Max across the engines a process is using, matching the busiest engine rule the
            // aggregate follows: a process at 50% on 3D and 50% on Copy is using half a card,
            // not all of it.
            int pid = GpuDeviceParser.ParsePidFromInstance(instanceName);

            if (pid >= 0 && item.doubleValue > 0.0) {
                processTotals[pid] = Math.Max(processTotals.GetValueOrDefault(pid), item.doubleValue);
            }
        }

        double busiestEngine = 0.0;

        foreach ((string engineKey, double engineTotal) in engineTotals) {
            busiestEngine = Math.Max(busiestEngine, engineTotal);

            // The same busiest engine rule as the headline, but per adapter: the luid token is the
            // part of the engine key every engine on one card has in common.
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

    private unsafe bool TryGetEngineCounterArray(out uint itemCount)
    {
        itemCount = 0;

        // Pdh sizes the buffer for us. Instances come and go between ticks, so a buffer that was
        // large enough last cycle can fall short on this one; the buffer is grown and kept rather
        // than reallocated every cycle.
        for (int attempt = 0; attempt < 3; attempt++) {
            uint bufferSize = counterBufferSize;
            uint count = 0;

            int result = Pdh.PdhGetFormattedCounterArrayW(
                engineCounter,
                Pdh.PDH_FMT_DOUBLE,
                &bufferSize,
                &count,
                counterBuffer);

            if (result == (int)Pdh.ERROR_SUCCESS) {
                itemCount = count;
                return true;
            }

            if (result != Pdh.PDH_MORE_DATA) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} {engineCounter}",
                    $"Failed {nameof(OnDoWorkGpuPidMetrics)}",
                    (uint)result);

                return false;
            }

            if (bufferSize <= counterBufferSize) {
                TraceEx.WriteLineOnce(
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} {engineCounter}",
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} failed to calculate {nameof(bufferSize)}");

                return false;
            }

            if (counterBuffer != nint.Zero) {
                Marshal.FreeHGlobal(counterBuffer);
                counterBuffer = nint.Zero;
                counterBufferSize = 0;
            }

            counterBuffer = Marshal.AllocHGlobal((int)bufferSize);
            counterBufferSize = bufferSize;
        }

        return false;
    }

    private const uint MaxAdapterSegments = 64;
    private int segmentIdOffset = -1;

    private unsafe bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo)
    {
        nint factoryPtr = nint.Zero;
        long totalDedicated = 0;
        long usedDedicated = 0;
        long totalShared = 0;
        long usedShared = 0;
        bool queried = false;

        try
        {
            Guid factoryIid = Dxgi.IID_IDXGIFactory1;
            int hr = Dxgi.CreateDXGIFactory1(ref factoryIid, out factoryPtr);

            if (hr < 0) {
                TraceEx.WriteLineOnce(
                    nameof(Dxgi.CreateDXGIFactory1),
                    $"Failed {nameof(OnDoWorkGpuMemoryMetrics)}: HRESULT 0x{hr:X8}");
                return false;
            }

            byte* buffer = stackalloc byte[D3DKmt.QueryStatisticsBufferSize];

            uint adapterIndex = 0;
            while (Dxgi.EnumAdapters1(factoryPtr, adapterIndex, out nint adapter1Ptr) == 0) // S_OK
            {
                try
                {
                    int descHr = Dxgi.GetDesc1(adapter1Ptr, out Dxgi.DXGI_ADAPTER_DESC1 desc);

                    if (descHr < 0) {
                        TraceEx.WriteLineOnce(
                            nameof(Dxgi.GetDesc1),
                            $"Failed {nameof(Dxgi.GetDesc1)}: HRESULT 0x{descHr:X8}");

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
                    usedDedicated += dedicatedResident;
                    totalShared += (long)(ulong)desc.SharedSystemMemory;
                    usedShared += sharedResident;
                    queried = true;

                    long deviceDedicated = (long)(ulong)desc.DedicatedVideoMemory;
                    long deviceShared = (long)(ulong)desc.SharedSystemMemory;

                    gpuInfo.Metrics.Devices.Add(new GpuDeviceMetrics {
                        Index = (int)adapterIndex,
                        AdapterLuid = desc.AdapterLuid,
                        GpuPercentTime = Math.Clamp(
                            engineUtilisationByLuid.GetValueOrDefault(desc.AdapterLuid) / 100.0, 0.0, 1.0),
                        TotalGpuMemory = deviceDedicated,
                        AvailableGpuMemory = deviceDedicated - dedicatedResident,
                        TotalSharedGpuMemory = deviceShared,
                        AvailableSharedGpuMemory = deviceShared - sharedResident,
                        TotalCombinedGpuMemory = deviceDedicated + deviceShared,
                        AvailableCombinedGpuMemory =
                            (deviceDedicated + deviceShared) - (dedicatedResident + sharedResident),
                    });
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

        gpuInfo.Metrics.TotalGpuMemory = dedicatedTotal;
        gpuInfo.Metrics.AvailableGpuMemory = dedicatedTotal - usedDedicated;

        gpuInfo.Metrics.TotalSharedGpuMemory = totalShared;
        gpuInfo.Metrics.AvailableSharedGpuMemory = totalShared - usedShared;

        gpuInfo.Metrics.TotalCombinedGpuMemory = dedicatedTotal + totalShared;
        gpuInfo.Metrics.AvailableCombinedGpuMemory =
            (dedicatedTotal + totalShared) - (usedDedicated + usedShared);

        return true;
    }

    private unsafe bool TryGetBytesResident(
        byte* buffer,
        long adapterLuid,
        out long dedicatedBytesResident,
        out long sharedBytesResident)
    {
        dedicatedBytesResident = 0;
        sharedBytesResident = 0;

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
            if (QuerySegmentStatistics(buffer, adapterLuid, offset, segment) != D3DKmt.STATUS_SUCCESS) {
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

    private static unsafe bool IsSegmentIdOffset(byte* buffer, long adapterLuid, int offset) =>
        QuerySegmentStatistics(buffer, adapterLuid, offset, D3DKmt.InvalidSegmentId) != D3DKmt.STATUS_SUCCESS &&
        QuerySegmentStatistics(buffer, adapterLuid, offset, 0) == D3DKmt.STATUS_SUCCESS;

    private static unsafe void InitQueryStatistics(byte* buffer, uint type, long adapterLuid)
    {
        new Span<byte>(buffer, D3DKmt.QueryStatisticsBufferSize).Clear();

        *(uint*)(buffer + D3DKmt.OffsetType) = type;
        *(long*)(buffer + D3DKmt.OffsetAdapterLuid) = adapterLuid;
    }

    private static unsafe int QuerySegmentStatistics(
        byte* buffer,
        long adapterLuid,
        int offset,
        uint segmentId)
    {
        InitQueryStatistics(buffer, D3DKmt.D3DKMT_QUERYSTATISTICS_SEGMENT, adapterLuid);
        *(uint*)(buffer + offset) = segmentId;

        return D3DKmt.D3DKMTQueryStatistics((nint)buffer);
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
