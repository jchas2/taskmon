namespace Task.Monitor.System.Services.Gpu;

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

    public string AdapterType           { get; set; } = GpuDeviceParser.NotAvailable;

    public long   DedicatedVideoMemory  { get; set; }
    public long   DedicatedSystemMemory { get; set; }
    public long   SharedSystemMemory    { get; set; }

    public string DriverVersion         { get; set; } = GpuDeviceParser.NotAvailable;
    public string DriverDate            { get; set; } = GpuDeviceParser.NotAvailable;
}
