using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class ProcessesCommandTests
{
    [Fact]
    public void Processes_Command_Should_Be_Enabled_By_Default()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ProcessesCommand cmd = new("Processes", mainScreen);
        
        Assert.True(cmd.IsEnabled);
    }
}
