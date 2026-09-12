using Moq;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class DiskPerformanceControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public DiskPerformanceControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static DiskInfo BuildDiskInfo() =>
        new() {
            Specs = new DiskSpecs {
                Devices = [
                    new DiskDevice { Index = 0, Capacity = 2L * 1024 * 1024 * 1024 * 1024 },
                    new DiskDevice { Index = 1, Capacity = 1L * 1024 * 1024 * 1024 * 1024 },
                ],
            },
            Metrics = new DiskMetrics {
                PercentActiveTime = 42.5,
                ReadBytesPerSecond = 12.0 * 1024 * 1024,
                WriteBytesPerSecond = 3.5 * 1024 * 1024,
                ReadMegabytesPerSecond = 12.0,
                WriteMegabytesPerSecond = 3.5,
                TotalBytesRead = 98324528934,
                TotalBytesWritten = 21983484324,
            },
        };

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        DiskPerformanceControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Should_Draw_Combined_Disk_Metrics()
    {
        // Wrap the mock terminal so the Chart's ReadOnlySpan<char> blit doesn't hit the Moq
        // proxy (which can't proxy ref-struct params); other writes still forward to the mock.
        DiskPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Disk = BuildDiskInfo() });
        ctrl.Draw();

        // The three list view headers the bottom third reports on.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Active Time"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Read Speed"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Write Speed"))), Times.AtLeastOnce);

        // 42.5% active, 12.0 MB/s read and 3.5 MB/s write.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("042.5%"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("12.0 MB/s"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("3.5 MB/s"))), Times.AtLeastOnce);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Should_Draw_Disk_Specs_Keys_And_Values()
    {
        DiskPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Disk = BuildDiskInfo() });
        ctrl.Draw();

        foreach (string key in new[] {
            "Number of Disks:",
            "Total Capacity:",
            "Total Bytes Read:",
            "Total Bytes Written:" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(key))), Times.AtLeastOnce);
        }

        // Two disks totalling 3 TB of raw capacity.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("3.0 TB"))), Times.AtLeastOnce);

        // 98324528934 bytes read and 21983484324 written.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("91.6 GB"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("20.5 GB"))), Times.AtLeastOnce);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    private static DiskInfo BuildDiskInfoWithDevices()
    {
        DiskInfo info = BuildDiskInfo();

        info.Metrics.Devices =
        [
            new DiskDeviceMetrics {
                Index = 0,
                InstanceName = "0 C:",
                PercentActiveTime = 90.0,
                ReadBytesPerSecond = 40.0 * 1024 * 1024,
                WriteBytesPerSecond = 0.0,
                TotalBytesRead = 1_000_000,
                TotalBytesWritten = 2_000_000,
            },
            new DiskDeviceMetrics {
                Index = 1,
                InstanceName = "1 D:",
                PercentActiveTime = 5.0,
                ReadBytesPerSecond = 0.0,
                WriteBytesPerSecond = 7.0 * 1024 * 1024,
                TotalBytesRead = 3_000_000,
                TotalBytesWritten = 4_000_000,
            },
        ];

        info.Specs.Devices[0].Model = "Samsung SSD 990";

        info.Specs.Devices[1].Model = "WD Blue";
        info.Specs.Devices[1].Manufacturer = "Western Digital";
        info.Specs.Devices[1].FirmwareRevision = "01.01A01";
        info.Specs.Devices[1].SerialNumber = "WD-WCC7K1234567";
        info.Specs.Devices[1].BusType = "SATA";
        info.Specs.Devices[1].MediaType = "HDD";
        info.Specs.Devices[1].IsRemovable = false;
        info.Specs.Devices[1].Capacity = 1L * 1024 * 1024 * 1024 * 1024;

        return info;
    }

    [Fact]
    public void Scoped_To_A_Disk_Draws_That_Disk_Only()
    {
        DiskPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.SetScope(1);
        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Disk = BuildDiskInfoWithDevices() });
        ctrl.Draw();

        // Disk 1: 5.0% active, 7.0 MB/s write, and the scoped specs rows.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("005.0%"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("7.0 MB/s"))), Times.AtLeastOnce);

        foreach (string key in new[] {
            "Model:", "Manufacturer:", "Firmware Revision:", "Serial Number:",
            "Bus Type:", "Media Type:", "Capacity:", "Removable:" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(key))), Times.AtLeastOnce);
        }

        foreach (string value in new[] {
            "WD Blue", "Western Digital", "01.01A01", "WD-WCC7K1234567", "SATA", "HDD" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(value))), Times.AtLeastOnce);
        }

        // Disk 0's 90% active time must not leak into a disk-1 view.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("090.0%"))), Times.Never);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Number of Disks:"))), Times.Never);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Scoped_To_A_Disk_Draws_Its_Rated_Power()
    {
        DiskPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.SetScope(0);
        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot {
            Disk = BuildDiskInfoWithDevices(),
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [
                        new PowerReading {
                            Component = PowerComponent.Disk, ComponentId = "0", Rail = "Drive",
                            Watts = 5.5, IsRated = true, Source = PowerSource.NvmeRated
                        }
                    ]
                }
            }
        });
        ctrl.Draw();

        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Power"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("5.5 W (rated)"))), Times.AtLeastOnce);

        ctrl.Unload();
    }

    [Fact]
    public void Should_Not_Draw_Before_A_Snapshot_Arrives()
    {
        DiskPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("MB/s"))), Times.Never);

        ctrl.Unload();
    }
}
