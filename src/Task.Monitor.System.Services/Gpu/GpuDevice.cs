namespace Task.Monitor.System.Services.Gpu;

// One installed display adapter, as reported by IDXGIFactory1::EnumAdapters1 / GetDesc1.
// AdapterLuid is the join key back to the \GPU Engine(*) and \GPU Adapter Memory(*) counter
// instances (their luid_0xHIGH_0xLOW token) and to D3DKMTQueryStatistics, the same way
// DiskDevice.Index joins to the PhysicalDisk counter instances.
public sealed class GpuDevice
{
    public int    Index                 { get; set; }
    public long   AdapterLuid           { get; set; }
    public string Description           { get; set; } = GpuDeviceParser.NotAvailable;
    public string Vendor                { get; set; } = GpuDeviceParser.NotAvailable;
    public uint   VendorId              { get; set; }
    public uint   DeviceId              { get; set; }
    public uint   SubSysId              { get; set; }
    public uint   Revision              { get; set; }

    // Discrete, Integrated, Software or Virtual - see GpuDeviceParser.DecodeAdapterType.
    public string AdapterType           { get; set; } = GpuDeviceParser.NotAvailable;

    // From DXGI_ADAPTER_DESC1, in bytes.
    public long   DedicatedVideoMemory  { get; set; }
    public long   DedicatedSystemMemory { get; set; }
    public long   SharedSystemMemory    { get; set; }

    // Best effort enrichment from the display class key, matched by PCI VEN_/DEV_ id rather than
    // by subkey position. "N/A" when the Registry has no entry for this adapter.
    public string DriverVersion         { get; set; } = GpuDeviceParser.NotAvailable;
    public string DriverDate            { get; set; } = GpuDeviceParser.NotAvailable;
}
