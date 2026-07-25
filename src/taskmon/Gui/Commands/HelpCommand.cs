using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui.Commands;

public sealed class HelpCommand(string text, ScreenApplication screenApp) : AbstractCommand(text)
{
    public override void Execute() =>
        screenApp.ShowScreen<HelpScreen>();
    
    public override bool IsEnabled => true;
}