using System.Reflection;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Tests.Process;

public sealed class ModuleInfoTests
{
    [Fact]
    public void ModuleInfo_Canary_Test() =>
        Assert.Equal(2, CanaryTestHelper.GetPropertyCount<ModuleInfo>());

    [Fact]
    public void ModuleInfo_Write_Read_Test()
    {
        ModuleInfo moduleInfo = new() {
            ModuleName = "test",
            FileName = "test.dll"
        };
        
        Assert.Equal("test", moduleInfo.ModuleName);
        Assert.Equal("test.dll", moduleInfo.FileName);
    }
}