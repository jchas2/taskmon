using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Commands;

public class ProcessSortCommand(string text, MainScreen mainScreen) : AbstractCommand(text) 
{
    public MainScreen MainScreen { get; } = mainScreen;

    public override void Execute()
    {
        if (!IsEnabled) {
            return;
        }

        var processControl = MainScreen.GetActiveControl as ProcessControl;
        processControl!.SetMode(ProcessControl.ControlMode.SortSelection);
    }

    public override bool IsEnabled  => 
        MainScreen.GetActiveControl is ProcessControl;
}