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
using Task.Monitor.System.Process;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.Tests.Gui.Controls;

// Every ListView - and every row it holds - must be painted with the theme's listview.background
// and listview.foreground rather than control.background / control.foreground, and its border
// with listview.borderforeground / listview.borderbackground (or the chart border pair, for the
// lists that sit beside charts). Each test sets every one of these to a distinct colour, so
// anything still using a control colour, or the wrong border pair, fails.
public sealed class ListViewColourTests
{
    private static readonly Color ListViewBackground = ConsolePalette.DarkCyan;
    private static readonly Color ListViewForeground = ConsolePalette.DarkYellow;
    private static readonly Color ListViewBorderForeground = ConsolePalette.DarkGreen;
    private static readonly Color ListViewBorderBackground = ConsolePalette.DarkRed;
    private static readonly Color ChartBorderForeground = ConsolePalette.Magenta;
    private static readonly Color ChartBorderBackground = ConsolePalette.DarkBlue;

    // Hosts whose lists deliberately take the chart border colours, to match the charts beside them.
    private static readonly HashSet<string> ChartBorderedHosts = [
        nameof(CpuPerformanceControl),
        nameof(DiskPerformanceControl),
        nameof(GpuPerformanceControl),
        nameof(MemoryPerformanceControl),
        nameof(NetworkPerformanceControl),
        nameof(ProcessControl)
    ];

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

    private ISystemTerminal Terminal => new ForwardingTerminal(runContext.Terminal);

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

        AssertListViewsUseListViewColours(host, chartBordered: ChartBorderedHosts.Contains(hostName));

        host.Unload();
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
                            Source = StartupEntrySource.RunKey,
                            Scope = StartupEntryScope.User,
                            State = StartupEntryState.Enabled,
                            Command = "OneDrive.exe"
                        }
                    ]
                }
            }
        });

        List<ListViewItem> rows = AssertRowsUseListViewBackground(control);

        // NAME cell of the main table and the "Name" field row of the detail pane.
        Assert.Contains(rows, row => row.SubItems[0].Text == "OneDrive"
            && row.SubItems[0].ForegroundColor == ListViewForeground);
        Assert.Contains(rows, row => row.SubItems[0].Text == "Name"
            && row.SubItems[0].ForegroundColor == ListViewForeground);

        control.Unload();
    }

    [Fact]
    public void ProcessInfoControl_ListViews_And_Rows_Use_The_ListView_Colours()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);
        Assert.NotNull(processInfo);

        ProcessServiceFake processServiceFake = new();
        processServiceFake.AddProcessInfo(processInfo);

        ProcessInfoControl control = new(
            processServiceFake,
            new ModuleServiceFake(),
            new ThreadServiceFake(),
            Terminal,
            runContext.AppConfig) {
            AutoRefresh = false,
            SelectedProcessId = currentProcess.Id
        };

        Loaded(control);

        AssertListViewsUseListViewColours(control);
        List<ListViewItem> rows = AssertRowsUseListViewBackground(control);

        Assert.Contains(rows, row => row.SubItems[0].Text == "DETAIL"
            && row.SubItems[0].ForegroundColor == ListViewForeground);

        control.Unload();
    }

    [Fact]
    public void HelpScreen_Rows_Use_The_ListView_Background()
    {
        HelpScreen screen = Loaded(new HelpScreen(runContext));

        AssertRowsUseListViewBackground(screen);

        screen.Unload();
    }

    private static T Loaded<T>(T control) where T : Control
    {
        control.Width = 160;
        control.Height = 48;
        control.Load();
        control.Resize();
        return control;
    }

    // Pop-up dialogs (DriveInputBox, PickerBox) own a private list drawn on the dialog's own
    // colours, not a themed pane, so their lists are left out.
    private static void AssertListViewsUseListViewColours(Control host, bool chartBordered = false)
    {
        (Color borderForeground, Color borderBackground) = chartBordered
            ? (ChartBorderForeground, ChartBorderBackground)
            : (ListViewBorderForeground, ListViewBorderBackground);

        List<ListView> listViews = ThemedListViews(host);

        Assert.NotEmpty(listViews);
        Assert.All(listViews, listView => {
            Assert.Equal(ListViewBackground, listView.BackgroundColour);
            Assert.Equal(ListViewForeground, listView.ForegroundColour);
            Assert.Equal(borderForeground, listView.BorderForegroundColour);
            Assert.Equal(borderBackground, listView.BorderBackgroundColour);
        });
    }

    private static List<ListView> ThemedListViews(Control host)
    {
        HashSet<ListView> dialogLists = ControlTreeHelper.FindAll<DriveInputBox>(host).Cast<Control>()
            .Concat(ControlTreeHelper.FindAll<PickerBox>(host))
            .SelectMany(ControlTreeHelper.FindAll<ListView>)
            .ToHashSet(ReferenceEqualityComparer.Instance as IEqualityComparer<ListView>);

        return ControlTreeHelper.FindAll<ListView>(host).Where(listView => !dialogLists.Contains(listView)).ToList();
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
}
