#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Common;

// An ADL session and the AMD adapters it reports.
internal sealed class AdlAdapters : IDisposable
{
    private nint context;

    public delegate bool TryReadValue(nint context, int adapterIndex, out double value);

    public void Dispose()
    {
        if (context != nint.Zero) {
            Adl.Destroy(context);
            context = nint.Zero;
        }
    }

    public IEnumerable<(string ComponentId, double Value)> Read(IReadOnlyList<GpuDevice> gpus, TryReadValue read)
    {
        if (context == nint.Zero) {
            yield break;
        }

        GpuDeviceMatcher matcher = new(gpus);

        foreach (Adl.Adapter adapter in Adl.GetAdapters(context)) {
            if (!read(context, adapter.AdapterIndex, out double value)) {
                continue;
            }

            GpuDevice? gpu = matcher.Match(adapter.VendorId, adapter.DeviceId);

            yield return (gpu?.AdapterLuid.ToString() ?? $"adl{adapter.AdapterIndex}", value);
        }
    }

    public bool TryOpen()
    {
        context = nint.Zero;

        try {
            Adl.TryCreate(out context);
        }
        catch (DllNotFoundException) {
            // There's no AMD driver installed.
            context = nint.Zero;
        }

        return context != nint.Zero;
    }
}
#endif
