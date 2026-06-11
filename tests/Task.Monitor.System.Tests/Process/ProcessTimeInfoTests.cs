using System.Diagnostics;
using System.Reflection;
using Task.Monitor.System.Process;

namespace Task.Monitor.System.Tests.Process;

public sealed class ProcessTimeInfoTests
{
    [Fact]
    public void ProcessTimeInfo_Canary_Test()
    {
        // If this fails, review all tests in this class                                                                                   
        // and update the expected count after adding tests                                                                                
        const int ExpectedFieldCount = 3;

        int actualCount = typeof(ProcessTimeInfo).GetFields(
            BindingFlags.Public | BindingFlags.Instance).Length;

        Assert.Equal(ExpectedFieldCount, actualCount);
    }
    
    [Fact]
    public void Should_Clear_All_Properties()
    {
        const int FieldsTested = 3;

        var fields = typeof(ProcessTimeInfo).GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(FieldsTested, fields.Length);

        var processTimeInfo = new ProcessTimeInfo() {
            DiskOperations = 78346578346,
            KernelTime = 38346510925,
            UserTime = 72346378346
        };
        
        processTimeInfo.Clear();
        
        Assert.True(processTimeInfo.DiskOperations == 0);
        Assert.True(processTimeInfo.KernelTime == 0);
        Assert.True(processTimeInfo.UserTime == 0);
    }
}