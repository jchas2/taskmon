using Task.Monitor.Gui.Controls.Performance;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformanceControlPanelKeyTests
{
    [Fact]
    public void AppendDeviceKeys_No_Devices_Adds_The_Placeholder_Key()
    {
        List<string> keys = new();

        PerformanceControl.AppendDeviceKeys(keys, "gpu", deviceIds: null);

        Assert.Equal(["gpu"], keys);
    }

    [Fact]
    public void AppendDeviceKeys_Empty_Device_List_Adds_The_Placeholder_Key()
    {
        List<string> keys = new();

        PerformanceControl.AppendDeviceKeys(keys, "disk", []);

        Assert.Equal(["disk"], keys);
    }

    [Fact]
    public void AppendDeviceKeys_Single_Device_Gets_Its_Own_Key()
    {
        List<string> keys = new();

        PerformanceControl.AppendDeviceKeys(keys, "disk", ["0"]);

        Assert.Equal(["disk:0"], keys);
    }

    [Fact]
    public void AppendDeviceKeys_Multiple_Devices_Get_One_Key_Each()
    {
        List<string> keys = new();

        PerformanceControl.AppendDeviceKeys(keys, "net", ["111", "222", "333"]);

        Assert.Equal(["net:111", "net:222", "net:333"], keys);
    }
}
