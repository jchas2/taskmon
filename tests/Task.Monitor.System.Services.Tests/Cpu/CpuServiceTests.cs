using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.System.Services.Tests.Cpu;

public sealed class CpuServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void GetCpuName_Returns_A_Name() =>
        Assert.False(string.IsNullOrWhiteSpace(CpuService.GetCpuName()));

    // The startup log calls GetCpuName before the services start; the specs must carry the same name.
    [Fact]
    public void Published_Specs_Carry_The_Name_GetCpuName_Returns()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new CpuService { Delay = MinimumDelay });
        controller.Start();

        CpuInfo? cpu = snapshots.WaitFor(published => published.Cpu != null)?.Cpu;
        controller.Stop();

        Assert.NotNull(cpu);
        Assert.Equal(CpuService.GetCpuName(), cpu.Specs.CpuName);
    }

    [Fact]
    public void Should_Run_CpuSystemService()
    {
        CpuService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
