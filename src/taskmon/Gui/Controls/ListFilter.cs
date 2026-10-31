using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.TextInputDialog;
using TextInputDialogControl = Task.Monitor.System.Controls.TextInputDialog.TextInputDialog;

namespace Task.Monitor.Gui.Controls;

internal sealed class ListFilter
{
    private const int DialogMaxWidth = 50;
    private const int DialogMinWidth = 30;

    public const string NoMatchesText = "No matches";

    private readonly Control         owner;
    private readonly ISystemTerminal terminal;

    public ListFilter(Control owner, ISystemTerminal terminal, string title)
    {
        this.owner    = owner;
        this.terminal = terminal;

        Dialog = new TextInputDialogControl(terminal) {
            Visible = false,
            Title   = title
        };
    }

    public void ApplyTheme(Theme theme) => CommonControl.Theme(Dialog, theme);

    public string BuildHeaderText(int matched, int total, bool showTotal = true)
    {
        if (!IsActive) {
            return string.Empty;
        }

        return showTotal
            ? $"Filter: {Text} ({matched} of {total})"
            : $"Filter: {Text} ({matched})";
    }

    public void Close()
    {
        if (!Dialog.Visible) {
            return;
        }

        Dialog.Visible        = false;
        Control.RedrawEnabled = true;
    }

    public TextInputDialogControl Dialog { get; }

    public void Draw()
    {
        if (Dialog.Visible) {
            Dialog.Draw();
        }
    }

    // The filter's footer hotkeys; Esc to clear only appears while a filter is applied.
    public (string Key, string Label)[] FooterHotKeys =>
        IsActive
            ? [CommonControl.FilterHotKey, CommonControl.ClearFilterHotKey]
            : [CommonControl.FilterHotKey];

    public bool IsActive => !string.IsNullOrEmpty(Text);

    public bool IsDialogVisible => Dialog.Visible;

    // Returns true when the owner must redraw: the dialog closed or the filter was cleared.
    public bool KeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (Dialog.Visible) {
            Dialog.KeyPressed(keyInfo, ref handled);

            if (Dialog.Result == TextInputDialogResult.None) {
                return false;
            }

            Close();

            if (Dialog.Result == TextInputDialogResult.Ok) {
                Text = Dialog.Text.Trim();
            }

            return true;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.F:
                Show();
                handled = true;
                return false;

            case ConsoleKey.Escape when IsActive:
                Text    = string.Empty;
                handled = true;
                return true;
        }

        return false;
    }

    public bool Matches(params string?[] fields)
    {
        if (!IsActive) {
            return true;
        }

        foreach (string? field in fields) {
            if (field?.Contains(Text, StringComparison.CurrentCultureIgnoreCase) == true) {
                return true;
            }
        }

        return false;
    }

    private void Show()
    {
        Control.RedrawEnabled = false;

        Dialog.Width  = Math.Clamp(owner.Width - 4, DialogMinWidth, DialogMaxWidth);
        Dialog.Height = TextInputDialogControl.PreferredHeight;
        Dialog.X      = Math.Clamp(owner.X + (owner.Width - Dialog.Width) / 2, 0, Math.Max(0, terminal.WindowWidth - Dialog.Width));
        Dialog.Y      = Math.Clamp(owner.Y + (owner.Height - Dialog.Height) / 2, 0, Math.Max(0, terminal.WindowHeight - Dialog.Height));

        Dialog.SetText(Text);
        Dialog.Visible = true;
        Dialog.ShowTextInputDialog();
    }

    public string Text { get; set; } = string.Empty;
}
