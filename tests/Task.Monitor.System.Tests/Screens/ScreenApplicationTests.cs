using Moq;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.System.Tests.Screens;

public sealed class ScreenApplicationTests
{
    [Fact]
    public void Should_Register_Screen()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        ForwardingTerminal terminal = new(terminalMock.Object);
        ScreenApplication screenApp = new(terminal);

        screenApp.RegisterScreen(new ScreenTests.TestScreen1(terminal));
        screenApp.ShowScreen<ScreenTests.TestScreen1>();
        
        terminalMock.Verify(terminal => terminal.WindowHeight, Times.Once);
        terminalMock.Verify(terminal => terminal.WindowWidth, Times.Once);
    }

    [Fact]
    public void ShowScreen_Throws_InvalidOperationException_When_Screen_Is_Not_Registered()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        ScreenApplication screenApp = new(new ForwardingTerminal(terminalMock.Object));

        Assert.Throws<InvalidOperationException>(screenApp.ShowScreen<ScreenTests.TestScreen2>);
    }

    [Fact]
    public void Should_Set_OwnerScreen()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        ForwardingTerminal terminal = new(terminalMock.Object);
        ScreenApplication.ScreenApplicationContext appContext = new(terminal);

        ScreenTests.TestScreen1 testScreen = new(terminal);
        appContext.OwnerScreen = testScreen;
        
        terminalMock.Verify(terminal => terminal.WindowHeight, Times.Once);
        terminalMock.Verify(terminal => terminal.WindowWidth, Times.Once);
        
        ScreenTests.TestScreen1? result = appContext.OwnerScreen as ScreenTests.TestScreen1;
        
        Assert.True(result == testScreen);
    }
}
