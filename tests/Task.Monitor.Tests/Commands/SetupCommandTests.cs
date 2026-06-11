using Task.Monitor.Commands;
using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Tests.Commands;

public sealed class SetupCommandTests
{
    [Fact]
    public void Help_Command_Should_Be_Enabled()
    {
        (ScreenApplication screenApp, MainScreen mainScreen) = CommandHelper.SetupMainScreenWithScreenApp();
        SetupCommand cmd = new("Setup", screenApp);
        
        Assert.True(cmd.IsEnabled);
    }
}