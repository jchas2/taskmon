using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.System.Controls.MessageBox;

namespace Task.Monitor.Commands;

public sealed class AboutCommand(string text, MainScreen mainScreen) : AbstractCommand(text)
{
    public override void Execute()
    {
        string version = AssemblyVersionInfo.GetVersion();

        mainScreen.ShowMessageBox(
            "About Task Monitor",
            $"\nVersion {version}",
            MessageBoxButtons.Ok,
            () => { });
    }

    public override bool IsEnabled => true;
}
