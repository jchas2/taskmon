using Task.Monitor.Gui.Controls.Processes;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ColumnsEnumTests
{
    public static TheoryData<ProcessControl.Columns, string> ColumnTitleData()
        => new()
        {
            { ProcessControl.Columns.Process, "PROCESS" },
            { ProcessControl.Columns.Pid, "PID" },
            { ProcessControl.Columns.User, "USER" },
            { ProcessControl.Columns.Cpu, "CPU%" },
            { ProcessControl.Columns.Threads, "THRDS" },
            { ProcessControl.Columns.Memory, "MEM" },
            { ProcessControl.Columns.Disk, "DISK" },
            { ProcessControl.Columns.Power, "POWER" },
            { ProcessControl.Columns.CommandLine, "PATH" },
            { ProcessControl.Columns.Count, "" }
        };

    // The titles are the British defaults of their Strings.resx entries.
    [Theory]
    [MemberData(nameof(ColumnTitleData))]
    public void Column_Has_The_Expected_Title(ProcessControl.Columns column, string expectedTitle) =>
        Assert.Equal(expectedTitle, ProcessControl.Title(column));

    [Fact]
    public void Every_Column_Has_A_Title()
    {
        for (ProcessControl.Columns column = ProcessControl.Columns.Process; column < ProcessControl.Columns.Count; column++) {
            Assert.False(string.IsNullOrEmpty(ProcessControl.Title(column)), column.ToString());
        }
    }

    [Fact]
    public void Priority_Has_The_Platform_Title() =>
        Assert.True(ProcessControl.Title(ProcessControl.Columns.Priority) is "PRI" or "NI");
}
