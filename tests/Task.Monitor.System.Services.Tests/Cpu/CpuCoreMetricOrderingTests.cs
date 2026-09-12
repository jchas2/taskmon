using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.System.Services.Tests.Cpu;

public sealed class CpuCoreMetricOrderingTests
{
    private static string[] Sorted(params string[] names)
    {
        string[] copy = (string[])names.Clone();
        Array.Sort(copy, CpuCoreMetricOrdering.Compare);
        return copy;
    }

    [Fact]
    public void Should_Order_Core_Names_By_Integer_Value() =>
        Assert.Equal(
            ["0", "1", "2", "3", "10", "11"],
            Sorted("0", "1", "10", "11", "2", "3"));

    [Fact]
    public void Should_Leave_Already_Ordered_Single_Digit_Names_Untouched() =>
        Assert.Equal(
            ["0", "1", "2", "3"],
            Sorted("0", "1", "2", "3"));

    [Fact]
    public void Should_Sort_A_Non_Numeric_Name_After_The_Numeric_Ones() =>
        Assert.Equal(
            ["0", "2", "10", "0,0"],
            Sorted("10", "0,0", "0", "2"));

    [Fact]
    public void Should_Compare_Two_Non_Numeric_Names_Ordinally() =>
        Assert.True(CpuCoreMetricOrdering.Compare("0,1", "0,2") < 0);

    [Fact]
    public void Should_Report_Equal_Core_Names_As_Equal() =>
        Assert.Equal(0, CpuCoreMetricOrdering.Compare("5", "5"));
}
