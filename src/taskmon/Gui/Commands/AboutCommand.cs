using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui.Commands;

public sealed class AboutCommand(string text, ScreenApplication screenApp) : AbstractCommand(text)
{
    public override void Execute() =>
        screenApp.ShowScreen<AboutScreen>();

    public override bool IsEnabled => true;
}
