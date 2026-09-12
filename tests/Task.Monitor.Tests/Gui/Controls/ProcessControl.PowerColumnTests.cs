using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessControlPowerColumnTests
{
    private const ulong TotalPhysicalMemory = 8UL * 1024 * 1024 * 1024;

    private static readonly Task.Monitor.Configuration.AppConfig AppConfig =
        new RunContextHelper().GetRunContext().AppConfig;

    private static ProcessEntry Entry(ProcessPowerBucket bucket) =>
        new() { Pid = 1, FileDescription = "test.exe", CmdLine = "test", PowerBucket = bucket };

    [Theory]
    [InlineData(ProcessPowerBucket.VeryLow, "Very Low")]
    [InlineData(ProcessPowerBucket.Low, "Low")]
    [InlineData(ProcessPowerBucket.Moderate, "Moderate")]
    [InlineData(ProcessPowerBucket.High, "High")]
    [InlineData(ProcessPowerBucket.VeryHigh, "Very High")]
    public void Renders_The_Power_Bucket_As_Text(ProcessPowerBucket bucket, string expected)
    {
        ProcessControl.ProcessListViewItem item = new(Entry(bucket), TotalPhysicalMemory, AppConfig);

        Assert.Equal(expected, item.SubItems[(int)ProcessControl.Columns.Power].Text);
    }

    [Fact]
    public void UpdateSubItems_Refreshes_The_Power_Bucket()
    {
        ProcessControl.ProcessListViewItem item = new(Entry(ProcessPowerBucket.Low), TotalPhysicalMemory, AppConfig);

        item.UpdateSubItems(Entry(ProcessPowerBucket.VeryHigh), TotalPhysicalMemory);

        Assert.Equal("Very High", item.SubItems[(int)ProcessControl.Columns.Power].Text);
    }
}
