using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Gui.Commands;

public class ProcessCommand(string text, MainScreen mainScreen) : AbstractCommand(text)
{
    protected MainScreen MainScreen { get; } = mainScreen;
    
    public override void Execute() => throw new NotImplementedException();

    public override bool IsEnabled =>
        MainScreen.GetActiveControl is ProcessControl && 
        ProcessControl.SelectedProcessId > -1;

    protected ProcessControl ProcessControl
        => MainScreen.GetControl<ProcessControl>();
    
    protected internal int SelectedProcessId => ProcessControl.SelectedProcessId; 
}
