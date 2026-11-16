using System.Drawing;
using Task.Monitor.Cli.Utils;
using ListViewColumnHeader = Task.Monitor.System.Controls.ListView.ListViewColumnHeader;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
using ListViewItem = Task.Monitor.System.Controls.ListView.ListViewItem;

namespace Task.Monitor.System.Controls.DriveInputBox;

public sealed class DriveInputBox : Dialog
{
    private const int MinWidth     = 30;
    private const int MinHeight    = 8;
    private const int MinButtonWidth = 10;
    private const int ButtonGap    = 6;

    private const int ChromeHeight = 7;

    private readonly ListViewControl list;
    private readonly List<string>    paths = [];
    
    private bool okFocused = true;

    public DriveInputBox(ISystemTerminal terminal) : base(terminal)
    {
        list = new ListViewControl(terminal) {
            EnableScroll      = true,
            Focused           = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = false,
            ShowBorder        = false,
            Visible           = true,
            BackgroundColour  = ConsolePalette.Gray,
            ForegroundColour  = ConsolePalette.Black
        };

        list.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty));
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

    private int ButtonWidth => Math.Max(MinButtonWidth, Math.Max(OkText.Length, CancelText.Length) + 2);

    public string CancelText { get; set; } = string.Empty;

    public bool CustomPathRequested { get; private set; }

    public string CustomPathText { get; set; } = string.Empty;

    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonFocusColour { get; set; } = ConsolePalette.Red;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;

    private void DrawButton(int x, int y, string text, bool selected) =>
        base.DrawButton(
            x,
            y,
            ButtonWidth,
            text,
            selected,
            DialogButtonBackgroundColour,
            DialogButtonForegroundColour,
            DialogButtonFocusColour);

    private void DrawFooter()
    {
        int y = list.Y + list.Height;

        DrawRow(y, string.Empty);

        int buttonX = X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);
        int buttonY = ++y;

        DrawRow(buttonY, string.Empty);

        DrawButton(buttonX, buttonY, OkText, selected: okFocused);
        DrawButton(buttonX + ButtonWidth + ButtonGap, buttonY, CancelText, selected: !okFocused);

        DrawRow(++y, string.Empty);
        DrawRow(++y, HelpText);
        DrawBottomLine(++y);
    }

    private void DrawFrame()
    {
        DrawBackground();
        DrawTopLine();
        DrawRow(Y + 1, string.Empty);

        for (int row = 0; row < list.Height; row++) {
            DrawSide(X, list.Y + row);
            DrawSide(X + Width - 1, list.Y + row);
        }
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

    public static int GetPreferredHeight(int rowCount) => Math.Max(MinHeight, rowCount + ChromeHeight);

    public string HelpText { get; set; } = string.Empty;

    public Color ListBackgroundColour
    {
        get => list.BackgroundColour;
        set => list.BackgroundColour = value;
    }

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

    public Color ListForegroundColour
    {
        get => list.ForegroundColour;
        set => list.ForegroundColour = value;
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

    public int ListWidth => list.Width;

    public int ListX => list.X;
    public int ListY => list.Y;

    public string OkText { get; set; } = string.Empty;

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }

        DrawFrame();
        DrawList();
        DrawFooter();
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

    public DriveInputBoxResult Result { get; private set; } = DriveInputBoxResult.None;

    public string? SelectedPath { get; private set; }

    public int SelectedRowY => list.Y + Math.Max(0, list.SelectedIndex - list.TopIndex);

    public void SetCandidates(IReadOnlyList<string> candidatePaths)
    {
        paths.Clear();
        paths.AddRange(candidatePaths);
        paths.Add(CustomPathText);

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
}
