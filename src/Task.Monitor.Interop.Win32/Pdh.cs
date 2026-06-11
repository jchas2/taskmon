using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class Pdh
{
    public const uint ERROR_SUCCESS = 0;
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_RAW_COUNTER {
        public uint CStatus;
        public System.Runtime.InteropServices.ComTypes.FILETIME TimeStamp;
        public long FirstValue;
        public long SecondValue;
        public uint MultiCount;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PDH_RAW_COUNTER_ITEM {
        [MarshalAs(UnmanagedType.LPWStr)] public string szName;
        public PDH_RAW_COUNTER RawValue;
    }

    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern uint PdhOpenQuery(
        string? szDataSource, 
        IntPtr dwUserData, 
        out IntPtr phQuery);
    
    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern uint PdhAddEnglishCounter(
        IntPtr hQuery, 
        string szFullCounterPath, 
        IntPtr dwUserData, 
        out IntPtr phCounter);
    
    [DllImport(Libraries.Pdh)]
    public static extern uint PdhCollectQueryData(IntPtr hQuery);
    
    [DllImport(Libraries.Pdh, CharSet = CharSet.Unicode)]
    public static extern uint PdhGetRawCounterArray(
        IntPtr hCounter, 
        ref uint lpdwBufferSize, 
        ref uint lpdwItemCount, 
        IntPtr ItemBuffer);
    
    [DllImport("pdh.dll")]
    public static extern uint PdhCloseQuery(IntPtr hQuery);
}