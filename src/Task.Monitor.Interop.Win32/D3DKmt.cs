using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// D3DKMTQueryStatistics reports the display kernel's per adapter memory segment statistics.
/// This is the adapter wide view that Task Manager shows; IDXGIAdapter3.QueryVideoMemoryInfo
/// only ever reports the calling process' own usage, which is zero for a monitor that never
/// creates a D3D device.
/// </summary>
public static class D3DKmt
{
    public const int STATUS_SUCCESS = 0;

    // D3DKMT_QUERYSTATISTICS_TYPE. Only the two we consume are declared.
    public const uint D3DKMT_QUERYSTATISTICS_ADAPTER = 0;
    public const uint D3DKMT_QUERYSTATISTICS_SEGMENT = 3;

    // D3DKMT_QUERYSTATISTICS has no public struct definition, so it is driven as a raw buffer:
    //
    //   0    UINT   Type
    //   4    LUID   AdapterLuid
    //   16   HANDLE hProcess
    //   24   D3DKMT_QUERYSTATISTICS_RESULT   (output union)
    //   ?    input union { UINT SegmentId; ... }
    //
    // The result union is an undocumented size, so callers resolve the offset of the input union
    // at runtime rather than trust it from a header. Everything up to and including the start of
    // the result union is fixed, which is what the field offsets below describe.
    public const int QueryStatisticsBufferSize = 4096;
    public const int OffsetType = 0;
    public const int OffsetAdapterLuid = 4;
    public const int OffsetProcess = 16;
    public const int OffsetResult = 24;

    // The size of D3DKMT_QUERYSTATISTICS_RESULT on Windows 10 and 11. Verified, but treated as a
    // starting guess only - see ResolveSegmentIdOffset.
    public const int DefaultResultSize = 776;

    // D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION, relative to OffsetResult.
    public const int OffsetAdapterNbSegments = 0;
    public const int OffsetAdapterNodeCount = 4;

    // D3DKMT_QUERYSTATISTICS_SEGMENT_INFORMATION, relative to OffsetResult.
    //   ULONG64 CommitLimit
    //   ULONG64 BytesCommitted
    //   ULONG64 BytesResident
    //   D3DKMT_QUERYSTATSTICS_MEMORY Memory   (16 bytes)
    //   UINT    Aperture
    public const int OffsetSegmentCommitLimit = 0;
    public const int OffsetSegmentBytesCommitted = 8;
    public const int OffsetSegmentBytesResident = 16;
    public const int OffsetSegmentAperture = 40;

    // A SegmentId no adapter will own, used to prove that a candidate offset is really the one
    // the display kernel reads - at the wrong offset the write is ignored and segment zero is
    // returned instead of an error.
    public const uint InvalidSegmentId = 0xFFFF;

    [DllImport(Libraries.Gdi32, ExactSpelling = true)]
    public static extern int D3DKMTQueryStatistics(nint pData);
}
