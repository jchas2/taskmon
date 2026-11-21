#if __WIN32__
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Common;

// An ADL session and the AMD adapters it reports.
internal sealed class AdlAdapters : IDisposable
{
    private nint context;

    public delegate bool TryReadValue(nint context, int adapterIndex, out double value);

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        if (context != nint.Zero) {
            AdlLibrary.Destroy(context);
            context = nint.Zero;
        }
    }

    public IEnumerable<(string ComponentId, double Value)> Read(IReadOnlyList<GpuDevice> gpus, TryReadValue read)
    {
        if (context == nint.Zero) {
            yield break;
        }

        GpuDeviceMatcher matcher = new(gpus);

        foreach (AdlLibrary.Adapter adapter in AdlLibrary.GetAdapters(context)) {
            if (!read(context, adapter.AdapterIndex, out double value)) {
                continue;
            }

            GpuDevice? gpu = matcher.Match(adapter.VendorId, adapter.DeviceId);

            yield return (gpu?.AdapterLuid.ToString() ?? $"adl{adapter.AdapterIndex}", value);
        }
    }

    public bool TryOpen()
    {
        bool opened = AdlLibrary.TryCreate(out context, out string diagnostic);

        Diagnostic = diagnostic;

        return opened;
    }
}
#endif
