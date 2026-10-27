using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.InstalledApps;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class InstalledAppsControlTests : IDisposable
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public InstalledAppsControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static InstalledApp App(
        string name,
        string? version = "1.0.0",
        string? publisher = null,
        InstalledAppScope scope = InstalledAppScope.Machine,
        DateTime? installDate = null,
        long? estimatedSizeKb = null,
        string? installLocation = null,
        bool sizePending = false) =>
        new() {
            Name = name,
            Version = version,
            Publisher = publisher,
            Scope = scope,
            InstallDate = installDate,
            EstimatedSizeKb = estimatedSizeKb,
            InstallLocation = installLocation,
            SizePending = sizePending
        };

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        InstalledAppsControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private InstalledAppsControl CreateControl(int width = 160, int height = 40)
    {
        InstalledAppsControl ctrl = new(
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

    [Fact]
    public void Descending_Sort_Reverses_The_Rows_And_The_Detail_Pane_Follows_The_Selection()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            App("Alpha App"),
            App("Bravo App")));
        ctrl.Draw();

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.D));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        string detail = output[output.IndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Contains("NAME\u2193", output);
        Assert.True(output.IndexOf("Bravo App", StringComparison.Ordinal) < output.IndexOf("Alpha App", StringComparison.Ordinal));
        Assert.Contains("Bravo App", detail);
        Assert.DoesNotContain("Alpha App", detail);

        ctrl.Unload();
    }

    [Fact]
    public void Detail_Pane_Follows_The_Selection_In_The_Filtered_List()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            App("Alpha App", installLocation: "/Applications/Alpha.app"),
            App("Bravo App", installLocation: "/Applications/Bravo.app"),
            App("Charlie App", installLocation: "/Applications/Charlie.app")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "charlie");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Charlie.app", output);
        Assert.DoesNotContain("Alpha.app", output);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_App()
    {
        InstalledAppsControl ctrl = CreateControl();

        const string longLocation =
            @"C:\Program Files\SomeVendor\SomeProduct\LongPathName\With\Several\Nested\Folders";

        ctrl.Sample(SnapshotWith(
            App("Git", version: "2.44.0", publisher: "Git for Windows",
                installDate: new DateTime(2026, 1, 15), estimatedSizeKb: 51200,
                installLocation: longLocation),
            App("MyTool", version: "0.9", scope: InstalledAppScope.User)));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the install location is too long to fit in the main table's LOCATION column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Version", "Publisher", "Scope", "Installed", "Size", "Location",
            "Git", "2.44.0", "Git for Windows", "Machine", "2026-01-15", "50.0 MB", longLocation
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_App()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            App("Git", version: "2.44.0", publisher: "Git for Windows",
                installDate: new DateTime(2026, 1, 15), estimatedSizeKb: 51200,
                installLocation: @"C:\Program Files\Git"),
            App("MyTool", version: "0.9", scope: InstalledAppScope.User)));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "VERSION", "PUBLISHER", "SCOPE", "INSTALLED", "SIZE", "LOCATION",
            "Git", "2.44.0", "Git for Windows", "Machine", "2026-01-15", "50.0 MB",
            @"C:\Program Files\Git",
            "MyTool", "0.9", "User"
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering installed applications", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Escape_Clears_The_Filter()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            App("Alpha App", installLocation: "/Applications/Alpha.app"),
            App("Bravo App", installLocation: "/Applications/Bravo.app"),
            App("Charlie App", installLocation: "/Applications/Charlie.app")));
        ctrl.Draw();
        ListKeys.ApplyFilter(ctrl, "bravo");

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.Escape));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Alpha App", output);
        Assert.Contains("Charlie App", output);
        Assert.DoesNotContain("Filter:", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Shows_Only_Matching_Rows_And_The_Match_Count_In_The_Title()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            App("Alpha App", installLocation: "/Applications/Alpha.app"),
            App("Bravo App", installLocation: "/Applications/Bravo.app"),
            App("Charlie App", installLocation: "/Applications/Charlie.app")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Filter: bravo (1 of 3)", output);
        Assert.Contains("Bravo App", output);
        Assert.DoesNotContain("Alpha App", output);
        Assert.DoesNotContain("Charlie App", output);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Clear_Hint_Only_While_Filtered_And_Counts_All_Entries()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            App("Alpha App", installLocation: "/Applications/Alpha.app"),
            App("Bravo App", installLocation: "/Applications/Bravo.app"),
            App("Charlie App", installLocation: "/Applications/Charlie.app")));
        ctrl.Draw();

        Assert.Contains("r Refresh     s Sort     f Filter     ↑ ↓", CapturedOutput());
        Assert.DoesNotContain("Esc Clear Filter", CapturedOutput());

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("3 installed     r Refresh     s Sort     f Filter     Esc Clear Filter     ↑ ↓", output);

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_InstalledApps_Info()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering installed applications", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_App_Set_Changes()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("Only")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(App("Only"), App("Added")));
        ctrl.Draw();

        Assert.Contains("Added", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Redraws_A_Row_When_Its_Size_Arrives()
    {
        // On macOS the app set stays the same while bundle sizes are measured after the scan, so a
        // size alone must be enough to rebuild the rows.
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("Xcode", sizePending: true)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(App("Xcode", estimatedSizeKb: 51200)));
        ctrl.Draw();

        Assert.Contains("50.0 MB", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_A_Pending_Size_As_An_Ellipsis()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("Xcode", sizePending: true)));
        ctrl.Draw();

        Assert.Contains("...", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_Total_Count_In_The_Footer()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("A"), App("B"), App("C")));
        ctrl.Draw();

        Assert.Contains("3 installed", CapturedOutput());

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(params InstalledApp[] apps) =>
        new() { InstalledApps = new InstalledAppsInfo { Specs = new InstalledAppsSpecs { Apps = [.. apps] } } };

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            App("Git", publisher: "Git for Windows"),
            App("MyTool", publisher: "Acme", scope: InstalledAppScope.User)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains("Acme", CapturedOutput());

        ctrl.Unload();
    }
}
