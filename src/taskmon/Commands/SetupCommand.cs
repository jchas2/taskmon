using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Commands;

public sealed class SetupCommand(string text, ScreenApplication screenApp) : AbstractCommand(text)
{
    public override void Execute() =>
        screenApp.ShowScreen<SetupScreen>();
    
    public override bool IsEnabled => true;
}
