using System.Reflection;
using System.Text.RegularExpressions;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Tips;
using Task.Monitor.System.Services;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class FooterControlTests : IDisposable
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    private FooterControl? control;

    private const string KeyHintsText = "F1 Help   F2 Setup   F10 Exit";
    private const string OrangeGlyph  = "38;2;255;165;0m  ●";

    public FooterControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
        runContext.ServiceController.SetSamplingDelay(60_000);
    }

    [Fact]
    public void A_Narrow_Footer_Drops_The_Hint_And_Cuts_The_Tip_Short()
    {
        FooterControl ctrl = CreateTipControl(width: 40);
        ctrl.Draw();

        string row = VisibleRow(0);

        Assert.Equal(40, row.Length);
        Assert.EndsWith("…", row);
        Assert.DoesNotContain("Next Tip", row);
    }

    [Fact]
    public void A_Snapshot_After_Unknown_Resets_The_Status()
    {
        FooterControl ctrl = CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));
        Tick(ctrl, 3);

        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));
        ClearOutput();
        Tick(ctrl, 2);
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Services: Running", output);
        Assert.DoesNotContain("Service status: Unknown", output);
    }

    [Fact]
    public void A_Wide_Footer_Shows_The_Next_Tip_Hint_At_The_Right_Edge()
    {
        FooterControl ctrl = CreateTipControl(width: 160);
        ctrl.Draw();

        string row = VisibleRow(0);

        Assert.Equal(160, row.Length);
        Assert.EndsWith("t Next Tip", row);
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    private void ClearOutput() => runContextHelper.terminal.Invocations.Clear();

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        FooterControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private FooterControl CreateControl(int width = 160, int height = 1)
    {
        FooterControl ctrl = control = new(
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

    private FooterControl CreateTipControl(int width = 160, DateTime? now = null)
    {
        FooterControl ctrl = CreateControl(width, height: 2);
        ctrl.TipIndex = TipIndexOf("{z}");
        ctrl.UtcNow = () => now ?? DateTime.UnixEpoch;
        return ctrl;
    }

    public void Dispose() => control?.Unload();

    [Fact]
    public void Draws_The_Key_Hints_After_The_Status_And_Rx_Count()
    {
        CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));

        string text = VisibleText();

        Assert.Contains(KeyHintsText, text);
        Assert.True(text.IndexOf("Services: Running", StringComparison.Ordinal) < text.IndexOf(" Rx", StringComparison.Ordinal));
        Assert.True(text.IndexOf(" Rx", StringComparison.Ordinal) < text.IndexOf(KeyHintsText, StringComparison.Ordinal));
    }

    [Fact]
    public void Draws_The_Version_Banner_Without_A_Snapshot()
    {
        FooterControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Task Monitor v", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Key_Hints_Are_Coloured_With_The_Theme_HotKey_Colours()
    {
        CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));

        Theme theme = runContext.AppConfig.Theme;
        string hint = ConsolePalette.ForegroundSgr(theme.HotKey) + "F1" + ConsolePalette.ForegroundSgr(theme.HotKeyName) + " Help";

        Assert.Contains(hint, CapturedOutput());
    }

    [Fact]
    public void Key_Hints_Are_Dropped_When_The_Footer_Is_Too_Narrow()
    {
        CreateControl(width: 80);
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));

        string text = VisibleText();

        Assert.DoesNotContain("F1 Help", text);
        Assert.True(text.Length <= 80);
    }

    [Fact]
    public void Key_Hints_Keep_Their_Column_Without_A_Snapshot_Or_When_The_Status_Changes()
    {
        FooterControl ctrl = CreateControl();
        ctrl.Draw();
        int withoutSnapshot = VisibleText().IndexOf(KeyHintsText, StringComparison.Ordinal);

        ClearOutput();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));
        int running = VisibleText().IndexOf(KeyHintsText, StringComparison.Ordinal);

        ClearOutput();
        RaiseSnapshotUpdated(new SystemSnapshot {
            Sequence = 1_234_567,
            Services = [new ServiceHealth("Cpu", ServiceStatus.Stopping)]
        });
        int stopping = VisibleText().IndexOf(KeyHintsText, StringComparison.Ordinal);

        ClearOutput();
        Tick(ctrl, 3);
        int unknown = VisibleText().IndexOf(KeyHintsText, StringComparison.Ordinal);

        Assert.True(withoutSnapshot > 0);
        Assert.Equal(withoutSnapshot, running);
        Assert.Equal(withoutSnapshot, stopping);
        Assert.Equal(withoutSnapshot, unknown);
    }

    [Fact]
    public void Key_Hints_Move_Right_Once_The_Rx_Count_Passes_Seven_Digits()
    {
        CreateControl();
        RaiseSnapshotUpdated(new SystemSnapshot {
            Sequence = 9_999_999,
            Services = [new ServiceHealth("Cpu", ServiceStatus.Running)]
        });
        int sevenDigits = VisibleText().IndexOf(KeyHintsText, StringComparison.Ordinal);

        ClearOutput();
        RaiseSnapshotUpdated(new SystemSnapshot {
            Sequence = 10_000_000,
            Services = [new ServiceHealth("Cpu", ServiceStatus.Running)]
        });
        string text = VisibleText();

        Assert.Contains(" 10000000 Rx   F1 Help", text);
        Assert.Equal(sevenDigits + 1, text.IndexOf(KeyHintsText, StringComparison.Ordinal));
    }

    [Fact]
    public void NextTip_Moves_On_And_Restarts_The_Rotation_Interval()
    {
        DateTime now  = DateTime.UnixEpoch;
        FooterControl ctrl = CreateTipControl();
        ctrl.UtcNow = () => now;

        int first = ctrl.TipIndex;
        ctrl.Draw();

        now += TimeSpan.FromMinutes(4);
        ctrl.NextTip();
        Assert.Equal((first + 1) % TipCatalogue.Tips.Count, ctrl.TipIndex);

        // Five minutes from the start, but only one since the key press: no rotation yet.
        now += TimeSpan.FromMinutes(1);
        ctrl.Draw();
        Assert.Equal((first + 1) % TipCatalogue.Tips.Count, ctrl.TipIndex);
    }

    [Fact]
    public void NextTip_Wraps_From_The_Last_Tip_To_The_First()
    {
        FooterControl ctrl = CreateTipControl();
        ctrl.TipIndex = TipCatalogue.Tips.Count - 1;

        ctrl.NextTip();

        Assert.Equal(0, ctrl.TipIndex);
    }

    [Fact]
    public void One_Row_Draws_No_Tip()
    {
        FooterControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.DoesNotContain("Tip:", VisibleText());
    }

    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(runContext.ServiceController);
        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    [Fact]
    public void Rows_Is_Two_With_Tips_On_And_One_With_Them_Off()
    {
        FooterControl ctrl = CreateControl();

        runContext.AppConfig.ShowTips = true;
        Assert.Equal(2, ctrl.Rows);

        runContext.AppConfig.ShowTips = false;
        Assert.Equal(1, ctrl.Rows);
    }

    private static SystemSnapshot SnapshotWith(params ServiceHealth[] services) =>
        new() { Services = services };

    [Fact]
    public void The_Tip_Changes_Once_The_Rotation_Interval_Passes()
    {
        DateTime now  = DateTime.UnixEpoch;
        FooterControl ctrl = CreateTipControl();
        ctrl.UtcNow = () => now;

        int first = ctrl.TipIndex;
        ctrl.Draw();

        now += FooterControl.TipRotationInterval - TimeSpan.FromSeconds(1);
        ctrl.Draw();
        Assert.Equal(first, ctrl.TipIndex);

        now += TimeSpan.FromSeconds(1);
        ctrl.Draw();
        Assert.Equal((first + 1) % TipCatalogue.Tips.Count, ctrl.TipIndex);
    }

    [Fact]
    public void The_Tip_Glyph_Takes_The_Theme_Tip_Colour_And_Keys_The_HotKey_Colour()
    {
        FooterControl ctrl = CreateTipControl();
        ctrl.Draw();

        Theme  theme  = runContext.AppConfig.Theme;
        string output = CapturedOutput();

        Assert.Contains(ConsolePalette.ForegroundSgr(theme.TipGlyph) + "●", output);
        Assert.Contains(ConsolePalette.ForegroundSgr(theme.HotKey) + "z", output);
        Assert.Contains(ConsolePalette.ForegroundSgr(theme.Foreground) + " in a process list", output);
    }

    [Fact]
    public void Three_Missed_Cycles_Show_Unknown_With_An_Orange_Glyph()
    {
        FooterControl ctrl = CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));
        ClearOutput();

        Tick(ctrl, 3);

        string output = CapturedOutput();
        Assert.Contains("Service status: Unknown", output);
        Assert.Contains(OrangeGlyph, output);
    }

    [Fact]
    public void Three_Missed_Cycles_Without_Any_Snapshot_Show_Unknown()
    {
        FooterControl ctrl = CreateControl();

        Tick(ctrl, 3);

        string output = CapturedOutput();
        Assert.Contains("Service status: Unknown", output);
        Assert.Contains(OrangeGlyph, output);
        Assert.DoesNotContain(" Rx", output);
    }

    private static void Tick(FooterControl ctrl, int cycles)
    {
        for (int i = 0; i < cycles; i++) {
            ctrl.OnWatchdogTick(null);
        }
    }

    private static int TipIndexOf(string marker) =>
        TipCatalogue.Tips.ToList().FindIndex(tip => tip.Contains(marker));

    [Fact]
    public void Two_Missed_Cycles_Keep_The_Normal_Status()
    {
        FooterControl ctrl = CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));
        ClearOutput();

        Tick(ctrl, 2);
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Services: Running", output);
        Assert.DoesNotContain("Service status: Unknown", output);
    }

    [Fact]
    public void Two_Rows_Draw_The_Tip_Above_The_Status_Row()
    {
        FooterControl ctrl = CreateTipControl();
        ctrl.Y = 10;
        ctrl.Draw();

        Assert.StartsWith("● Tip: Press z in a process list to freeze updates", VisibleRow(10));
        Assert.StartsWith(" Task Monitor v", VisibleRow(11));
    }

    [Fact]
    public void Unknown_Overrides_An_Errored_Service_In_A_Stale_Snapshot()
    {
        FooterControl ctrl = CreateControl();
        RaiseSnapshotUpdated(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Errored)));
        ClearOutput();

        Tick(ctrl, 3);

        string output = CapturedOutput();
        Assert.Contains("Service status: Unknown", output);
        Assert.DoesNotContain("Errored", output);
    }

    // The visible text written after the cursor moves to the given row, up to the next move.
    private string VisibleRow(int row)
    {
        Match match = Regex.Match(CapturedOutput(), $@"\x1b\[{row + 1};1H(.*?)(?=\x1b\[\d+;\d+H|$)", RegexOptions.Singleline);
        Assert.True(match.Success, $"Nothing was drawn on row {row}.");

        return Regex.Replace(match.Groups[1].Value, @"\x1b\[[0-9;]*[A-Za-z]", string.Empty);
    }

    private string VisibleText() =>
        Regex.Replace(CapturedOutput(), @"\x1b\[[0-9;]*[A-Za-z]", string.Empty);
}
