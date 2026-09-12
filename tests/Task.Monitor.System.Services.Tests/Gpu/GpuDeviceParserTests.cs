using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Tests.Gpu;

public sealed class GpuDeviceParserTests
{
    [Theory]
    [InlineData(0x10DEu, "NVIDIA")]
    [InlineData(0x1002u, "AMD")]
    [InlineData(0x1022u, "AMD")]
    [InlineData(0x8086u, "Intel")]
    [InlineData(0x1414u, "Microsoft")]
    [InlineData(0x15ADu, "VMware")]
    [InlineData(0xBEEFu, GpuDeviceParser.NotAvailable)]
    public void DecodeVendor_Maps_Known_Pci_Vendor_Ids(uint vendorId, string expected) =>
        Assert.Equal(expected, GpuDeviceParser.DecodeVendor(vendorId));

    [Fact]
    public void DecodeAdapterType_Flags_A_Software_Adapter() =>
        Assert.Equal(
            "Software",
            GpuDeviceParser.DecodeAdapterType(dxgiFlags: 2, vendorId: 0x1414, dedicatedVideoMemory: 0));

    [Fact]
    public void DecodeAdapterType_Treats_A_Large_Vram_Adapter_As_Discrete() =>
        Assert.Equal(
            "Discrete",
            GpuDeviceParser.DecodeAdapterType(0, 0x10DE, 8L * 1024 * 1024 * 1024));

    [Fact]
    public void DecodeAdapterType_Treats_A_Near_Zero_Vram_Adapter_As_Integrated() =>
        Assert.Equal(
            "Integrated",
            GpuDeviceParser.DecodeAdapterType(0, 0x8086, 128L * 1024 * 1024));

    [Fact]
    public void DecodeAdapterType_Flags_A_Hypervisor_Vendor_As_Virtual() =>
        Assert.Equal(
            "Virtual",
            GpuDeviceParser.DecodeAdapterType(0, 0x15AD, 256L * 1024 * 1024));

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D", 0x0000ABCDL)]
    [InlineData("luid_0x00000001_0x0000ABCD_phys_0_eng_0_engtype_Compute_0", 0x1_0000ABCDL)]
    [InlineData("pid_4_luid_0x0000000A_0x0000000B", 0xA_0000000BL)]
    public void TryParseAdapterLuid_Combines_High_And_Low_Parts(string instanceName, long expected)
    {
        Assert.True(GpuDeviceParser.TryParseAdapterLuid(instanceName, out long luid));
        Assert.Equal(expected, luid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pid_1234_phys_0_eng_3")]
    [InlineData("luid_0x00000000")]
    [InlineData("luid_nothex_0x0")]
    public void TryParseAdapterLuid_Rejects_Anything_Without_Two_Hex_Groups(string instanceName) =>
        Assert.False(GpuDeviceParser.TryParseAdapterLuid(instanceName, out _));

    [Theory]
    [InlineData(@"pci\ven_10de&dev_2482&subsys_40BF1458&rev_a1", 0x10DEu, 0x2482u)]
    [InlineData(@"PCI\VEN_8086&DEV_9A49", 0x8086u, 0x9A49u)]
    public void TryParsePciIds_Reads_Ven_And_Dev(
        string matchingDeviceId,
        uint expectedVendor,
        uint expectedDevice)
    {
        Assert.True(GpuDeviceParser.TryParsePciIds(matchingDeviceId, out uint vendorId, out uint deviceId));
        Assert.Equal(expectedVendor, vendorId);
        Assert.Equal(expectedDevice, deviceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"USB\VID_1234&PID_5678")]
    public void TryParsePciIds_Rejects_A_Non_Pci_Id(string? matchingDeviceId) =>
        Assert.False(GpuDeviceParser.TryParsePciIds(matchingDeviceId, out _, out _));

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D", 1234)]
    [InlineData("pid_0_luid_0x00000000_0x0000ABCD_phys_0_eng_1_engtype_Copy", 0)]
    public void ParsePidFromInstance_Reads_The_Leading_Pid(string instanceName, int expected) =>
        Assert.Equal(expected, GpuDeviceParser.ParsePidFromInstance(instanceName));

    [Theory]
    [InlineData("")]
    [InlineData("engtype_3D")]
    [InlineData("pid_")]
    [InlineData("pid_notanumber_luid_0x0")]
    public void ParsePidFromInstance_Rejects_Anything_Else(string instanceName) =>
        Assert.Equal(-1, GpuDeviceParser.ParsePidFromInstance(instanceName));

    [Fact]
    public void ParseEngineFromInstance_Returns_Everything_After_The_Pid() =>
        Assert.Equal(
            "luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D",
            GpuDeviceParser.ParseEngineFromInstance(
                "pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D"));
}
