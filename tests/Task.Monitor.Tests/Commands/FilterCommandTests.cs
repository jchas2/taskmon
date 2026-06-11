using Moq;
using Task.Monitor.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Screens;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Commands;

public sealed class FilterCommandTests
{
    [Fact]
    public void Filter_Command_Should_Be_Enabled_When_ProcessControl_IsActive_But_Empty()
    {
        MainScreen mainScreen = CommandHelper.SetupMainScreen();
        FilterCommand cmd = new("Filter", mainScreen);
        
        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.True(cmd.IsEnabled);
    }
}
