using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class D3DKmt
{
    public const int STATUS_SUCCESS = 0;

    public const uint D3DKMT_QUERYSTATISTICS_ADAPTER = 0;
    public const uint D3DKMT_QUERYSTATISTICS_SEGMENT = 3;

    // D3DKMT_QUERYSTATISTICS has no public struct definition, so treated as a raw buffer.
    public const int QueryStatisticsBufferSize = 4096;
    public const int OffsetType = 0;
    public const int OffsetAdapterLuid = 4;
    public const int OffsetProcess = 16;
    public const int OffsetResult = 24;

    // What it should be on Win 10 + 11, but treated as a starting guess - see ResolveSegmentIdOffset.
    public const int DefaultResultSize = 776;

    // D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION, relative to OffsetResult.
    public const int OffsetAdapterNbSegments = 0;
    public const int OffsetAdapterNodeCount = 4;

    // D3DKMT_QUERYSTATISTICS_SEGMENT_INFORMATION, relative to OffsetResult.
    public const int OffsetSegmentCommitLimit = 0;
    public const int OffsetSegmentBytesCommitted = 8;
    public const int OffsetSegmentBytesResident = 16;
    public const int OffsetSegmentAperture = 40;

    public const uint InvalidSegmentId = 0xFFFF;

    [DllImport(Libraries.Gdi32, ExactSpelling = true)]
    public static extern int D3DKMTQueryStatistics(nint pData);
}
