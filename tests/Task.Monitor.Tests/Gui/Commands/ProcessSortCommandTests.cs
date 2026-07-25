using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class ProcessSortCommandTests
{
    [Fact]
    public void ProcessSort_Command_Should_Be_Enabled_When_ProcessControl_IsActive_But_Empty()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ProcessSortCommand cmd = new("Sort", mainScreen);
        
        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.True(cmd.IsEnabled);
    }
}