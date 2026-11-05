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

    internal static readonly (string Key, string Label) ToggleHotKey = ("Space", "Start/Stop");

    private sealed record ToggleFailure(string DisplayName, bool Start, ServiceControlOutcome Outcome);

    // Requests sent and not yet completed, by service name: true to start, false to stop.
    private readonly Dictionary<string, bool> inFlight = new(StringComparer.OrdinalIgnoreCase);

    private ToggleFailure? pendingFailure;

    internal static string BuildToggleFailureText(
        string                displayName,
        bool                  start,
        ServiceControlOutcome outcome,
        bool                  isElevated,
        int                   width)
    {
        string action  = start ? "start" : "stop";
        string heading = Fit($"Couldn't {action} {displayName}", width);

        string[] reason = outcome.Result switch {
            ServiceControlResult.AccessDenied when isElevated => ["Windows refused the change."],
            ServiceControlResult.AccessDenied                 => ["It needs administrator rights.", "Run taskmon as administrator."],
            ServiceControlResult.Busy                         => ["It's busy. Try again in a moment."],
            ServiceControlResult.CannotStop                   => ["It doesn't accept a stop request."],
            ServiceControlResult.DependencyFailed             => ["A service it depends on couldn't start."],
            ServiceControlResult.DependentsRunning            => DescribeDependents(outcome.Dependents ?? [], width),
            ServiceControlResult.DidNotStop                   => ["It's still running."],
            ServiceControlResult.Disabled                     => ["Its startup type is Disabled."],
            ServiceControlResult.NotFound                     => ["It no longer exists."],
            ServiceControlResult.NotSupported                 => ["Services can't be started or stopped here."],
            ServiceControlResult.StillPending                 => [$"It's still {(start ? "starting" : "stopping")} after a minute."],
            ServiceControlResult.StoppedAfterStarting         => ["It started and then stopped."],
            ServiceControlResult.TimedOut                     => ["It didn't respond in time."],
                                                            _ => [$"Windows error {outcome.Win32Error}."]
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
            return ["Other running services depend on it."];
        }

        string names = string.Join(", ", dependents.Take(MaxDependentsShown));

        if (dependents.Count > MaxDependentsShown) {
            names += $" and {dependents.Count - MaxDependentsShown} more";
        }

        return ["Running services depend on it:", Fit(names, width)];
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
        toggleDialog.Title   = failure.Start ? "Start Service" : "Stop Service";
        toggleDialog.Text    = BuildToggleFailureText(
            failure.DisplayName, failure.Start, failure.Outcome, SystemInfo.IsRunningAsRoot(), toggleDialog.Width - 4);

        toggleDialog.Visible = true;
        toggleDialog.ShowMessageBox();
    }

    private static bool ShowsAsRunning(WindowsServiceStatus status) =>
        status is not (WindowsServiceStatus.Stopped or WindowsServiceStatus.StopPending);

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
