using System.Security;

namespace Task.Monitor.System.Services.Memory;

// A decoded SMBIOS Type 17 (Memory Device) structure.
public struct MemoryDevice
{
    public uint   SizeInMegabytes      { get; set; }
    public ushort Speed                { get; set; }
    public ushort ConfiguredClockSpeed { get; set; }
    public int    Slot                 { get; set; }
    public string MemoryType           { get; set; } 
    public string FormFactor           { get; set; } 
    public string DeviceLocator        { get; set; } 
    public string BankLocator          { get; set; } 
    public string Manufacturer         { get; set; } 
    public string SerialNumber         { get; set; } 
    public string PartNumber           { get; set; } 
}
