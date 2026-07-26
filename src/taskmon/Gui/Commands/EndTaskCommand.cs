using System.Diagnostics;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Process;

namespace Task.Monitor.Gui.Commands;

public sealed class EndTaskCommand(
    string text, 
    MainScreen mainScreen,
    AppConfig appConfig) : ProcessCommand(text, mainScreen)
{
    private const int EndTaskTimeout = 0;
    private const int MaxPidsToShow = 3;

    public override void Execute()
    {
        if (!IsEnabled) {
            return;
        }

        List<int> selectedProcesses = [];
        
        if (appConfig.MultiSelectProcesses && ProcessControl.CheckedProcesses.Count > 0) {
            selectedProcesses.AddRange(ProcessControl.CheckedProcesses);
        }
        else {
            selectedProcesses.Add(SelectedProcessId);
        }

        Action action = () => {
            foreach (int pid in selectedProcesses) {
                ProcessUtils.EndTask(pid, EndTaskTimeout);
            }
        };

        string pidStr = string.Join(", ", selectedProcesses);
        Trace.WriteLine($"Attempt to terminate pids: {pidStr}");
        
        pidStr = string.Join(", ", selectedProcesses.Take(MaxPidsToShow));

        if (selectedProcesses.Count > MaxPidsToShow) {
            pidStr += "...";
        }
        
        if (appConfig.ConfirmTaskDelete) {
            MainScreen.ShowMessageBox(
                "End Task(s)",
                $"Force Task termination for Pid(s):\n{pidStr}\n\nAre you sure you want to continue?",
                MessageBoxButtons.OkCancel,
                action);
        }
        else {
            action.Invoke();
        }        
    }
}
