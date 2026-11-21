#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
    private const string InterfaceCounters        = nameof(IpHlpApi.GetIfTable2);
    private const string InterfaceCountersFailure = "see the error above";

    private unsafe delegate void InterfaceRowReader(byte* row);

    // Calls read with each MIB_IF_ROW2 in the interface table.
    private static unsafe bool ReadInterfaceTable(string caller, InterfaceRowReader read)
    {
        nint table  = nint.Zero;
        uint result = IpHlpApi.GetIfTable2(&table);

        if (result != IpHlpApi.ERROR_SUCCESS || table == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                $"{nameof(IpHlpApi.GetIfTable2)} {caller}",
                $"Failed {caller}",
                result);

            return false;
        }

        try {
            byte* buffer    = (byte*)table;
            uint entryCount = InteropHelper.ReadUInt32(buffer, NetIoApi.IfTable2NumEntriesOffset);

            for (uint entry = 0; entry < entryCount; entry++) {
                read(buffer + NetIoApi.IfTable2TableOffset + entry * (uint)NetIoApi.IfRow2Size);
            }

            return true;
        }
        finally {
            IpHlpApi.FreeMibTable(table);
        }
    }

    private static unsafe bool TryReadInterfaces(Dictionary<ulong, InterfaceSample> samples) =>
        ReadInterfaceTable(nameof(TryReadInterfaces), row =>
            samples[InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InterfaceLuidOffset)] = new InterfaceSample(
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutOctetsOffset),
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InOctetsOffset),
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutUcastPktsOffset),
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InUcastPktsOffset),
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InErrorsOffset) +
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutErrorsOffset),
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InDiscardsOffset) +
                InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutDiscardsOffset)));
}
#endif
