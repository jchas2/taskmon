using System.Drawing;
using System.Globalization;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls.Mascot;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui;

// With the one sampler these tests add, the robot is 19 rows, the figures 4 (heading, blank row and
// two rows of figures) and the list 1, with a blank row between each.
public sealed class AboutScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    // Named for the sampler it stands in for: the list shows it as the CPU sampler.
    private sealed class CpuService : WorkerService
    {
        public void SetStatus(ServiceStatus status) => Status = status;
    }

    public AboutScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    [Fact]
    public void AboutScreen_Canary_Test() =>
        Assert.Equal(15, CanaryTestHelper.GetPropertyCount<AboutScreen>());

    // The list is refreshed from the samplers themselves, so it changes even without snapshots.
    [Fact]
    public void A_Status_Change_Redraws_The_List_And_Other_Ticks_Only_The_Figures()
    {
        CpuService service = new();
        runContext.ServiceController.AddService(service);

        AboutScreen screen = Shown();

        ClearOutput();
        screen.OnRefreshTick(null);
        string unchanged = CapturedOutput();

        service.SetStatus(ServiceStatus.Errored);
        ClearOutput();
        screen.OnRefreshTick(null);
        string changed = CapturedOutput();

        screen.Close();

        Assert.Contains(Strings.AboutLabelUptime, unchanged);
        Assert.DoesNotContain(Strings.ServiceNameCpu, unchanged);
        Assert.Contains($"{Strings.ServiceNameCpu}: {Strings.ServiceStatus_Errored}", changed);
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    private void ClearOutput() => runContextHelper.terminal.Invocations.Clear();

    [Fact]
    public void Constructor_Hosts_The_Mascot_And_Two_Tables()
    {
        AboutScreen screen = new(runContext);

        Assert.Collection(screen.Controls,
            control => Assert.IsType<MascotControl>(control),
            control => Assert.IsType<ListView>(control),
            control => Assert.IsType<ListView>(control));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void C_Copies_The_Details_And_Says_Whether_It_Worked(bool works)
    {
        runContext.ServiceController.AddService(new CpuService());

        AboutScreen screen = Shown();
        string?     copied = null;

        screen.CopyToClipboard = text => {
            copied = text;
            return works;
        };

        Press(screen, ConsoleKey.Spacebar);
        ClearOutput();
        Press(screen, ConsoleKey.C);
        string output = CapturedOutput();

        screen.Close();

        Assert.NotNull(copied);
        Assert.StartsWith($"{Strings.AboutProductName}\n  {Strings.AboutLabelVersion}: ", copied);
        Assert.Contains($"\n\n{Strings.AboutSamplingEngine}\n  {Strings.AboutLabelUptime}: ", copied);
        Assert.Contains($"\n  {Strings.ServiceNameCpu}: {Strings.ServiceStatus_None}\n", copied);
        Assert.Contains($"\n\n{Strings.AboutSectionFiles}\n", copied);
        Assert.Contains(works ? Strings.AboutCopied : Strings.AboutCopyFailed, output);
    }

    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        AboutScreen screen = new(runContext);

        Assert.Equal(ConsolePalette.Black, screen.BackgroundColour);
        Assert.Equal(ConsolePalette.White, screen.BorderColour);
        Assert.Equal(3, screen.Controls.Count());
        Assert.Equal(ConsolePalette.White, screen.DisplayBorderColour);
        Assert.False(screen.Focused);
        Assert.Equal(ConsolePalette.White, screen.ForegroundColour);
        Assert.False(screen.HasFocus);
        Assert.Equal(0, screen.Height);
        Assert.Equal(string.Empty, screen.Name);
        Assert.Equal(0U, screen.TabIndex);
        Assert.False(screen.TabStop);
        Assert.True(screen.Visible);
        Assert.Equal(0, screen.Width);
        Assert.Equal(0, screen.X);
        Assert.Equal(0, screen.Y);
    }

    [Fact]
    public void Draw_Shows_The_Banner_And_The_Exit_Key()
    {
        AboutScreen screen = Shown();

        string output = CapturedOutput();

        screen.Close();

        Assert.Contains("ABOUT TASK MONITOR", output);
        Assert.Contains("ESC", output);
    }

    // A running sampler is just its name; any other says what it is doing. The dot takes the status colour.
    [Fact]
    public void Each_Sampler_Has_A_Dot_In_Its_Status_Colour_And_Says_What_It_Is_Doing_Unless_Running()
    {
        Color ok      = Color.FromArgb(20, 160, 64);
        Color warning = Color.FromArgb(224, 160, 32);

        runContext.AppConfig.Theme.StatusbarStatusOk      = ok;
        runContext.AppConfig.Theme.StatusbarStatusWarning = warning;

        CpuService service = new();
        runContext.ServiceController.AddService(service);

        AboutScreen screen = Shown();
        string notStarted = CapturedOutput();

        service.SetStatus(ServiceStatus.Running);
        ClearOutput();
        screen.OnRefreshTick(null);
        string running = CapturedOutput();

        screen.Close();

        Assert.Contains(ConsolePalette.ForegroundSgr(warning) + "●", notStarted);
        Assert.Contains($"{Strings.ServiceNameCpu}: {Strings.ServiceStatus_None}", notStarted);
        Assert.Contains(ConsolePalette.ForegroundSgr(ok) + "●", running);
        Assert.Contains(Strings.ServiceNameCpu, running);
        Assert.DoesNotContain($"{Strings.ServiceNameCpu}: ", running);
    }

    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    [Fact]
    public void Load_Hides_The_Cursor_And_Unload_Shows_It()
    {
        AboutScreen screen = new(runContext);

        screen.Load();
        runContextHelper.terminal.VerifySet(t => t.CursorVisible = false, Times.AtLeastOnce);

        screen.Unload();
        runContextHelper.terminal.VerifySet(t => t.CursorVisible = true, Times.AtLeastOnce);
    }

    // Esc must still close the screen while the mascot is mid-intro, so the screen leaves it unhandled
    // for the application to pop the screen.
    [Theory]
    [InlineData(ConsoleKey.Escape)]
    [InlineData(ConsoleKey.F10)]
    public void OnKeyPressed_Leaves_The_Close_Keys_Unhandled_Mid_Intro(ConsoleKey key)
    {
        AboutScreen screen = Shown();

        bool handled = false;
        screen.KeyPressed(Key(key), ref handled);

        screen.Close();

        Assert.False(handled);
    }

    // On a screen too short for the robot nothing plays, so the first key isn't spent skipping it.
    [Theory]
    [InlineData(30, true)]
    [InlineData(18, false)]
    public void OnKeyPressed_Skips_The_Intro_Only_While_The_Robot_Plays(int height, bool skipped)
    {
        AboutScreen screen = Shown(height: height);

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.Spacebar), ref handled);

        bool handledAfter = false;
        screen.KeyPressed(Key(ConsoleKey.Spacebar), ref handledAfter);

        screen.Close();

        Assert.Equal(skipped, handled);
        Assert.False(handledAfter);
        Assert.Contains(Strings.AboutProductName, CapturedOutput());
    }

    private static (MascotControl Mascot, ListView Stats, ListView Samplers) Parts(AboutScreen screen)
    {
        List<object> controls = [.. screen.Controls];
        return ((MascotControl)controls[0], (ListView)controls[1], (ListView)controls[2]);
    }

    private static void Press(AboutScreen screen, ConsoleKey key)
    {
        bool handled = false;
        screen.KeyPressed(Key(key), ref handled);
    }

    [Theory]
    [InlineData(30, false, true,  true)]
    [InlineData(26, false, true,  false)]
    [InlineData(24, false, false, false)]
    [InlineData(18, true,  false, false)]
    public void Sections_Show_Only_When_They_Fit_Whole(int height, bool captionOnly, bool figures, bool list)
    {
        runContext.ServiceController.AddService(new CpuService());

        AboutScreen screen = Shown(height: height);
        (MascotControl mascot, ListView stats, ListView samplers) = Parts(screen);
        (bool, bool, bool) shown = (mascot.CaptionOnly, stats.Visible, samplers.Visible);

        screen.Close();

        Assert.Equal((captionOnly, figures, list), shown);
    }

    private AboutScreen Shown(int width = 80, int height = 30)
    {
        AboutScreen screen = new(runContext) {
            Width  = width,
            Height = height
        };

        screen.Show();

        return screen;
    }

    // The heading and the labels are in the property key colour, the values in the value colour,
    // with a blank row between the heading and the figures.
    [Fact]
    public void The_Figures_Sit_A_Blank_Row_Under_Their_Heading_In_The_Property_Colours()
    {
        Color key   = Color.FromArgb(241, 250, 141);
        Color value = Color.FromArgb(168, 184, 201);

        runContext.AppConfig.Theme.PropertyKey   = key;
        runContext.AppConfig.Theme.PropertyValue = value;

        AboutScreen screen = Shown();
        (MascotControl mascot, ListView stats, ListView _) = Parts(screen);
        int gap = stats.Y - (mascot.Y + mascot.Height);

        string output = CapturedOutput();

        screen.Close();

        Assert.Equal(1 + 2, gap);
        Assert.Contains(Strings.AboutSamplingEngine, output);
        Assert.Contains(ConsolePalette.ForegroundSgr(key) + Strings.AboutLabelUptime, output);
        Assert.Contains(ConsolePalette.ForegroundSgr(value) + string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationSeconds, runContext.ServiceController.Delay / 1000.0), output);
    }

    // At 80×40 the three sections take 26 of the 38 rows between the bars: six spare above and below.
    [Fact]
    public void The_Sections_Shown_Are_Centred_In_The_Height()
    {
        runContext.ServiceController.AddService(new CpuService());

        AboutScreen screen = Shown(height: 40);
        (MascotControl mascot, ListView _, ListView samplers) = Parts(screen);
        (int top, int bottom) = (mascot.Y, samplers.Y + samplers.Height);

        screen.Close();

        Assert.Equal(1 + 6, top);
        Assert.Equal(39 - 6, bottom);
    }
}
