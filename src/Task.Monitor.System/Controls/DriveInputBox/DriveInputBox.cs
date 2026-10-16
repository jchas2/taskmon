using System.Drawing;
using Task.Monitor.Cli.Utils;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
using ListViewColumnHeader = Task.Monitor.System.Controls.ListView.ListViewColumnHeader;
using ListViewItem = Task.Monitor.System.Controls.ListView.ListViewItem;

namespace Task.Monitor.System.Controls.DriveInputBox;

public sealed class DriveInputBox : Control
{
    private const int MinWidth     = 30;
    private const int MinHeight    = 8;
    private const int ButtonWidth  = 10;
    private const int ButtonHeight = 1;
    private const int ButtonGap    = 6;

    private const int ChromeHeight = 7;

    private readonly ListViewControl list;
    private readonly List<string>    paths = [];
    
    private bool okFocused = true;

    public static int GetPreferredHeight(int rowCount) => Math.Max(MinHeight, rowCount + ChromeHeight);

    public DriveInputBox(ISystemTerminal terminal) : base(terminal)
    {
        list = new ListViewControl(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = false,
            ShowBorder        = false,
            Visible           = true
        };

        list.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty));
    }

    public Color DialogBackgroundColour { get; set; } = ConsolePalette.Gray;
    public Color DialogBorderColour { get; set; } = ConsolePalette.Black;
    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;
    public Color DialogForegroundColour { get; set; } = ConsolePalette.Black;

    public Color ListBackgroundHighlightColour
    {
        get => list.BackgroundHighlightColour;
        set => list.BackgroundHighlightColour = value;
    }

    public Color ListBackgroundHighlightInactiveColour
    {
        get => list.BackgroundHighlightInactiveColour;
        set => list.BackgroundHighlightInactiveColour = value;
    }

    public Color ListForegroundHighlightColour
    {
        get => list.ForegroundHighlightColour;
        set => list.ForegroundHighlightColour = value;
    }

    public Color ListForegroundHighlightInactiveColour
    {
        get => list.ForegroundHighlightInactiveColour;
        set => list.ForegroundHighlightInactiveColour = value;
    }

    public string Title { get; set; } = string.Empty;

    public int ListX => list.X;
    public int ListY => list.Y;
    public int ListWidth => list.Width;

    public DriveInputBoxResult Result { get; private set; } = DriveInputBoxResult.None;

    public string? SelectedPath { get; private set; }

    public bool CustomPathRequested { get; private set; }

    public void SetCandidates(IReadOnlyList<string> candidatePaths)
    {
        paths.Clear();
        paths.AddRange(candidatePaths);
        paths.Add("Custom path...");

        list.Items.Clear();

        foreach (string path in paths) {
            list.Items.Add(new ListViewItem(path));
        }

        if (list.Items.Count > 0) {
            list.SelectedIndex = 0;
        }

        Result              = DriveInputBoxResult.None;
        SelectedPath        = null;
        CustomPathRequested = false;
        okFocused           = true;
    }

    public void ShowDriveInputBox()
    {
        OnResize();
        OnDraw();
    }

    protected override void OnResize()
    {
        list.X      = X + 1;
        list.Y      = Y + 2;
        list.Width  = Math.Max(0, Width - 2);
        list.Height = Math.Max(1, Height - ChromeHeight);
        
        list.ColumnHeaders[0].Width = list.Width;
        list.Resize();

        base.OnResize();
    }

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }

        DrawFrame();
        DrawList();
        DrawFooter();
    }

    private void DrawList()
    {
        bool redrawEnabled = Control.RedrawEnabled;
        Control.RedrawEnabled = true;

        try {
            list.Draw();
        }
        finally {
            Control.RedrawEnabled = redrawEnabled;
        }
    }

    private void DrawFrame()
    {
        DrawRectangle(
            X, 
            Y, 
            Width, 
            Height, 
            DialogBackgroundColour);

        int dialogWidth = Width - 2;
        string title    = Title.Length > 0 ? $" {Title} " : string.Empty;
        int titleLen    = Math.Min(title.Length, dialogWidth);
        int leftDashes  = (dialogWidth - titleLen) / 2;
        int rightDashes = dialogWidth - titleLen - leftDashes;

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, Y);
        Terminal.Write('╭');
        Terminal.Write('─', leftDashes);
        Terminal.Write(titleLen < title.Length ? title[..titleLen] : title);
        Terminal.Write('─', rightDashes);
        Terminal.Write('╮');

        string spacer = new(' ', dialogWidth);
        Terminal.SetCursorPosition(X, Y + 1);
        Terminal.Write($"│{spacer}│");

        for (int row = 0; row < list.Height; row++) {
            Terminal.SetCursorPosition(X, list.Y + row);
            Terminal.Write('│');
            Terminal.SetCursorPosition(X + Width - 1, list.Y + row);
            Terminal.Write('│');
        }
    }

    private void DrawFooter()
    {
        int dialogWidth = Width - 2;
        string spacer   = new(' ', dialogWidth);
        int y           = list.Y + list.Height;

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, y);
        Terminal.Write($"│{spacer}│");

        int buttonX = X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);
        int buttonY = ++y;

        Terminal.SetCursorPosition(X, buttonY);
        Terminal.Write($"│{spacer}│");

        DrawButton(
            buttonX, 
            buttonY, 
            ButtonWidth, 
            ButtonHeight, 
            "OK",
            selected: okFocused);
        
        DrawButton(
            buttonX + ButtonWidth + ButtonGap, 
            buttonY, 
            ButtonWidth, 
            ButtonHeight, 
            "Cancel", 
            selected: !okFocused);

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write($"│{spacer}│");

        string help = "↑ ↓ browse   ← → select button   ↵ confirm   Esc cancel";
        
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write($"│{help.CentreWithLength(dialogWidth)}│");

        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write('╰');
        Terminal.Write('─', dialogWidth);
        Terminal.Write('╯');
    }

    private void DrawButton(
        int x, 
        int y, 
        int width, 
        int height, 
        string text, 
        bool selected)
    {
        DrawRectangle(x, y, width, height, DialogBackgroundColour);

        string centredText = text.CentreWithLength(width);
        
        Terminal.BackgroundColor = DialogButtonBackgroundColour;
        Terminal.ForegroundColor = DialogButtonForegroundColour;
        Terminal.SetCursorPosition(x, y);

        bool isHighlightChar = true;

        foreach (char ch in centredText) {
            if (char.IsWhiteSpace(ch)) {
                Terminal.Write(ch);
                continue;
            }

            if (isHighlightChar && selected) {
                Terminal.ForegroundColor = ConsolePalette.Red;
                Terminal.Write(ch);
                Terminal.ForegroundColor = DialogButtonForegroundColour;
                isHighlightChar = false;
                continue;
            }

            Terminal.Write(ch);
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Result  = DriveInputBoxResult.None;
        handled = true;

        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow:
                okFocused = true;
                DrawFooter();
                break;

            case ConsoleKey.RightArrow:
                okFocused = false;
                DrawFooter();
                break;

            case ConsoleKey.Enter:
                Result = okFocused ? DriveInputBoxResult.Ok : DriveInputBoxResult.Cancel;
                ApplySelection();
                break;

            case ConsoleKey.Escape:
                Result = DriveInputBoxResult.Cancel;
                break;

            default:
                list.KeyPressed(keyInfo, ref handled);
                break;
        }
    }

    private void ApplySelection()
    {
        if (Result != DriveInputBoxResult.Ok) {
            return;
        }

        int index = list.SelectedIndex;

        if (index < 0 || index >= paths.Count) {
            return;
        }

        bool isCustomPathRow = index == paths.Count - 1;
        CustomPathRequested  = isCustomPathRow;
        SelectedPath         = isCustomPathRow ? null : paths[index];
    }
}
