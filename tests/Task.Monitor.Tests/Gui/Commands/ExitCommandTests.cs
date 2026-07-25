using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class ExitCommandTests
{
    [Fact]
    public void Exit_Command_Should_Be_Enabled()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ExitCommand cmd = new("Exit");

        Assert.True(cmd.IsEnabled);
    }
}
