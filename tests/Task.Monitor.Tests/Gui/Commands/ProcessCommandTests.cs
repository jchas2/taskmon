using System.Runtime.CompilerServices;
using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Gui.Commands;

public sealed class ProcessCommandTests
{
    [Fact]
    public void Process_Command_Should_Be_Disabled_By_Default_When_ProcessControl_Empty()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ProcessCommand cmd = new("Process", mainScreen);
        
        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.False(cmd.IsEnabled);
    }
    
    [Fact]
    public void Process_Command_Should_Return_Negative_ProcessId_When_ProcessControl_Empty()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        ProcessCommand cmd = new("Process", mainScreen);
        
        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.Equal(-1, cmd.SelectedProcessId);
    }
}
