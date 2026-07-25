using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class AboutCommandTests
{
    [Fact]
    public void About_Command_Should_Be_Enabled()
    {
        (ScreenApplication screenApp, MainScreen _) = CommandHelper.SetupMainScreenWithScreenApp();
        AboutCommand cmd = new("About", screenApp);
        
        Assert.True(cmd.IsEnabled);
    }
}
