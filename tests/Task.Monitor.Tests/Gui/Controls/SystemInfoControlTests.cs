using Moq;
using Task.Monitor.Gui.Controls.SystemInformation;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class SystemInfoControlTests
{
    // Index into navMenu.MenuItems / the Section enum - keep in sync with SystemInfoControl's nav.
    private const int System = 0;
    private const int Cpu = 1;
    private const int Memory = 2;
    private const int Gpu = 3;
    private const int Disk = 4;
    private const int Network = 5;

    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public SystemInfoControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static SystemSnapshot BuildSnapshot() =>
        new() {
            Cpu = new CpuInfo {
                Specs = new CpuSpecs {
                    CpuName = "AMD Ryzen 9 7950X",
                    CpuCores = 32,
                    CpuFrequency = 4500,
                },
            },
            Memory = new MemoryInfo {
                Specs = new MemorySpecs {
                    Devices = [
                        new MemoryDevice {
                            Slot = 1, DeviceLocator = "DIMM 0", SizeInMegabytes = 16384,
                            MemoryType = "DDR5", FormFactor = "DIMM", Speed = 6000,
                            ConfiguredClockSpeed = 5600, Manufacturer = "G.Skill",
                            PartNumber = "F5-6000J3038F16G", SerialNumber = "00000001",
                            BankLocator = "P0 CHANNEL A",
                        },
                        new MemoryDevice {
                            Slot = 2, DeviceLocator = "DIMM 1", SizeInMegabytes = 16384,
                            MemoryType = "DDR5", FormFactor = "DIMM", Speed = 6000,
                            ConfiguredClockSpeed = 5600, Manufacturer = "G.Skill",
                            PartNumber = "F5-6000J3038F16G", SerialNumber = "00000002",
                            BankLocator = "P0 CHANNEL B",
                        },
                    ],
                },
            },
            Gpu = new GpuInfo {
                Specs = new GpuSpecs {
                    TotalGpuMemory = 16L * 1024 * 1024 * 1024,
                    Devices = [
                        new GpuDevice {
                            Index = 0, Vendor = "NVIDIA", Description = "NVIDIA GeForce RTX 4080",
                            AdapterType = "Discrete", DedicatedVideoMemory = 16L * 1024 * 1024 * 1024,
                            VendorId = 0x10DE, DeviceId = 0x2704,
                            DriverVersion = "560.94", DriverDate = "2024-08-01",
                        },
                        new GpuDevice {
                            Index = 1, Vendor = "Intel", Description = "Intel UHD Graphics 770",
                            AdapterType = "Integrated", VendorId = 0x8086, DeviceId = 0x4680,
                            DriverVersion = "31.0.101.5333", DriverDate = "2024-03-15",
                        },
                    ],
                },
            },
            Disk = new DiskInfo {
                Specs = new DiskSpecs {
                    Devices = [
                        new DiskDevice {
                            Index = 0, Model = "Samsung SSD 990 PRO 2TB", Manufacturer = "Samsung",
                            FirmwareRevision = "4B2QJXD7", SerialNumber = "S1A2B3C4",
                            BusType = "NVMe", MediaType = "SSD", IsRemovable = false,
                            Capacity = 2L * 1024 * 1024 * 1024 * 1024,
                        },
                        new DiskDevice {
                            Index = 1, Model = "WD Elements 25A3", Manufacturer = "Western Digital",
                            FirmwareRevision = "1015", SerialNumber = "WX11A", BusType = "USB",
                            MediaType = "HDD", IsRemovable = true,
                            Capacity = 1L * 1024 * 1024 * 1024 * 1024,
                        },
                    ],
                    UnattachedVolumes = [
                        new DiskVolume {
                            VolumeName = @"\\?\Volume{net-mount}\", Label = "media",
                            FileSystem = "SMB", IsReady = true,
                            FormattedCapacity = 8L * 1024 * 1024 * 1024 * 1024,
                            AvailableFreeSpace = 2L * 1024 * 1024 * 1024 * 1024,
                        },
                    ],
                },
            },
            Network = new NetworkInfo {
                Specs = new NetworkSpecs {
                    Devices = [
                        new NetworkDevice {
                            InterfaceLuid = 111, Name = "{GUID-ETH}", FriendlyName = "Ethernet",
                            Description = "Realtek Gaming 2.5GbE Family Controller",
                            ConnectionType = "Ethernet", PhysicalMedium = "802.3",
                            MacAddress = "AA-BB-CC-DD-EE-01",
                            IPv4Addresses = ["192.168.1.10"], IPv6Addresses = ["fe80::1"],
                            TransmitLinkSpeed = 2_500_000_000, ReceiveLinkSpeed = 2_500_000_000,
                            OperationalStatus = "Up",
                        },
                        new NetworkDevice {
                            InterfaceLuid = 222, Name = "{GUID-WIFI}", FriendlyName = "Wi-Fi",
                            Description = "Intel Wi-Fi 6E AX211 160MHz",
                            ConnectionType = "Wi-Fi", PhysicalMedium = "Native 802.11",
                            MacAddress = "AA-BB-CC-DD-EE-02",
                            IPv4Addresses = ["192.168.1.20", "192.168.1.21"],
                            IPv6Addresses = ["fe80::2"],
                            TransmitLinkSpeed = 1_200_000_000, ReceiveLinkSpeed = 1_200_000_000,
                            OperationalStatus = "Up",
                        },
                    ],
                },
            },
        };

    private SystemInfoControl CreateControl(int width = 120, int height = 240)
    {
        SystemInfoControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    // SetFocus() is a no-op without a parent Screen to route through (GetParentScreen() finds
    // nothing to call FocusInternal on), so the focus-routing tests need one, unlike CreateControl.
    private SystemInfoControl CreateFocusableControl(int width = 120, int height = 240)
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = width, Height = height };

        SystemInfoControl ctrl = new(
            runContext.ServiceController,
            terminal,
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        SystemInfoControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Nav_Shows_All_Six_Section_Labels_Regardless_Of_Selection()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.Sample(BuildSnapshot());
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string label in new[] { "SYSTEM", "CPU", "MEMORY", "GPU", "DISK", "NETWORK" }) {
            Assert.Contains(label, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_System_Section_Without_A_Snapshot()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("SYSTEM INFORMATION", output);
        Assert.Contains(Environment.MachineName.ToUpper(), output);

        ctrl.Unload();
    }

    [Fact]
    public void Selecting_A_Section_Shows_Only_That_Sections_Content()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.Sample(BuildSnapshot());

        ctrl.SelectSectionForTests(Cpu);
        ctrl.Draw();
        string cpuOutput = CapturedOutput();

        Assert.Contains("Processor:", cpuOutput);
        Assert.Contains("AMD Ryzen 9 7950X", cpuOutput);
        Assert.DoesNotContain("MAC Address:", cpuOutput);
        Assert.DoesNotContain("Part Number:", cpuOutput);

        runContextHelper.terminal.Invocations.Clear();

        ctrl.SelectSectionForTests(Network);
        ctrl.Draw();
        string networkOutput = CapturedOutput();

        Assert.Contains("MAC Address:", networkOutput);
        Assert.Contains("AA-BB-CC-DD-EE-01", networkOutput);
        Assert.DoesNotContain("Processor:", networkOutput);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_Key_And_Value_Pairs_For_Each_Subsystem()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.Sample(BuildSnapshot());

        AssertSectionContains(ctrl, Cpu, "Processor:", "AMD Ryzen 9 7950X");
        AssertSectionContains(ctrl, Memory, "Part Number:", "F5-6000J3038F16G");
        AssertSectionContains(ctrl, Gpu, "Driver Version:", "560.94");
        AssertSectionContains(ctrl, Disk, "Model:", "Samsung SSD 990 PRO 2TB", "UNATTACHED VOLUMES");
        AssertSectionContains(ctrl, Network, "MAC Address:", "192.168.1.20, 192.168.1.21", "2.5 Gbps");

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    private void AssertSectionContains(SystemInfoControl ctrl, int navIndex, params string[] fragments)
    {
        ctrl.SelectSectionForTests(navIndex);
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in fragments) {
            Assert.Contains(fragment, output);
        }
    }

    [Fact]
    public void Enumerates_Every_Device_In_A_Multi_Device_Subsystem()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.Sample(BuildSnapshot());

        ctrl.SelectSectionForTests(Gpu);
        ctrl.Draw();
        string gpuOutput = CapturedOutput();
        Assert.Contains("NVIDIA GeForce RTX 4080", gpuOutput);
        Assert.Contains("Intel UHD Graphics 770", gpuOutput);

        runContextHelper.terminal.Invocations.Clear();

        ctrl.SelectSectionForTests(Memory);
        ctrl.Draw();
        string memoryOutput = CapturedOutput();
        Assert.Contains("SLOT 1", memoryOutput);
        Assert.Contains("SLOT 2", memoryOutput);

        runContextHelper.terminal.Invocations.Clear();

        ctrl.SelectSectionForTests(Disk);
        ctrl.Draw();
        string diskOutput = CapturedOutput();
        Assert.Contains("Samsung SSD 990 PRO 2TB", diskOutput);
        Assert.Contains("WD Elements 25A3", diskOutput);

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_Device_Count_Changes()
    {
        SystemInfoControl ctrl = CreateControl();
        ctrl.SelectSectionForTests(Disk);

        SystemSnapshot oneDisk = BuildSnapshot() with {
            Disk = new DiskInfo {
                Specs = new DiskSpecs {
                    Devices = [ new DiskDevice { Index = 0, Model = "Samsung SSD 990 PRO 2TB" } ],
                },
            },
        };

        ctrl.Sample(oneDisk);
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(BuildSnapshot());
        ctrl.Draw();

        Assert.Contains("WD Elements 25A3", CapturedOutput());

        ctrl.Unload();
    }

    // ---- Nav / content focus routing ---------------------------------------------------------

    [Fact]
    public void RightArrow_On_The_Nav_Moves_Focus_Into_The_Content_Pane()
    {
        SystemInfoControl ctrl = CreateFocusableControl();
        ctrl.Sample(BuildSnapshot());

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    [Fact]
    public void LeftArrow_On_The_Content_Pane_Moves_Focus_Back_To_The_Nav()
    {
        SystemInfoControl ctrl = CreateFocusableControl();
        ctrl.Sample(BuildSnapshot());

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        Assert.True(handled);

        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        Assert.True(handled);

        ctrl.Unload();
    }

    [Fact]
    public void LeftArrow_On_The_Nav_Is_Left_Unhandled_So_It_Can_Bubble_Out()
    {
        // Nothing further left of the nav inside SystemInfoControl - MainScreen2 relies on this
        // staying unhandled so it can move focus back to its own outer app menu.
        SystemInfoControl ctrl = CreateFocusableControl();
        ctrl.Sample(BuildSnapshot());

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);

        Assert.False(handled);

        ctrl.Unload();
    }

    [Fact]
    public void RightArrow_On_The_Content_Pane_Is_Left_Unhandled()
    {
        // Nothing further right of the content pane.
        SystemInfoControl ctrl = CreateFocusableControl();
        ctrl.Sample(BuildSnapshot());

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        Assert.True(handled);

        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);

        Assert.False(handled);

        ctrl.Unload();
    }
}
