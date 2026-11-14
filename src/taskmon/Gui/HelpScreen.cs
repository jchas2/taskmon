using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public class HelpScreen : Screen
{
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

        chartView        = NewSection(Strings.HelpChartColoursSection, columns: 4);
        processView      = NewSection(Strings.HelpProcessColoursSection, columns: 5);
        keysView         = NewSection(Strings.HelpKeysSection, columns: 4);
        functionKeysView = NewSection(Strings.HelpFunctionKeysSection, columns: 2);
        otherKeysView    = NewSection(Strings.HelpOtherKeysSection, columns: 2);

        sections = [chartView, processView, keysView, functionKeysView, otherKeysView];

        foreach (ListView section in sections) {
            Controls.Add(section);
        }
    }

    private static ListViewSubItem Cell(string text, Color foreground, Color background) =>
        new(null!, text, background, foreground);

    private static int FitWidth(int minimum, params string[] texts) =>
        Math.Max(minimum, texts.Max(text => text.Length) + 1);

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
            Strings.KeyBindExit,
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
            Cell(Strings.HelpRangeLow,  theme.RangeLowBackground,  listBg),
            Cell(Strings.HelpRangeMid,  theme.RangeMidBackground,  listBg),
            Cell(Strings.HelpRangeHigh, theme.RangeHighBackground, listBg),
            Cell("",     listFg,                    listBg)
        ]));

        processView.Items.Add(new ListViewItem([Cell(Strings.HelpNormalProcess,      theme.ColumnCommandNormalUserSpace, listBg)]));
        processView.Items.Add(new ListViewItem([Cell(Strings.HelpLowPriorityProcess, theme.ColumnCommandLowPriority,     listBg)]));
        processView.Items.Add(new ListViewItem([Cell(Strings.HelpHighCpuProcess,     theme.ColumnCommandHighCpu,         listBg)]));
        processView.Items.Add(new ListViewItem([Cell(Strings.HelpIoBoundProcess,     theme.ColumnCommandIoBound,         listBg)]));

        processView.Items.Add(new ListViewItem([
            Cell(Strings.HelpMetric, listFg,                    listBg),
            Cell(Strings.HelpRangeLow,    theme.RangeLowForeground,  theme.RangeLowBackground),
            Cell(Strings.HelpRangeMid,    theme.RangeMidForeground,  theme.RangeMidBackground),
            Cell(Strings.HelpRangeHigh,   theme.RangeHighForeground, theme.RangeHighBackground),
            Cell("",       listFg,                    listBg)
        ]));

        processView.Items.Add(new ListViewItem([Cell(Strings.HelpMetricChanged, theme.DeltaHighlightColour, listBg)]));

        keysView.Items.Add(KeyRow(theme, "←:",        Strings.HelpKeyMoveLeft,   "↑:",       Strings.HelpKeyScrollUp));
        keysView.Items.Add(KeyRow(theme, "→:",        Strings.HelpKeyMoveRight,  "↓:",       Strings.HelpKeyScrollDown));
        keysView.Items.Add(KeyRow(theme, "Pg Up:",    Strings.HelpKeyPageUp,     "↵:",       Strings.HelpKeyEnter));
        keysView.Items.Add(KeyRow(theme, "Pg Down:",  Strings.HelpKeyPageDown,   "ESC:",     Strings.HelpKeyEscape));
        keysView.Items.Add(KeyRow(theme, "a:",        Strings.HelpKeySortAscending, "d:",    Strings.HelpKeySortDescending));
        keysView.Items.Add(KeyRow(theme, "s:",        Strings.HelpKeySortColumn, "p g m:",   Strings.HelpKeySortProcesses));
        keysView.Items.Add(KeyRow(theme, "f:",        Strings.HelpKeyFilter,     "x:",       Strings.HelpKeyMultiSelect));
        keysView.Items.Add(KeyRow(theme, "u:",        Strings.HelpKeyUncheckAll, "␣:",       SpaceHelpText));
        keysView.Items.Add(KeyRow(theme, "k:",        Strings.HelpKeyEndTask,    "r F5:",    Strings.HelpKeyRefresh));
        keysView.Items.Add(KeyRow(theme, "z:",        Strings.HelpKeyFreeze,     "i:",       Strings.HelpKeyIrix));
        keysView.Items.Add(KeyRow(theme, "t:",        Strings.HelpKeyNextTip,    "",         ""));

        functionKeysView.Items.Add(KeyRow(theme, "F1:",  Strings.HelpF1Description));
        functionKeysView.Items.Add(KeyRow(theme, "F2:",  Strings.HelpF2Description));
        functionKeysView.Items.Add(KeyRow(theme, "F3:",  Strings.HelpF3Description));
        functionKeysView.Items.Add(KeyRow(theme, "F4:",  Strings.HelpF4Description));
        functionKeysView.Items.Add(KeyRow(theme, "F5:",  Strings.HelpF5Description));
        functionKeysView.Items.Add(KeyRow(theme, "F10:", Strings.HelpF10Description));
        functionKeysView.Items.Add(KeyRow(theme, "F12:", Strings.HelpF12Description));

        otherKeysView.Items.Add(KeyRow(theme, "k:", Strings.HelpOtherKeyEndTask));

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

        // The widths are minimums, widened to fit the labels in the UI language.
        int lowWidth   = FitWidth(SwatchWidth,     Strings.HelpRangeLow);
        int midWidth   = FitWidth(SwatchWidth,     Strings.HelpRangeMid);
        int highWidth  = FitWidth(WideSwatchWidth, Strings.HelpRangeHigh);
        int labelWidth = FitWidth(
            LabelColumnWidth,
            Strings.HelpNormalProcess,
            Strings.HelpLowPriorityProcess,
            Strings.HelpHighCpuProcess,
            Strings.HelpIoBoundProcess,
            Strings.HelpMetric,
            Strings.HelpMetricChanged);
        int descriptionWidth = FitWidth(
            KeyDescriptionWidth,
            Strings.HelpKeyMoveLeft,
            Strings.HelpKeyMoveRight,
            Strings.HelpKeyPageUp,
            Strings.HelpKeyPageDown,
            Strings.HelpKeySortAscending,
            Strings.HelpKeySortColumn,
            Strings.HelpKeyFilter,
            Strings.HelpKeyUncheckAll,
            Strings.HelpKeyEndTask,
            Strings.HelpKeyFreeze,
            Strings.HelpKeyNextTip);

        SetColumnWidths(chartView,        lowWidth,       midWidth,         highWidth                );
        SetColumnWidths(processView,      labelWidth,     lowWidth,         midWidth,       highWidth);
        SetColumnWidths(keysView,         KeyColumnWidth, descriptionWidth, KeyColumnWidth           );
        SetColumnWidths(functionKeysView, KeyColumnWidth                                             );
        SetColumnWidths(otherKeysView,    KeyColumnWidth                                             );

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

#if __WIN32__
    private static string SpaceHelpText => Strings.HelpKeySpaceWindows;
#elif __APPLE__
    private static string SpaceHelpText => Strings.HelpKeySpaceMacOS;
#else
    private static string SpaceHelpText => Strings.HelpKeySpace;
#endif

    private static string Title => Strings.HelpTitle;
}
