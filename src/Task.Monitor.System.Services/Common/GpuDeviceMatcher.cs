using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Common;

internal sealed class GpuDeviceMatcher(IReadOnlyList<GpuDevice> gpus)
{
    private readonly HashSet<long> assigned = [];

    // The first unassigned device with these ids, or null when there is none left.
    public GpuDevice? Match(uint vendorId, uint deviceId)
    {
        foreach (GpuDevice candidate in gpus) {
            if (candidate.VendorId == vendorId &&
                candidate.DeviceId == deviceId &&
                assigned.Add(candidate.AdapterLuid)) {

                return candidate;
            }
        }

        return null;
    }
}
