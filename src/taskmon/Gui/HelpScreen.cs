using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public class HelpScreen : Screen
{
#if __WIN32__
    private const string SpaceHelpText = "Check/uncheck items; toggle STARTUP or SERVICES";
#elif __APPLE__
    private const string SpaceHelpText = "Check/uncheck items; enable/disable in STARTUP";
#else
    private const string SpaceHelpText = "Space-bar to check/uncheck items";
#endif

    private const string Title = "TASK MONITOR HELP";

    private const int KeyColumnWidth      = 10;
    private const int KeyDescriptionWidth = 41;
    private const int LabelColumnWidth    = 28;
    private const int SwatchWidth         = 4;
    private const int WideSwatchWidth     = 5;

    private readonly AnsiScreenBuffer frame = new();
    private readonly RunContext       runContext;

    private readonly ListView chartView;
    private readonly ListView functionKeysView;
    private readonly ListView keysView;
    private readonly ListView otherKeysView;
    private readonly ListView processView;

    private readonly ListView[] sections;

    public HelpScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        chartView        = NewSection("CHART COLOURS", columns: 4);
        processView      = NewSection("PROCESS AND PATH COLOURS", columns: 5);
        keysView         = NewSection("KEYS", columns: 4);
        functionKeysView = NewSection("FUNCTION KEYS", columns: 2);
        otherKeysView    = NewSection("OTHER KEYS", columns: 2);

        sections = [chartView, processView, keysView, functionKeysView, otherKeysView];

        foreach (ListView section in sections) {
            Controls.Add(section);
        }
    }

    private static ListViewSubItem Cell(string text, Color foreground, Color background) =>
        new(null!, text, background, foreground);

    private static ListViewItem KeyRow(Theme theme, params string[] cells) =>
        new(cells
            .Select((text, i) => Cell(
                text,
                i % 2 == 0 ? theme.RangeLowBackground : theme.ListViewForeground,
                theme.ListViewBackground))
            .ToArray());

    private ListView NewSection(string title, int columns)
    {
        ListView section = new(runContext.Terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            HeaderText        = title,
            ShowBorder        = true,
            ShowCheckboxes    = false,
            ShowColumnHeaders = false
        };

        for (int i = 0; i < columns; i++) {
            section.ColumnHeaders.Add(new ListViewColumnHeader(""));
        }

        return section;
    }

    protected override void OnDraw()
    {
        Theme theme  = runContext.AppConfig.Theme;
        int   offset = Math.Max(0, (Width - Title.Length) / 2);

        frame.Clear();
        frame.MoveTo(X, Y);
        frame.SetColour(theme.MenubarForeground, theme.MenubarBackground);
        frame.Append(' ', offset);
        frame.Append(Title.AsSpan(0, Math.Min(Title.Length, Math.Max(0, Width - offset))));
        frame.Append(' ', Width - offset - Title.Length);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());

        foreach (ListView section in sections) {
            section.Draw();
        }

        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Height - 1,
            10,
            theme,
            enabled: true,
            runContext.Terminal);
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        Theme theme  = runContext.AppConfig.Theme;
        Color listBg = theme.ListViewBackground;
        Color listFg = theme.ListViewForeground;

        BackgroundColour = theme.Background;
        ForegroundColour = theme.Foreground;

        foreach (ListView section in sections) {
            section.BackgroundColour       = listBg;
            section.ForegroundColour       = listFg;
            section.BorderForegroundColour = theme.ListViewBorderForeground;
            section.BorderBackgroundColour = theme.ListViewBorderBackground;
        }

        keysView.ColumnHeaders[0].RightAligned         = true;
        keysView.ColumnHeaders[2].RightAligned         = true;
        functionKeysView.ColumnHeaders[0].RightAligned = true;
        otherKeysView.ColumnHeaders[0].RightAligned    = true;

        chartView.Items.Add(new ListViewItem([
            Cell("Low",  theme.RangeLowBackground,  listBg),
            Cell("Mid",  theme.RangeMidBackground,  listBg),
            Cell("High", theme.RangeHighBackground, listBg),
            Cell("",     listFg,                    listBg)
        ]));

        processView.Items.Add(new ListViewItem([Cell("Normal process",              theme.ColumnCommandNormalUserSpace, listBg)]));
        processView.Items.Add(new ListViewItem([Cell("Low priority (nice) process", theme.ColumnCommandLowPriority,     listBg)]));
        processView.Items.Add(new ListViewItem([Cell("High Cpu usage (> 1 core)",   theme.ColumnCommandHighCpu,         listBg)]));
        processView.Items.Add(new ListViewItem([Cell("I/O bound process",           theme.ColumnCommandIoBound,         listBg)]));

        processView.Items.Add(new ListViewItem([
            Cell("Metric", listFg,                    listBg),
            Cell("Low",    theme.RangeLowForeground,  theme.RangeLowBackground),
            Cell("Mid",    theme.RangeMidForeground,  theme.RangeMidBackground),
            Cell("High",   theme.RangeHighForeground, theme.RangeHighBackground),
            Cell("",       listFg,                    listBg)
        ]));

        processView.Items.Add(new ListViewItem([Cell("Metric changed", theme.DeltaHighlightColour, listBg)]));

        keysView.Items.Add(KeyRow(theme, "←:",        "Move left to next screen component",       "↑:",       "Arrow to scroll list up"));
        keysView.Items.Add(KeyRow(theme, "→:",        "Move right to next screen component",      "↓:",       "Arrow to scroll list down"));
        keysView.Items.Add(KeyRow(theme, "Pg Up:",    "Move up one page in list",                 "↵:",       "ENTER to select item in list"));
        keysView.Items.Add(KeyRow(theme, "Pg Down:",  "Move down one page in list",               "ESC:",     "Close dialog or clear the filter"));
        keysView.Items.Add(KeyRow(theme, "a:",        "Sort list ascending",                      "d:",       "Sort list descending"));
        keysView.Items.Add(KeyRow(theme, "s:",        "Select list sort column",                  "p g m:",   "Sort process list on CPU%, GPU%, Memory"));
        keysView.Items.Add(KeyRow(theme, "f:",        "Filter the list",                          "x:",       "Toggle multiple process list selection"));
        keysView.Items.Add(KeyRow(theme, "u:",        "Uncheck all selected processes in a list", "␣:",       SpaceHelpText));
        keysView.Items.Add(KeyRow(theme, "k:",        "End task (checked or selected)",           "r F5:",    "Refresh the current list"));
        keysView.Items.Add(KeyRow(theme, "z:",        "Freeze process list updates",              "i:",       "Toggle Irix mode CPU reporting in process list"));
        keysView.Items.Add(KeyRow(theme, "t:",        "Show the next tip in the footer",          "",         ""));

        functionKeysView.Items.Add(KeyRow(theme, "F1:",  "Show this help screen."));
        functionKeysView.Items.Add(KeyRow(theme, "F2:",  "Show setup: layouts, metre display, visible columns, colour themes and other settings."));
        functionKeysView.Items.Add(KeyRow(theme, "F3:",  "Switch to the next colour theme, in the order setup lists them. Save it from setup to keep it."));
        functionKeysView.Items.Add(KeyRow(theme, "F4:",  "Switch SUMMARY to the next layout, in the order setup lists them. Save it from setup to keep it."));
        functionKeysView.Items.Add(KeyRow(theme, "F5:",  "Refresh the Startup, Apps, Services or Drivers list (same as r)."));
        functionKeysView.Items.Add(KeyRow(theme, "F10:", "Exit Task Monitor."));
        functionKeysView.Items.Add(KeyRow(theme, "F12:", "Show the about screen."));

        otherKeysView.Items.Add(KeyRow(theme, "k:",
            "End the selected process, or all checked processes in multiple selection (x), and their child processes."));

        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;

        int width  = Math.Max(0, Width - 2);
        int inner  = Math.Max(0, width - 2);
        int keyRow = Y + Height - 1;
        int y      = Y + 1;

        foreach (ListView section in sections) {
            section.X       = X;
            section.Y       = y;
            section.Width   = width;
            section.Height  = section.Items.Count + 2;
            section.Visible = y + section.Height <= keyRow;

            y += section.Height;
        }

        SetColumnWidths(chartView,        SwatchWidth,      SwatchWidth, WideSwatchWidth);
        SetColumnWidths(processView,      LabelColumnWidth, SwatchWidth, SwatchWidth, WideSwatchWidth);
        SetColumnWidths(keysView,         KeyColumnWidth,   KeyDescriptionWidth, KeyColumnWidth);
        SetColumnWidths(functionKeysView, KeyColumnWidth);
        SetColumnWidths(otherKeysView,    KeyColumnWidth);

        void SetColumnWidths(ListView section, params int[] leading)
        {
            for (int i = 0; i < leading.Length; i++) {
                section.ColumnHeaders[i].Width = leading[i];
            }

            section.ColumnHeaders[leading.Length].Width = Math.Max(0, inner - leading.Sum());
        }

        base.OnResize();
    }

    protected override void OnUnload()
    {
        foreach (ListView section in sections) {
            section.Items.Clear();
        }

        base.OnUnload();
        Terminal.CursorVisible = true;
    }
}
