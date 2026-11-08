using Moq;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls.Mascot;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui;

public sealed class AboutScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public AboutScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    [Fact]
    public void AboutScreen_Canary_Test() =>
        Assert.Equal(15, CanaryTestHelper.GetPropertyCount<AboutScreen>());

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_Hosts_Just_The_Mascot()
    {
        AboutScreen screen = new(runContext);

        Assert.IsType<MascotControl>(Assert.Single(screen.Controls));
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

    [Fact]
    public void OnKeyPressed_Skips_The_Intro_With_Any_Other_Key()
    {
        AboutScreen screen = Shown();

        bool skipped = false;
        screen.KeyPressed(Key(ConsoleKey.Spacebar), ref skipped);

        // Once the intro is over, keys are no longer the mascot's.
        bool handledAfter = false;
        screen.KeyPressed(Key(ConsoleKey.Spacebar), ref handledAfter);

        screen.Close();

        Assert.True(skipped);
        Assert.False(handledAfter);
        Assert.Contains("Task Monitor", CapturedOutput());
    }

    private AboutScreen Shown()
    {
        AboutScreen screen = new(runContext) {
            Width  = 80,
            Height = 30
        };

        screen.Show();

        return screen;
    }
}
