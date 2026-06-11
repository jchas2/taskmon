using Moq;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Tests.Screens;

public sealed class ScreenTests
{
    public class TestScreen1(ISystemTerminal systemTerminal) : Screen(systemTerminal);
    public class TestScreen2(ISystemTerminal systemTerminal) : Screen(systemTerminal);

    [Fact]
    public void InputBox_Canary_Test() =>
        Assert.Equal(12, CanaryTestHelper.GetPropertyCount<TestScreen1>());

    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        TestScreen1 testScreen = new(terminalMock.Object);

        Assert.Equal(ConsoleColor.Black, testScreen.BackgroundColour);
        Assert.Empty(testScreen.Controls);
        Assert.True(testScreen.CursorVisible);
        Assert.Equal(ConsoleColor.White, testScreen.ForegroundColour);
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
