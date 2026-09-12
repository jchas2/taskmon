using Moq;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class GpuPerformanceControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public GpuPerformanceControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static GpuInfo BuildGpuInfoWithDevices() =>
        new() {
            Specs = new GpuSpecs {
                Devices =
                [
                    new GpuDevice {
                        Index = 0,
                        AdapterLuid = 111,
                        Vendor = "NVIDIA",
                        Description = "NVIDIA GeForce RTX 4080",
                        AdapterType = "Discrete",
                        DriverVersion = "560.94",
                        DriverDate = "2024-08-01",
                    },
                    new GpuDevice {
                        Index = 1,
                        AdapterLuid = 222,
                        Vendor = "Intel",
                        Description = "Intel UHD Graphics 770",
                        AdapterType = "Integrated",
                        DriverVersion = "31.0.101.5333",
                        DriverDate = "2024-03-15",
                    },
                ],
            },
            Metrics = new GpuMetrics {
                GpuPercentTime = 0.5,
                Devices =
                [
                    new GpuDeviceMetrics { Index = 0, AdapterLuid = 111, GpuPercentTime = 0.5 },
                    new GpuDeviceMetrics { Index = 1, AdapterLuid = 222, GpuPercentTime = 0.1 },
                ],
            },
        };

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        GpuPerformanceControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Scoped_To_An_Adapter_Draws_Its_Specs_Rows()
    {
        GpuPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.SetScope(222);
        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Gpu = BuildGpuInfoWithDevices() });
        ctrl.Draw();

        foreach (string key in new[] {
            "Vendor:", "Description:", "Adapter Type:", "Driver Version:", "Driver Date:" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(key))), Times.AtLeastOnce);
        }

        // The Intel adapter's values, not the NVIDIA one's.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Intel UHD Graphics 770"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Integrated"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("31.0.101.5333"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("NVIDIA GeForce RTX 4080"))), Times.Never);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Scoped_To_An_Adapter_Draws_Its_Power_Draw()
    {
        GpuPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.SetScope(111);
        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot {
            Gpu = BuildGpuInfoWithDevices(),
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [
                        new PowerReading {
                            Component = PowerComponent.Gpu, ComponentId = "111", Rail = "GPU",
                            Watts = 123.4, Source = PowerSource.Nvml
                        }
                    ]
                }
            }
        });
        ctrl.Draw();

        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Power Draw"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("123.4 W"))), Times.AtLeastOnce);

        ctrl.Unload();
    }
}
