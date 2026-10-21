using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class GpuDeviceMatcherTests
{
    private const uint Amd    = 0x1002;
    private const uint Nvidia = 0x10DE;

    private static GpuDevice Device(long luid, uint vendorId, uint deviceId) =>
        new() { AdapterLuid = luid, VendorId = vendorId, DeviceId = deviceId };

    [Fact]
    public void Match_Hands_Each_Device_Out_Once()
    {
        // Two identical cards: each library GPU must get its own device, not both the first.
        GpuDeviceMatcher matcher = new([Device(1, Nvidia, 0x2684), Device(2, Nvidia, 0x2684)]);

        Assert.Equal(1, matcher.Match(Nvidia, 0x2684)?.AdapterLuid);
        Assert.Equal(2, matcher.Match(Nvidia, 0x2684)?.AdapterLuid);
        Assert.Null(matcher.Match(Nvidia, 0x2684));
    }

    [Fact]
    public void Match_Requires_The_Vendor_As_Well_As_The_Device()
    {
        GpuDeviceMatcher matcher = new([Device(1, Amd, 0x73BF)]);

        Assert.Null(matcher.Match(Nvidia, 0x73BF));
        Assert.Equal(1, matcher.Match(Amd, 0x73BF)?.AdapterLuid);
    }

    [Fact]
    public void Match_Returns_Null_When_No_Device_Has_The_Ids()
    {
        Assert.Null(new GpuDeviceMatcher([Device(1, Amd, 0x73BF)]).Match(Amd, 0x1234));
    }
}
