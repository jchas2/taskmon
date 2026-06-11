using Task.Monitor.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Commands;

public sealed class ProcessInfoCommandTests
{
    [Fact]
    public void ProcessInfo_Command_Should_Be_Disabled_When_ProcessControl_IsActive_But_Empty()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ProcessInfoCommand cmd = new("Info", mainScreen);

        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.False(cmd.IsEnabled);
    }
}
