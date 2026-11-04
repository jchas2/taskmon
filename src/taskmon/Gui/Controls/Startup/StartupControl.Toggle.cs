using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Services.Startup;
using MessageBox = Task.Monitor.System.Controls.MessageBox.MessageBox;

namespace Task.Monitor.Gui.Controls.Startup;

public sealed partial class StartupControl
{
    private const int ToggleDialogMaxWidth = 60;
    private const int ToggleDialogMinWidth = 40;

    internal static readonly (string Key, string Label) ToggleHotKey = ("Space", "Enable/Disable");

    internal static string BuildToggleFailureText(
        StartupEntry        entry,
        bool                enable,
        StartupToggleResult result,
        bool                isElevated,
        int                 width)
    {
        string action  = enable ? "enable" : "disable";
        string heading = $"Couldn't {action} {entry.Name}";

        if (heading.Length > width) {
            heading = heading[..Math.Max(0, width - 1)] + "…";
        }

        string[] reason = result switch {
            StartupToggleResult.AccessDenied when isElevated => ["Windows refused the change."],
            StartupToggleResult.AccessDenied                 => ["It needs administrator rights.", "Run taskmon as administrator."],
            StartupToggleResult.NotFound                     => ["It no longer exists."],
            StartupToggleResult.NotSupported                 => [$"{DescribeType(entry.Source)} items can't be {action}d."],
                                                           _ => ["Windows couldn't change it."]
        };

        return string.Join('\n', [heading, .. reason]);
    }

    internal bool CanSetEnabled
    {
        get => startupView.ShowCheckboxes;
        set => startupView.ShowCheckboxes = value;
    }

    private void CloseToggleDialog()
    {
        if (!toggleDialog.Visible) {
            return;
        }

        toggleDialog.Visible  = false;
        Control.RedrawEnabled = true;
    }

    private void OnToggleDialogKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        toggleDialog.KeyPressed(keyInfo, ref handled);

        if (toggleDialog.Result == MessageBoxResult.None) {
            return;
        }

        CloseToggleDialog();
        Draw();
    }

    private StartupToggleResult SetEnabledThroughService(StartupEntry entry, bool enabled)
    {
        StartupService service;

        try {
            service = serviceController.GetService<StartupService>();
        }
        catch (InvalidOperationException) {
            // No startup service registered (e.g. under test) - nothing to change.
            return StartupToggleResult.NotSupported;
        }

        return service.SetEnabled(entry, enabled);
    }

    internal Func<StartupEntry, bool, StartupToggleResult> SetEntryEnabled { get; set; }

    private void ShowToggleDialog(StartupEntry entry, bool enable, StartupToggleResult result)
    {
        Control.RedrawEnabled = false;

        toggleDialog.Width   = Math.Clamp(Width - 4, ToggleDialogMinWidth, ToggleDialogMaxWidth);
        toggleDialog.Height  = MessageBox.PreferredHeight;
        toggleDialog.X       = Math.Clamp(X + (Width - toggleDialog.Width) / 2, 0, Math.Max(0, Terminal.WindowWidth - toggleDialog.Width));
        toggleDialog.Y       = Math.Clamp(Y + (Height - toggleDialog.Height) / 2, 0, Math.Max(0, Terminal.WindowHeight - toggleDialog.Height));
        toggleDialog.Buttons = MessageBoxButtons.Ok;
        toggleDialog.Title   = enable ? "Enable Startup App" : "Disable Startup App";
        toggleDialog.Text    = BuildToggleFailureText(entry, enable, result, SystemInfo.IsRunningAsRoot(), toggleDialog.Width - 4);

        toggleDialog.Visible = true;
        toggleDialog.ShowMessageBox();
    }

    private void ToggleSelectedEntry()
    {
        int index = startupView.SelectedIndex;

        if (index < 0 || index >= visibleEntries.Count) {
            return;
        }

        StartupEntry        entry  = visibleEntries[index];
        bool                enable = entry.State != StartupEntryState.Enabled;
        StartupToggleResult result = SetEntryEnabled(entry, enable);

        if (result == StartupToggleResult.Succeeded) {
            Draw();
            return;
        }

        if (result == StartupToggleResult.NotFound) {
            TryRequestRefresh();
        }

        ShowToggleDialog(entry, enable, result);
    }
}
