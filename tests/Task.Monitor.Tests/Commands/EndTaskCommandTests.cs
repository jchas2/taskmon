using Task.Monitor.Commands;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Commands;

public sealed class EndTaskCommandTests
{
    [Fact]
    public void EndTask_Command_Should_Be_Disabled_When_ProcessControl_IsActive_But_Empty()
    {
        (RunContext context, MainScreen mainScreen) = CommandHelper.SetupMainScreenWithContext();
        EndTaskCommand cmd = new("End Task", mainScreen, context.AppConfig);

        Assert.IsType<ProcessControl>(mainScreen.GetActiveControl);
        Assert.False(cmd.IsEnabled);
    }
}