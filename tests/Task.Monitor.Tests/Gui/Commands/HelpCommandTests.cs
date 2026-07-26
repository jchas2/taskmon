using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class HelpCommandTests
{
    [Fact]
    public void Help_Command_Should_Be_Enabled()
    {
        (ScreenApplication screenApp, MainScreen mainScreen) = CommandHelper.SetupMainScreenWithScreenApp();
        HelpCommand cmd = new("Help", screenApp);
        
        Assert.True(cmd.IsEnabled);
    }
}
