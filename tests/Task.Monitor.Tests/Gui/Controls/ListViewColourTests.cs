using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.Gui.Controls.SystemInformation;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.DriveInputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.PickerBox;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Process;

namespace Task.Monitor.Tests.Gui.Controls;

// Every ListView - and every row it holds - must be painted with the theme's listview.background
// and listview.foreground rather than control.background / control.foreground, and its border
// with listview.borderforeground / listview.borderbackground. Lists never borrow the chart
// colours, even beside charts. Each test sets every one of these, and the chart border pair, to a
// distinct colour, so anything using a control colour or a chart colour fails.
public sealed class ListViewColourTests
{
    private static readonly Color ListViewBackground = ConsolePalette.DarkCyan;
    private static readonly Color ListViewForeground = ConsolePalette.DarkYellow;
    private static readonly Color ListViewBorderForeground = ConsolePalette.DarkGreen;
    private static readonly Color ListViewBorderBackground = ConsolePalette.DarkRed;
    private static readonly Color ChartBorderForeground = ConsolePalette.Magenta;
    private static readonly Color ChartBorderBackground = ConsolePalette.DarkBlue;

    private readonly RunContext runContext;

    public ListViewColourTests()
    {
        runContext = new RunContextHelper().GetRunContext();
        runContext.AppConfig.Theme.ListViewBackground = ListViewBackground;
        runContext.AppConfig.Theme.ListViewForeground = ListViewForeground;
        runContext.AppConfig.Theme.ListViewBorderForeground = ListViewBorderForeground;
        runContext.AppConfig.Theme.ListViewBorderBackground = ListViewBorderBackground;
        runContext.AppConfig.Theme.ChartBorderForeground = ChartBorderForeground;
        runContext.AppConfig.Theme.ChartBorderBackground = ChartBorderBackground;

        Assert.NotEqual(runContext.AppConfig.Theme.Background, runContext.AppConfig.Theme.ListViewBackground);
        Assert.NotEqual(runContext.AppConfig.Theme.Foreground, runContext.AppConfig.Theme.ListViewForeground);
    }

    // Pop-up dialogs (DriveInputBox, PickerBox) own a private list drawn on the dialog's own
    // colours, not a themed pane, so their lists are left out.
    private static void AssertListViewsUseListViewColours(Control host)
    {
        List<ListView> listViews = ThemedListViews(host);

        Assert.NotEmpty(listViews);
        Assert.All(listViews, listView => {
            Assert.Equal(ListViewBackground, listView.BackgroundColour);
            Assert.Equal(ListViewForeground, listView.ForegroundColour);
            Assert.Equal(ListViewBorderForeground, listView.BorderForegroundColour);
            Assert.Equal(ListViewBorderBackground, listView.BorderBackgroundColour);
        });
    }

    // Foregrounds legitimately vary per cell (key colours, disabled / high-CPU highlights), but no
    // cell should fall back to the control background.
    private static List<ListViewItem> AssertRowsUseListViewBackground(Control host)
    {
        List<ListViewItem> rows = ThemedListViews(host)
            .SelectMany(listView => listView.Items.Cast<ListViewItem>())
            .ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.All(row.SubItems.Cast<ListViewSubItem>(),
            subItem => Assert.Equal(ListViewBackground, subItem.BackgroundColor)));

