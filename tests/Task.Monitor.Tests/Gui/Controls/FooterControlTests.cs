using System.Reflection;
using System.Text.RegularExpressions;
using Task.Monitor.Gui.Controls;
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

    private const string KeyHintsText = "F1 Help  F2 Setup  F10 Exit";
    private const string OrangeGlyph  = "38;2;255;165;0m  ●";

    public FooterControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
        runContext.ServiceController.SetSamplingDelay(60_000);
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

    private FooterControl CreateControl(int width = 160)
    {
        FooterControl ctrl = control = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = 1
        };

        ctrl.Load();
        ctrl.Resize();
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

        Assert.Contains(" 10000000 Rx  F1 Help", text);
        Assert.Equal(sevenDigits + 1, text.IndexOf(KeyHintsText, StringComparison.Ordinal));
    }

    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(runContext.ServiceController);
        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    private static SystemSnapshot SnapshotWith(params ServiceHealth[] services) =>
        new() { Services = services };

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

    private string VisibleText() =>
        Regex.Replace(CapturedOutput(), @"\x1b\[[0-9;]*[A-Za-z]", string.Empty);
}
