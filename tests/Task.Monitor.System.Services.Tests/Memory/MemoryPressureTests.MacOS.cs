#if __APPLE__
using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Tests.Memory;

public sealed class MemoryPressureTests
{
    [Theory]
    [InlineData(0, MemoryPressure.Normal)]
    [InlineData(1, MemoryPressure.Normal)]
    [InlineData(2, MemoryPressure.Warning)]
    [InlineData(4, MemoryPressure.Critical)]
    public void Kernel_Levels_Map_To_Pressure(int level, MemoryPressure expected)
    {
        Assert.Equal(expected, MemoryService.ToMemoryPressure(level));
    }

    [Fact]
    public void The_Current_Pressure_Is_Published()
    {
        Assert.True(global::Task.Monitor.Interop.Mach.Sys.SysctlByNameInt("kern.memorystatus_vm_pressure_level", out int level));
        Assert.True(level is 1 or 2 or 4);
    }
}
#endif
