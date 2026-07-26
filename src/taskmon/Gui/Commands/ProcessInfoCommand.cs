using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Gui.Commands;

public sealed class ProcessInfoCommand(string text, MainScreen mainScreen) : ProcessCommand(text, mainScreen)
{
    public override void Execute()
    {
        if (!IsEnabled) {
            return;
        }

        var processInfoControl = MainScreen.GetControl<ProcessInfoControl>();
        processInfoControl.SelectedProcessId = SelectedProcessId;
        MainScreen.SetActiveControl<ProcessInfoControl>();
        MainScreen.Draw();
    }
}
