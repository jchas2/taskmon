using System.Diagnostics;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls.InputBox;

namespace Task.Monitor.Gui.Commands;

public sealed class FilterCommand(string text, MainScreen mainScreen) : AbstractCommand(text)
{
    public override void Execute()
    {
        if (!IsEnabled) {
            return;
        }

        void FilterAction(string filter, InputBoxResult result)
        {
            if (result == InputBoxResult.Enter) {
                ProcessControl.FilterText = filter;
                Trace.WriteLine($"Filter applied: {filter}.");
                
                if (!Text.StartsWith("*")) {
                    Text = "*" + Text;
                }
            }
            else if (result == InputBoxResult.Cancel) {
                ProcessControl.FilterText = string.Empty;
                Trace.WriteLine("Filter reset.");
                
                if (Text.StartsWith("*")) {
                    Text = Text.Substring(1, Text.Length - 1);
                }
            }

            mainScreen.ShowCommandControl();
        }

        mainScreen.ShowFilterControl(FilterAction);
    }

    public override bool IsEnabled
        => mainScreen.GetActiveControl is ProcessControl;

    private ProcessControl ProcessControl
        => mainScreen.GetControl<ProcessControl>();
}