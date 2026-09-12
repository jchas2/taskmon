namespace Task.Monitor.System.Services.Gpu;

public sealed class GpuSpecs
{
    // One entry per installed display adapter, from IDXGIFactory1::EnumAdapters1. Empty off
    // Windows and on a machine with no DXGI adapter.
    public List<GpuDevice> Devices { get; set; } = new();

    // No Windows API reports a GPU's shader / CU / CUDA core count, so this stays 0. Kept for API
    // stability and because the header and About screen already guard on it being > 0.
    public int  GpuCores       { get; set; }

    // Sum of Devices[*].DedicatedVideoMemory. The aggregate the charts and summary panel read.
    public long TotalGpuMemory { get; set; }
}
