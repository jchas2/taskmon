using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Screens;

public partial class Screen : Control
{
    private Control? focusedControl;

    public Screen(ISystemTerminal systemTerminal) : base(systemTerminal)
    {
    }

    public void Close()
    {
        Unload();
        IsActive = false;
    }

    // The control holding focus, however deeply it is nested.
    protected Control? CurrentFocus => focusedControl;

    private void Focus()
    {
        Control? control = SelectFirstControl(currentControl: this, lookForward: true);

        if (control != null) {
            FocusInternal(control);
        }
    }

    internal void FocusInternal(Control control)
    {
        if (focusedControl == control) {
            return;
        }

        Control? previous = focusedControl;

        lock (DrawingLock) {
            if (previous != null) {
                previous.Focused = false;
                previous.LostFocus();
                previous.Draw();
            }

            focusedControl = control;

            control.Focused = true;
            control.GotFocus();
            control.Draw();
        }
    }

    private bool IsActive { get; set; } = false;

    protected override void OnLoad()
    {
        focusedControl = SelectNextControl(null, lookForward: true);

        base.OnLoad();
    }

    protected virtual void OnShown() { }

    public void Show()
    {
        Load();
        Clear();
        Resize();
        Focus();
        Draw();
        OnShown();
        IsActive = true;
    }
}
