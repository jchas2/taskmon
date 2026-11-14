using System.Diagnostics;
using System.Globalization;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Services.Process;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;

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
        string heading = targets.Count == 1
            ? Strings.EndTaskHeadingOne
            : string.Format(CultureInfo.CurrentCulture, Strings.EndTaskHeadingMany, targets.Count);
        
        string more    = targets.Count > MaxEndTaskNamesShown
            ? " " + string.Format(CultureInfo.CurrentCulture, Strings.EndTaskMore, targets.Count - MaxEndTaskNamesShown)
            : string.Empty;
        
        string names   = string.Join(", ", targets.Take(MaxEndTaskNamesShown).Select(target => $"{target.Name} ({target.Pid})"));
        int    room    = Math.Max(1, width - more.Length);

        if (names.Length > room) {
            names = names[..Math.Max(0, room - 1)] + "…";
        }

        return string.Join('\n', heading, names + more, Strings.EndTaskChildren);
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

    private void EndTasks(IReadOnlyList<int> pids)
    {
        Trace.WriteLine($"End task pids: {string.Join(", ", pids)}");

        ProcessService processService = serviceController.GetService<ProcessService>();

        foreach (int pid in pids) {
            processService.EndTask(pid, EndTaskTimeout);
        }

        processService.RequestImmediateRefresh();
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
        endTaskDialog.Title   = targets.Count == 1 ? Strings.EndTaskTitleOne : Strings.EndTaskTitleMany;
        endTaskDialog.Text    = BuildEndTaskText(targets, endTaskDialog.Width - 4);

        endTaskDialog.Visible = true;
        endTaskDialog.ShowMessageBox();
    }
}
