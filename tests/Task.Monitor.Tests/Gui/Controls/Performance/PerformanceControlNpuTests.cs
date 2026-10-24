using System.Reflection;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformanceControlNpuTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public PerformanceControlNpuTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void DesiredPanelKeys_Adds_No_Npu_Panel_Without_A_Device()
    {
        Assert.DoesNotContain(PerformanceControl.DesiredPanelKeys(new SystemSnapshot()), key => key.StartsWith("npu"));
    }

    [Fact]
    public void DesiredPanelKeys_Puts_Each_Npu_After_The_Gpu()
    {
        List<string> keys = PerformanceControl.DesiredPanelKeys(SnapshotWithNpu());

        Assert.Equal(["cpu", "memory", "gpu", "npu:0", "disk", "net"], keys);
    }

    [Fact]
    public void Draws_An_Npu_Panel_Titled_Like_The_Gpu()
    {
        PerformanceControl ctrl = new(runContext.ServiceController, new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = 160,
            Height = 50
        };

        ctrl.Load();
        ctrl.Resize();
        RaiseSnapshotUpdated(SnapshotWithNpu());

        string output = CapturedOutput();

        Assert.Contains("NPU 0  Apple Neural Engine", output);
        Assert.Contains("16 cores", output);
        Assert.Contains("(1.3 W)", output);

        ctrl.Unload();
    }

    // ServiceController only raises this from its worker loop.
    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    internal static SystemSnapshot SnapshotWithNpu() =>
        new() {
            Npu = new NpuInfo {
                Specs   = new NpuSpecs { Devices = [new NpuDevice { Index = 0, Description = "Apple Neural Engine", Vendor = "Apple", Cores = 16, Architecture = "h15g", Version = "144.17" }] },
                Metrics = new NpuMetrics { Devices = [new NpuDeviceMetrics { Index = 0, ActiveRatio = 0.5 }] }
            },
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [new PowerReading { Component = PowerComponent.NeuralEngine, Rail = "Neural Engine", Watts = 1.3 }]
                }
            }
        };
}
