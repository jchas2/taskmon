#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
    private const string InterfaceCounters        = "sysctl NET_RT_IFLIST2";
    private const string InterfaceCountersFailure = "no interface counters";

    private static unsafe bool TryReadInterfaces(Dictionary<ulong, InterfaceSample> samples)
    {
        ReadOnlySpan<int> name = [
            (int)Sys.Selectors.CTL_NET,
            Sys.PF_ROUTE,
            0,
            Sys.AF_UNSPEC,
            (int)Sys.NetRouting.NET_RT_IFLIST2,
            0
        ];

        byte* buffer = null;
        int length   = 0;

        bool read = Sys.Sysctl(name, ref buffer, ref length);
        using HGlobalScope scope = new((nint)buffer);

        if (!read || buffer == null || length == 0) {
            return false;
        }

        byte* current  = buffer;
        byte* end      = buffer + length;
        int headerSize = Marshal.SizeOf<Sys.if_msghdr2>();

        while (current < end) {
            if (current + sizeof(ushort) > end) {
                break;
            }

            ushort msgLen = *(ushort*)current;

            if (msgLen == 0 || current + msgLen > end) {
                break;
            }

            // Byte 3 is ifm_type; RTM_IFINFO2 rows carry the per-interface statistics.
            if (current + 4 <= end && *(current + 3) == Sys.RTM_IFINFO2 && current + headerSize <= end) {
                Sys.if_msghdr2* ifMsg = (Sys.if_msghdr2*)current;

                samples[ifMsg->ifm_index] = new InterfaceSample(
                    ifMsg->ifm_data.ifi_obytes,
                    ifMsg->ifm_data.ifi_ibytes,
                    ifMsg->ifm_data.ifi_opackets,
                    ifMsg->ifm_data.ifi_ipackets,
                    ifMsg->ifm_data.ifi_ierrors + ifMsg->ifm_data.ifi_oerrors,
                    ifMsg->ifm_data.ifi_iqdrops);
            }

            current += msgLen;
        }

        return samples.Count > 0;
    }
}
#endif
