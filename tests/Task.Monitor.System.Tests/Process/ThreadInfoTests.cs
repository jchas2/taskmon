using System.Reflection;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Tests.Process;

public sealed class ThreadInfoTests
{
    [Fact]
    public void InputBox_Canary_Test() =>
        Assert.Equal(8, CanaryTestHelper.GetPropertyCount<ThreadInfo>());

    [Fact]
    public void ThreadInfo_Write_Read_Test()
    {
        ThreadInfo threadInfo = new() {
            ThreadId = 1,
            CpuKernelTime = TimeSpan.MaxValue,
            CpuTotalTime = TimeSpan.MaxValue,
            CpuUserTime = TimeSpan.MaxValue,
            Priority = 8,
            Reason = "ExecutionDelay",
            StartAddress = long.MaxValue,
            ThreadState = "Wait"
        };
        
        Assert.Equal(1, threadInfo.ThreadId);
        Assert.Equal(TimeSpan.MaxValue, threadInfo.CpuKernelTime);
        Assert.Equal(TimeSpan.MaxValue, threadInfo.CpuTotalTime);
        Assert.Equal(TimeSpan.MaxValue, threadInfo.CpuUserTime);
        Assert.Equal(8, threadInfo.Priority);
        Assert.Equal("ExecutionDelay", threadInfo.Reason);
        Assert.Equal(long.MaxValue, threadInfo.StartAddress);
        Assert.Equal("Wait", threadInfo.ThreadState);
    }
}
 