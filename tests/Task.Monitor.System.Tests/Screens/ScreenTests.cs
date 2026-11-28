using System.Drawing;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
namespace Task.Monitor.System.Tests.Screens;

public sealed class ScreenTests
{
    public class TestScreen1(ISystemTerminal systemTerminal) : Screen(systemTerminal);
    public class TestScreen2(ISystemTerminal systemTerminal) : Screen(systemTerminal);

    // Loads none of its controls, as a screen that loads only what it shows does - so
    // Screen.OnLoad, which picks the first control to focus, never runs.
    private sealed class SelectiveLoadScreen(ISystemTerminal systemTerminal) : Screen(systemTerminal)
    {
        protected override void OnLoad() { }
    }

    private sealed class DrawCountingControl(ISystemTerminal terminal) : Control(terminal)
    {
        public int DrawCount { get; private set; }

        protected override void OnDraw() => DrawCount++;
    }

    [Fact]
    public void Close_Drops_Focus_So_The_Next_Show_Does_Not_Redraw_The_Control_That_Had_It()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        SelectiveLoadScreen   screen       = new(terminalMock.Object);
        DrawCountingControl   first        = new(terminalMock.Object) { TabStop = true, TabIndex = 1 };
        DrawCountingControl   second       = new(terminalMock.Object) { TabStop = true, TabIndex = 2 };

        screen.Controls.Add(first).Add(second);
        screen.Show();
        second.SetFocus();

        screen.Close();

        Assert.False(second.Focused);

        int drawsBeforeShow = second.DrawCount;
        screen.Show();

        Assert.Equal(drawsBeforeShow, second.DrawCount);
        Assert.True(first.Focused);
    }

    [Fact]
    public void Screen_Canary_Test() =>
        Assert.Equal(15, CanaryTestHelper.GetPropertyCount<TestScreen1>());

    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        TestScreen1 testScreen = new(terminalMock.Object);

        Assert.Equal(ConsolePalette.Black, testScreen.BackgroundColour);
        Assert.Empty(testScreen.Controls);
        Assert.Equal(ConsolePalette.White, testScreen.ForegroundColour);
        Assert.Equal(0, testScreen.Height);
        Assert.NotNull(testScreen.Name);
        Assert.Empty(testScreen.Name);
        Assert.True(0 == testScreen.TabIndex);
        Assert.False(testScreen.TabStop);
        Assert.True(testScreen.Visible);
        Assert.Equal(0, testScreen.Width);
        Assert.Equal(0, testScreen.X);
        Assert.Equal(0, testScreen.Y);
    }
}
