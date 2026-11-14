using System.Globalization;
using Task.Monitor.Resources;
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

#if __APPLE__
    private const string PlatformName = "macOS";
#else
    private const string PlatformName = "Windows";
#endif

    internal static string BuildToggleFailureText(
        StartupEntry        entry,
        bool                enable,
        StartupToggleResult result,
        bool                isElevated,
        int                 width)
    {
        string heading = string.Format(CultureInfo.CurrentCulture, enable ? Strings.StartupCouldNotEnable : Strings.StartupCouldNotDisable, entry.Name);

        if (heading.Length > width) {
            heading = heading[..Math.Max(0, width - 1)] + "…";
        }

        string[] reason = result switch {
            StartupToggleResult.AccessDenied when isElevated => [string.Format(CultureInfo.CurrentCulture, Strings.StartupRefused, PlatformName)],
            StartupToggleResult.AccessDenied                 => ElevationReason,
            StartupToggleResult.NotFound                     => [Strings.CommonNoLongerExists],
            StartupToggleResult.NotSupported when entry.State == StartupEntryState.Unknown
                                                             => [Strings.StartupStateUnreadable],
            StartupToggleResult.NotSupported                 => [string.Format(CultureInfo.CurrentCulture, enable ? Strings.StartupCannotEnableType : Strings.StartupCannotDisableType, DescribeType(entry.Source)), .. NotSupportedHint],
            StartupToggleResult.RequiresApproval             => [Strings.StartupAllowInSettings, Strings.StartupAllowInLoginItems],
                                                           _ => [string.Format(CultureInfo.CurrentCulture, Strings.StartupCouldNotChange, PlatformName)]
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

#if __APPLE__
    private static string[] ElevationReason => [Strings.StartupNeedsRoot, Strings.StartupRunWithSudo];
#else
    private static string[] ElevationReason => [Strings.CommonNeedsAdministrator, Strings.CommonRunAsAdministrator];
#endif

#if __APPLE__
    private static string[] NotSupportedHint => [Strings.StartupUseLoginItems];
#else
    private static string[] NotSupportedHint => [];
#endif

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
        toggleDialog.Title   = enable ? Strings.StartupToggleTitleEnable : Strings.StartupToggleTitleDisable;
        toggleDialog.Text    = BuildToggleFailureText(entry, enable, result, SystemInfo.IsRunningAsRoot(), toggleDialog.Width - 4);

        toggleDialog.Visible = true;
        toggleDialog.ShowMessageBox();
    }

    internal static (string Key, string Label) ToggleHotKey => ("Space", Strings.StartupToggleHotKey);

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
