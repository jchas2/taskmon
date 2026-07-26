using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Gui.Commands;

public sealed class ProcessesCommand(string text, MainScreen mainScreen) : AbstractCommand(text)
{
    public override void Execute()
    {
        _ = mainScreen.SetActiveControl<ProcessControl>();
        mainScreen.Draw();
    }

    public override bool IsEnabled => true;
}
