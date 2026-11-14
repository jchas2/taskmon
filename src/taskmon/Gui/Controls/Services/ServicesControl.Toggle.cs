using System.Globalization;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Services.WindowsServices;
using MessageBox = Task.Monitor.System.Controls.MessageBox.MessageBox;

namespace Task.Monitor.Gui.Controls.Services;

public sealed partial class ServicesControl
{
    private const int MaxDependentsShown   = 2;
    private const int ToggleDialogMaxWidth = 60;
    private const int ToggleDialogMinWidth = 40;

    private sealed record ToggleFailure(string DisplayName, bool Start, ServiceControlOutcome Outcome);

    private readonly Dictionary<string, bool> inFlight = new(StringComparer.OrdinalIgnoreCase);

    private ToggleFailure? pendingFailure;

    internal static string BuildToggleFailureText(
        string                displayName,
        bool                  start,
        ServiceControlOutcome outcome,
        bool                  isElevated,
        int                   width)
    {
        string heading = Fit(string.Format(CultureInfo.CurrentCulture, start ? Strings.ServicesCouldNotStart : Strings.ServicesCouldNotStop, displayName), width);

        string[] reason = outcome.Result switch {
            ServiceControlResult.AccessDenied when isElevated => [Strings.ServicesRefused],
            ServiceControlResult.AccessDenied                 => [Strings.CommonNeedsAdministrator, Strings.CommonRunAsAdministrator],
            ServiceControlResult.Busy                         => [Strings.ServicesBusy],
            ServiceControlResult.CannotStop                   => [Strings.ServicesCannotStop],
            ServiceControlResult.DependencyFailed             => [Strings.ServicesDependencyFailed],
            ServiceControlResult.DependentsRunning            => DescribeDependents(outcome.Dependents ?? [], width),
            ServiceControlResult.DidNotStop                   => [Strings.ServicesDidNotStop],
            ServiceControlResult.Disabled                     => [Strings.ServicesDisabled],
            ServiceControlResult.NotFound                     => [Strings.CommonNoLongerExists],
            ServiceControlResult.NotSupported                 => [Strings.ServicesNotSupported],
            ServiceControlResult.StillPending                 => [start ? Strings.ServicesStillStarting : Strings.ServicesStillStopping],
            ServiceControlResult.StoppedAfterStarting         => [Strings.ServicesStoppedAfterStarting],
            ServiceControlResult.TimedOut                     => [Strings.ServicesTimedOut],
                                                            _ => [string.Format(CultureInfo.CurrentCulture, Strings.ServicesWindowsError, outcome.Win32Error)]
        };

        return string.Join('\n', [heading, .. reason]);
    }

    internal bool CanControl
    {
        get => servicesView.ShowCheckboxes;
        set => servicesView.ShowCheckboxes = value;
    }

    private void CloseToggleDialog()
    {
        if (!toggleDialog.Visible) {
            return;
        }

        toggleDialog.Visible  = false;
        Control.RedrawEnabled = true;
    }

    private static string[] DescribeDependents(IReadOnlyList<string> dependents, int width)
    {
        if (dependents.Count == 0) {
            return [Strings.ServicesDependentsRunning];
        }

        string names = string.Join(", ", dependents.Take(MaxDependentsShown));

        if (dependents.Count > MaxDependentsShown) {
            names += " " + string.Format(CultureInfo.CurrentCulture, Strings.ServicesDependentsMore, dependents.Count - MaxDependentsShown);
        }

        return [Strings.ServicesDependentsList, Fit(names, width)];
    }

    private static string Fit(string text, int width) =>
        text.Length <= width
            ? text
            : text[..Math.Max(0, width - 1)] + "…";

    private void OnControlCompleted(string serviceName, string displayName, bool start, ServiceControlOutcome outcome)
    {
        lock (DrawingLock) {
            if (!loaded) {
                return;
            }

            inFlight.Remove(serviceName);

            if (outcome.Result != ServiceControlResult.Succeeded) {
                pendingFailure = new ToggleFailure(displayName, start, outcome);
            }
        }

        if (outcome.Result == ServiceControlResult.NotFound) {
            TryRequestRefresh();
        }

        Draw();
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

    internal Action<WindowsServiceInfo, bool, Action<ServiceControlOutcome>> RequestControl { get; set; }

    private void RequestControlThroughService(WindowsServiceInfo service, bool start, Action<ServiceControlOutcome> completed)
    {
        WindowsServicesService windowsServices;

        try {
            windowsServices = serviceController.GetService<WindowsServicesService>();
        }
        catch (InvalidOperationException) {
            // No services service registered (e.g. under test) - nothing to change.
            completed(new ServiceControlOutcome(ServiceControlResult.NotSupported));
            return;
        }

        windowsServices.RequestControl(service.ServiceName, start, completed);
    }

    private void ShowPendingFailure()
    {
        if (pendingFailure is not { } failure || toggleDialog.Visible || !HasFocus) {
            return;
        }

        pendingFailure = null;

        Control.RedrawEnabled = false;

        toggleDialog.Width   = Math.Clamp(Width - 4, ToggleDialogMinWidth, ToggleDialogMaxWidth);
        toggleDialog.Height  = MessageBox.PreferredHeight;
        toggleDialog.X       = Math.Clamp(X + (Width - toggleDialog.Width) / 2, 0, Math.Max(0, Terminal.WindowWidth - toggleDialog.Width));
        toggleDialog.Y       = Math.Clamp(Y + (Height - toggleDialog.Height) / 2, 0, Math.Max(0, Terminal.WindowHeight - toggleDialog.Height));
        toggleDialog.Buttons = MessageBoxButtons.Ok;
        toggleDialog.Title   = failure.Start ? Strings.ServicesToggleTitleStart : Strings.ServicesToggleTitleStop;
        toggleDialog.Text    = BuildToggleFailureText(
            failure.DisplayName, failure.Start, failure.Outcome, SystemInfo.IsRunningAsRoot(), toggleDialog.Width - 4);

        toggleDialog.Visible = true;
        toggleDialog.ShowMessageBox();
    }

    private static bool ShowsAsRunning(WindowsServiceStatus status) =>
        status is not (WindowsServiceStatus.Stopped or WindowsServiceStatus.StopPending);

    internal static (string Key, string Label) ToggleHotKey => ("Space", Strings.ServicesToggleHotKey);

    private void ToggleSelectedService()
    {
        int index = servicesView.SelectedIndex;

        if (index < 0 || index >= visibleServices.Count) {
            return;
        }

        WindowsServiceInfo service = visibleServices[index];

        if (service.IsPending || inFlight.ContainsKey(service.ServiceName)) {
            return;
        }

        bool   start       = !ShowsAsRunning(service.Status);
        string serviceName = service.ServiceName;
        string displayName = service.DisplayName;

        inFlight[serviceName] = start;

        RequestControl(service, start, outcome => OnControlCompleted(serviceName, displayName, start, outcome));
        Draw();
    }
}
