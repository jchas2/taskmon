using System.Diagnostics;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Services.Process;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;
using ProcessUtils = Task.Monitor.System.Process.ProcessUtils;

namespace Task.Monitor.Gui.Controls.Processes;

public sealed partial class ProcessControl
{
    private const int EndTaskDialogMaxWidth = 60;
    private const int EndTaskDialogMinWidth = 40;
    private const int EndTaskTimeout        = 0;
    private const int MaxEndTaskNamesShown  = 3;

    private IReadOnlyList<int> pendingEndTaskPids = [];

    private static string BuildEndTaskText(IReadOnlyList<(int Pid, string Name)> targets, int width)
    {
        string heading = targets.Count == 1 ? "End this task?" : $"End these {targets.Count} tasks?";
        string more    = targets.Count > MaxEndTaskNamesShown
            ? $" and {targets.Count - MaxEndTaskNamesShown} more"
            : string.Empty;
        string names   = string.Join(", ", targets.Take(MaxEndTaskNamesShown).Select(target => $"{target.Name} ({target.Pid})"));
        int    room    = Math.Max(1, width - more.Length);

        if (names.Length > room) {
            names = names[..Math.Max(0, room - 1)] + "…";
        }

        return string.Join('\n', heading, names + more, "Child processes are also ended.");
    }

    private void CloseEndTaskDialog()
    {
        pendingEndTaskPids = [];

        if (!endTaskDialog.Visible) {
            return;
        }

        endTaskDialog.Visible = false;
        Control.RedrawEnabled = true;
    }

    internal Action<int> EndTask { get; set; } = pid => ProcessUtils.EndTask(pid, EndTaskTimeout);

    private void EndTasks(IReadOnlyList<int> pids)
    {
        Trace.WriteLine($"End task pids: {string.Join(", ", pids)}");

        foreach (int pid in pids) {
            EndTask(pid);
        }

        try {
            serviceController.GetService<ProcessService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No process service registered (e.g. under test) - the next sample picks it up.
        }
    }

    private List<(int Pid, string Name)> GetEndTaskTargets()
    {
        lock (DrawingLock) {
            List<ListViewItem> rows = processView.ShowCheckboxes
                ? processView.Items.Where(item => item.Checked).ToList()
                : [];

            if (rows.Count == 0 && processView.SelectedItem is { } selected) {
                rows.Add(selected);
            }

            return rows
                .OfType<ProcessListViewItem>()
                .Where(item => item.Pid != Environment.ProcessId)
                .Select(item => (item.Pid, item.SubItems[(int)Columns.Process].Text))
                .ToList();
        }
    }

    public bool IsEndTaskDialogVisible => endTaskDialog.Visible;

    private void OnEndTaskDialogKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        endTaskDialog.KeyPressed(keyInfo, ref handled);

        if (endTaskDialog.Result == MessageBoxResult.None) {
            return;
        }

        IReadOnlyList<int> pids = endTaskDialog.Result == MessageBoxResult.Ok ? pendingEndTaskPids : [];

        CloseEndTaskDialog();

        if (pids.Count > 0) {
            EndTasks(pids);
        }

        Draw();
    }

    private void RequestEndTask()
    {
        List<(int Pid, string Name)> targets = GetEndTaskTargets();

        if (targets.Count == 0) {
            return;
        }

        List<int> pids = targets.Select(target => target.Pid).ToList();

        if (!appConfig.ConfirmTaskDelete) {
            EndTasks(pids);
            return;
        }

        ShowEndTaskDialog(targets, pids);
    }

    private void ShowEndTaskDialog(IReadOnlyList<(int Pid, string Name)> targets, IReadOnlyList<int> pids)
    {
        Control.RedrawEnabled = false;
        pendingEndTaskPids    = pids;

        endTaskDialog.Width   = Math.Clamp(Width - 4, EndTaskDialogMinWidth, EndTaskDialogMaxWidth);
        endTaskDialog.Height  = MessageBoxControl.PreferredHeight;
        endTaskDialog.X       = Math.Clamp(X + (Width - endTaskDialog.Width) / 2, 0, Math.Max(0, Terminal.WindowWidth - endTaskDialog.Width));
        endTaskDialog.Y       = Math.Clamp(Y + (Height - endTaskDialog.Height) / 2, 0, Math.Max(0, Terminal.WindowHeight - endTaskDialog.Height));
        endTaskDialog.Buttons = MessageBoxButtons.OkCancel;
        endTaskDialog.Title   = targets.Count == 1 ? "End Task" : "End Tasks";
        endTaskDialog.Text    = BuildEndTaskText(targets, endTaskDialog.Width - 4);

        endTaskDialog.Visible = true;
        endTaskDialog.ShowMessageBox();
    }
}
