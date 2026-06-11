using System.Reflection;
using Task.Monitor.Gui.Controls;

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
            { ProcessControl.Columns.CommandLine, "PATH" },
            { ProcessControl.Columns.Count, "" }
        };

    [Theory]
    [MemberData(nameof(ColumnTitleData))]
    public void Column_Has_ColumnTitle_Attribute_With_Expected_Value(
        ProcessControl.Columns column,
        string expectedTitle)
    {
        ProcessControl.ColumnTitleAttribute? attr = GetAttribute<ProcessControl.ColumnTitleAttribute>(column);

        Assert.NotNull(attr);
        Assert.Equal(expectedTitle, attr.Title);
    }

    [Fact]
    public void Priority_Has_ColumnTitle_Attribute()
    {
        ProcessControl.ColumnTitleAttribute? attr = GetAttribute<ProcessControl.ColumnTitleAttribute>(ProcessControl.Columns.Priority);

        Assert.NotNull(attr);
        Assert.True(attr.Title == "PRI" || attr.Title == "NI");
    }

    public static TheoryData<ProcessControl.Columns, string> ColumnPropertyData()
        => new()
        {
            { ProcessControl.Columns.Process, "FileDescription" },
            { ProcessControl.Columns.Pid, "Pid" },
            { ProcessControl.Columns.User, "UserName" },
            { ProcessControl.Columns.Priority, "BasePriority" },
            { ProcessControl.Columns.Cpu, "CpuTimePercent" },
            { ProcessControl.Columns.Threads, "ThreadCount" },
            { ProcessControl.Columns.Memory, "UsedMemory" },
            { ProcessControl.Columns.Disk, "DiskUsage" },
            { ProcessControl.Columns.CommandLine, "CmdLine" },
            { ProcessControl.Columns.Count, "" }
        };

    [Theory]
    [MemberData(nameof(ColumnPropertyData))]
    public void Column_Has_ColumnProperty_Attribute_With_Expected_Value(
        ProcessControl.Columns column,
        string expectedProperty)
    {
        ProcessControl.ColumnPropertyAttribute? attr = GetAttribute<ProcessControl.ColumnPropertyAttribute>(column);

        Assert.NotNull(attr);
        Assert.Equal(expectedProperty, attr.Property);
    }

    public static TheoryData<ProcessControl.Columns, ConsoleKey> ColumnSortKeyData()
        => new()
        {
            { ProcessControl.Columns.Pid, ConsoleKey.N },
            { ProcessControl.Columns.User, ConsoleKey.U },
            { ProcessControl.Columns.Cpu, ConsoleKey.P },
            { ProcessControl.Columns.Memory, ConsoleKey.M },
            { ProcessControl.Columns.Disk, ConsoleKey.D }
        };

    [Theory]
    [MemberData(nameof(ColumnSortKeyData))]
    public void Column_Has_ColumnSortKey_Attribute_With_Expected_Value(
        ProcessControl.Columns column,
        ConsoleKey expectedKey)
    {
        ProcessControl.ColumnSortKeyAttribute? attr = GetAttribute<ProcessControl.ColumnSortKeyAttribute>(column);

        Assert.NotNull(attr);
        Assert.Equal(expectedKey, attr.Key);
    }

    public static TheoryData<ProcessControl.Columns> ColumnsWithoutSortKeyData()
        => new()
        {
            ProcessControl.Columns.Process,
            ProcessControl.Columns.Priority,
            ProcessControl.Columns.Threads,
            ProcessControl.Columns.CommandLine,
            ProcessControl.Columns.Count
        };

    [Theory]
    [MemberData(nameof(ColumnsWithoutSortKeyData))]
    public void Column_Does_Not_Have_ColumnSortKey_Attribute(ProcessControl.Columns column)
    {
        ProcessControl.ColumnSortKeyAttribute? attr = GetAttribute<ProcessControl.ColumnSortKeyAttribute>(column);

        Assert.Null(attr);
    }

    private static T? GetAttribute<T>(ProcessControl.Columns column) where T : Attribute
    {
        MemberInfo memberInfo = typeof(ProcessControl.Columns).GetMember(column.ToString())[0];
        return memberInfo.GetCustomAttribute<T>();
    }
}