        return rows;
    }

    [Fact]
    public void HelpScreen_Rows_Use_The_ListView_Background()
    {
        HelpScreen screen = Loaded(new HelpScreen(runContext));

        AssertRowsUseListViewBackground(screen);

        screen.Unload();
    }

    public static TheoryData<string> Hosts => new() {
        nameof(CpuPerformanceControl),
        nameof(DiskPerformanceControl),
        nameof(DiskSpaceControl),
        nameof(DriversControl),
        nameof(GpuPerformanceControl),
        nameof(InstalledAppsControl),
        nameof(MemoryPerformanceControl),
        nameof(MenuControl),
        nameof(NetworkPerformanceControl),
        nameof(ProcessControl),
        nameof(ServicesControl),
        nameof(StartupControl),
        nameof(SystemInfoControl),
        nameof(AboutScreen),
        nameof(HelpScreen),
        nameof(SetupScreen)
    };

    [Theory]
    [MemberData(nameof(Hosts))]
    public void ListViews_Use_The_ListView_Colours(string hostName)
    {
        Control host = hostName switch {
            nameof(CpuPerformanceControl) => new CpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(DiskPerformanceControl) => new DiskPerformanceControl(Terminal, runContext.AppConfig),
            nameof(DiskSpaceControl) => new DiskSpaceControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(DriversControl) => new DriversControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(GpuPerformanceControl) => new GpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(InstalledAppsControl) => new InstalledAppsControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(MemoryPerformanceControl) => new MemoryPerformanceControl(Terminal, runContext.AppConfig),
            nameof(MenuControl) => new MenuControl(Terminal, runContext.AppConfig) {
                MenuItems = [new MenuListViewItem(new Control(Terminal), "ITEM")]
            },
            nameof(NetworkPerformanceControl) => new NetworkPerformanceControl(Terminal, runContext.AppConfig),
            nameof(ProcessControl) => new ProcessControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(ServicesControl) => new ServicesControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(StartupControl) => new StartupControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(SystemInfoControl) => new SystemInfoControl(runContext.ServiceController, Terminal, runContext.AppConfig),
            nameof(AboutScreen) => new AboutScreen(runContext),
            nameof(HelpScreen) => new HelpScreen(runContext),
            nameof(SetupScreen) => new SetupScreen(runContext, new ScreenApplication(runContext.Terminal)),
            _ => throw new ArgumentOutOfRangeException(nameof(hostName))
        };

        Loaded(host);

        // SetupScreen re-applies its (preview) theme on every draw rather than in OnLoad.
        if (host is SetupScreen) {
            host.Draw();
        }

        AssertListViewsUseListViewColours(host);

        host.Unload();
    }

    public static TheoryData<string> PerformanceHosts => new() {
        nameof(CpuPerformanceControl),
        nameof(DiskPerformanceControl),
        nameof(GpuPerformanceControl),
        nameof(MemoryPerformanceControl),
        nameof(NetworkPerformanceControl),
        nameof(NpuPerformanceControl)
    };

    // Each performance control's specs list (two columns, no headers) holds "Key:" / value rows,
    // drawn like the header's labels and values: keys in property.key, values in property.value.
    [Theory]
    [MemberData(nameof(PerformanceHosts))]
    public void Performance_Specs_Rows_Use_The_Property_Colours(string hostName)
    {
        Color propertyKey   = ConsolePalette.Cyan;
        Color propertyValue = ConsolePalette.Yellow;

        runContext.AppConfig.Theme.PropertyKey   = propertyKey;
        runContext.AppConfig.Theme.PropertyValue = propertyValue;

        Control host = hostName switch {
            nameof(CpuPerformanceControl)     => new CpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(DiskPerformanceControl)    => new DiskPerformanceControl(Terminal, runContext.AppConfig),
            nameof(GpuPerformanceControl)     => new GpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(MemoryPerformanceControl)  => new MemoryPerformanceControl(Terminal, runContext.AppConfig),
            nameof(NetworkPerformanceControl) => new NetworkPerformanceControl(Terminal, runContext.AppConfig),
            nameof(NpuPerformanceControl)     => new NpuPerformanceControl(Terminal, runContext.AppConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(hostName))
        };

        Loaded(host);

        ListView specs = Assert.Single(
            ThemedListViews(host),
            listView => !listView.ShowColumnHeaders && listView.ColumnHeaders.Count() == 2);

        Assert.NotEmpty(specs.Items);
        Assert.All(specs.Items.Cast<ListViewItem>(), row => {
            Assert.Equal(propertyKey, row.SubItems[0].ForegroundColor);
            Assert.Equal(propertyValue, row.SubItems[1].ForegroundColor);
        });

        host.Unload();
    }

    private static T Loaded<T>(T control) where T : Control
    {
        control.Width = 160;
        control.Height = 48;
        control.Load();
        control.Resize();
        return control;
    }

    [Fact]
    public void ProcessInfoControl_ListViews_And_Rows_Use_The_ListView_Colours()
    {
        ProcessInfoControl control = LoadedProcessInfoControl();

        AssertListViewsUseListViewColours(control);
        List<ListViewItem> rows = AssertRowsUseListViewBackground(control);

        Assert.Contains(rows, row => row.SubItems[0].Text == "DETAIL"
            && row.SubItems[0].ForegroundColor == ListViewForeground);

        control.Unload();
    }

    // DETAIL lists "Key:" / value rows, drawn like the performance specs lists.
    [Fact]
    public void ProcessInfoControl_Detail_Rows_Use_The_Property_Colours()
    {
        Color propertyKey   = ConsolePalette.Cyan;
        Color propertyValue = ConsolePalette.Yellow;

        runContext.AppConfig.Theme.PropertyKey   = propertyKey;
        runContext.AppConfig.Theme.PropertyValue = propertyValue;

        ProcessInfoControl control = LoadedProcessInfoControl();

        ListView detail = Assert.Single(
            ThemedListViews(control),
            listView => listView.Items.Cast<ListViewItem>().Any(row => row.SubItems[0].Text == "File:"));

        Assert.All(detail.Items.Cast<ListViewItem>(), row => {
            Assert.Equal(propertyKey, row.SubItems[0].ForegroundColor);
            Assert.Equal(propertyValue, row.SubItems[1].ForegroundColor);
        });

        control.Unload();
    }

    private ProcessInfoControl LoadedProcessInfoControl()
    {
        ProcessSample? sample = new ProcessService().GetProcessSample(Environment.ProcessId);
        Assert.NotNull(sample);

        ProcessServiceFake processServiceFake = new();
        processServiceFake.Add(sample);
        runContext.ServiceController.AddService(() => processServiceFake);

        ProcessInfoControl control = new(
            runContext.ServiceController,
            Terminal,
            runContext.AppConfig) {
            AutoRefresh = false,
            SelectedProcessId = sample.Pid
        };

        return Loaded(control);
    }

    [Fact]
    public void StartupControl_Rows_Use_The_ListView_Colours()
    {
        StartupControl control = Loaded(
            new StartupControl(runContext.ServiceController, Terminal, runContext.AppConfig));

        control.Sample(new SystemSnapshot {
            Startup = new StartupInfo {
                Specs = new StartupSpecs {
                    Entries = [
                        new StartupEntry {
                            Name = "OneDrive",
                            Source = StartupTestSources.Primary,
                            Scope = StartupEntryScope.User,
                            State = StartupEntryState.Enabled,
                            Command = "OneDrive.exe"
                        }
                    ]
                }
            }
        });

        List<ListViewItem> rows = AssertRowsUseListViewBackground(control);

        // NAME cell of the main table uses the list colour; the "Name" field row of the detail pane
        // is a property row, so its field takes property.key.
        Assert.Contains(rows, row => row.SubItems[0].Text == "OneDrive"
            && row.SubItems[0].ForegroundColor == ListViewForeground);
        Assert.Contains(rows, row => row.SubItems[0].Text == "Name"
            && row.SubItems[0].ForegroundColor == runContext.AppConfig.Theme.PropertyKey);

        control.Unload();
    }

    private ISystemTerminal Terminal => new ForwardingTerminal(runContext.Terminal);

    private static List<ListView> ThemedListViews(Control host)
    {
        HashSet<ListView> dialogLists = ControlTreeHelper.FindAll<DriveInputBox>(host).Cast<Control>()
            .Concat(ControlTreeHelper.FindAll<PickerBox>(host))
            .SelectMany(ControlTreeHelper.FindAll<ListView>)
            .ToHashSet(ReferenceEqualityComparer.Instance as IEqualityComparer<ListView>);

        return ControlTreeHelper.FindAll<ListView>(host).Where(listView => !dialogLists.Contains(listView)).ToList();
    }
}
