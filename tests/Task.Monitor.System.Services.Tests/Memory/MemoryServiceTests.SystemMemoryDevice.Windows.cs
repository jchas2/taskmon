#if __WIN32__
using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Tests.Memory;

public sealed partial class MemoryServiceTests
{
    // The stand-in for firmware that lists no memory devices: one device holding the total.
    [Fact]
    public void SystemMemoryDevice_Holds_The_Total_As_One_Device()
    {
        MemoryDevice device = MemoryService.SystemMemoryDevice(32UL * 1024 * 1024 * 1024);

        Assert.Equal(32768u, device.SizeInMegabytes);
        Assert.Equal("System Memory", device.DeviceLocator);
        Assert.Equal("Unknown", device.MemoryType);
        Assert.Equal("Unknown", device.FormFactor);
        Assert.Equal("N/A", device.Manufacturer);
        Assert.Equal(0, device.Speed);
    }
}
#endif
