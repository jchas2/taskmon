using Task.Monitor.Commands;
using Task.Monitor.Gui;

namespace Task.Monitor.Tests.Commands;

public sealed class AboutCommandTests
{
    [Fact]
    public void About_Command_Should_Be_Enabled()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        AboutCommand cmd = new("About", mainScreen);
        
        Assert.True(cmd.IsEnabled);
    }
}
