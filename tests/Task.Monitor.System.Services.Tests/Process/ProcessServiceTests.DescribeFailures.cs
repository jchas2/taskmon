using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed partial class ProcessServiceTests
{
    // One summary line replaces a line per process, so the most common error leads.
    [Fact]
    public void DescribeFailures_Lists_The_Most_Frequent_Error_First()
    {
        Dictionary<int, int> failures = new() { [87] = 2, [5] = 21 };

        Assert.Equal(
            $"{ErrorCodes.Describe(5)} x21, {ErrorCodes.Describe(87)} x2",
            ProcessService.DescribeFailures(failures));
    }

    [Fact]
    public void DescribeFailures_Orders_Equal_Counts_By_Error()
    {
        Dictionary<int, int> failures = new() { [87] = 3, [5] = 3 };

        Assert.Equal(
            $"{ErrorCodes.Describe(5)} x3, {ErrorCodes.Describe(87)} x3",
            ProcessService.DescribeFailures(failures));
    }
}
