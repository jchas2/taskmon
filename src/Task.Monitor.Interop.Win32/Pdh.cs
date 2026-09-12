using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class Pdh
{
    public const uint ERROR_SUCCESS = 0;
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    public const int  PDH_MORE_DATA  = unchecked((int)0x800007D2);
    public const uint PDH_FMT_DOUBLE = 0x00000200;

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PDH_FMT_COUNTERVALUE_ITEM_W
    {
        [FieldOffset( 0)] public nint szName;
        [FieldOffset( 8)] public uint CStatus;   
        [FieldOffset(16)] public double doubleValue;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_RAW_COUNTER {
        public uint CStatus;
        public MinWinBase.FILETIME TimeStamp;
        public long FirstValue;
        public long SecondValue;
        public uint MultiCount;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PDH_RAW_COUNTER_ITEM {
        [MarshalAs(UnmanagedType.LPWStr)] 
        public string szName;
        public PDH_RAW_COUNTER RawValue;
    }

    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern unsafe uint PdhOpenQuery(
        [MarshalAs(UnmanagedType.LPWStr)]
        string? szDataSource, 
        nint    dwUserData, 
        nint*   phQuery);
    
    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern unsafe uint PdhAddEnglishCounter(
        nint   hQuery, 
        string szFullCounterPath, 
        nint   dwUserData, 
        nint*  phCounter);
    
    [DllImport(Libraries.Pdh)]
    public static extern uint PdhCollectQueryData(nint hQuery);
    
    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern unsafe int PdhGetFormattedCounterArrayW(
        nint  hCounter,
        uint  dwFormat,
        uint* pdwBufferSize,
        uint* pdwItemCount,
        nint  ItemBuffer);
    
    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern unsafe uint PdhGetRawCounterArray(
        nint  hCounter, 
        uint* lpdwBufferSize, 
        uint* lpdwItemCount, 
        nint  ItemBuffer);
    
    [DllImport(Libraries.Pdh)]
    public static extern uint PdhCloseQuery(nint hQuery);
}