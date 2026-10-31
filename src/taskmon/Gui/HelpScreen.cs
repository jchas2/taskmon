using System.Drawing;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public class HelpScreen : Screen
{
    private readonly RunContext runContext;
    private readonly ListView helpView;
    private StringBuilder colourHelpText = new();
    private StringBuilder functionHelpText = new();

    public HelpScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;
        
        helpView = new(runContext.Terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
        };

        helpView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
        
        Controls.Add(helpView);
    } 

    protected override void OnDraw()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.MenubarBackground;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.MenubarForeground;

        string menubar = "TASK MONITOR HELP";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar);
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);
        Terminal.WriteLine(colourHelpText.ToString());
        
        helpView.Draw();
        
        Terminal.SetCursorPosition(0, helpView.Y + helpView.Height);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.Foreground;
        Terminal.WriteEmptyLine();
        Terminal.WriteLine(functionHelpText.ToString());
        
        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Height - 1,
            10,
            runContext.AppConfig.Theme,
            enabled: true,
            runContext.Terminal);
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;
        
        Color bg = runContext.AppConfig.Theme.Background;
        Color fg = runContext.AppConfig.Theme.Foreground;
        Color listBg = runContext.AppConfig.Theme.ListViewBackground;
        Color listFg = runContext.AppConfig.Theme.ListViewForeground;
        Color keyColour = runContext.AppConfig.Theme.RangeLowBackground;
        Theme theme = runContext.AppConfig.Theme;

        BackgroundColour = bg;
        ForegroundColour = fg;

        helpView.BackgroundColour = listBg;
        helpView.ForegroundColour = listFg;
        helpView.BorderForegroundColour = runContext.AppConfig.Theme.ListViewBorderForeground;
        helpView.BorderBackgroundColour = runContext.AppConfig.Theme.ListViewBorderBackground;

        helpView.ColumnHeaders[0].RightAligned = true;
        helpView.ColumnHeaders[1].RightAligned = false;
        helpView.ColumnHeaders[2].RightAligned = true;
        helpView.ColumnHeaders[3].RightAligned = false;

        colourHelpText.Append("Chart Colours: ".ToColour(fg, bg));
        colourHelpText.Append("Low ".ToColour(theme.RangeLowBackground, bg));
        colourHelpText.Append("Mid ".ToColour(theme.RangeMidBackground, bg));
        colourHelpText.AppendLine("High".ToColour(theme.RangeHighBackground, bg));
        colourHelpText.AppendLine();
        colourHelpText.AppendLine("Process and Path Colours:".ToColour(fg, bg));
        colourHelpText.AppendLine("Normal process".ToColour(theme.ColumnCommandNormalUserSpace, bg));
        colourHelpText.AppendLine("Low priority (nice) process".ToColour(theme.ColumnCommandLowPriority, bg));
        colourHelpText.AppendLine("High Cpu usage (> 1 core)".ToColour(theme.ColumnCommandHighCpu, bg));
        colourHelpText.AppendLine("I/O bound process".ToColour(theme.ColumnCommandIoBound, bg));
        colourHelpText.Append("Metric ".ToColour(fg, bg));
        colourHelpText.Append("Low ".ToColour(theme.RangeLowForeground, theme.RangeLowBackground));
        colourHelpText.Append("Mid ".ToColour(theme.RangeMidForeground, theme.RangeMidBackground));
        colourHelpText.AppendLine("High".ToColour(theme.RangeHighForeground, theme.RangeHighBackground));
        colourHelpText.AppendLine("Metric changed".ToColour(theme.DeltaHighlightColour, bg));
        
        helpView.Items.Add(new ListViewItem(new[] { "", "", "", "" }));
        helpView.Items.Add(new ListViewItem(new[] { "\u2190:", "Move left to next screen component",  "\u2191:", "Arrow to scroll up" }));
        helpView.Items.Add(new ListViewItem(new[] { "\u2192:", "Move right to next screen component", "\u2193:", "Arrow to scroll down" }));
        helpView.Items.Add(new ListViewItem(new[] { "Pg Up:", "Move up one page in list",             "\u21B5:", "ENTER to select item" }));
        helpView.Items.Add(new ListViewItem(new[] { "Pg Down:", "Move down one page in list",         "ESC:", "Close dialog or clear the filter" }));
        helpView.Items.Add(new ListViewItem(new[] { "a:", "Sort ascending",                           "d:", "Sort descending" }));
        helpView.Items.Add(new ListViewItem(new[] { "s:", "Select sort column",                       "p g m:", "Sort processes on CPU%, GPU%, Memory" }));
        helpView.Items.Add(new ListViewItem(new[] { "f:", "Filter the list in the current view",      "x:", "Toggle multiple process selection" }));
        helpView.Items.Add(new ListViewItem(new[] { "u:", "Uncheck all selected processes",           "\u2423:", "Space-bar to check/uncheck items" }));
        helpView.Items.Add(new ListViewItem(new[] { "k:", "End task (checked or selected)",           "r F5:", "Refresh the current list" }));
        helpView.Items.Add(new ListViewItem(new[] { "z:", "Freeze process updates",                   "i:", "Toggle Irix mode CPU reporting" }));
        helpView.Items.Add(new ListViewItem(new[] { "t:", "Show the next tip in the footer",          "", "" }));
        
        for (int i = 0; i < helpView.Items.Count; i++) {
            helpView.Items[i].SubItems[0].ForegroundColor = keyColour;
            helpView.Items[i].SubItems[0].BackgroundColor = listBg;
            helpView.Items[i].SubItems[2].ForegroundColor = keyColour;
            helpView.Items[i].SubItems[2].BackgroundColor = listBg;
        }

        functionHelpText.AppendLine(
            @$"Function Keys:
{"F1:".ToColour(theme.RangeLowBackground, bg)}{"  Show this help screen.".ToColour(fg, bg)}
{"F2:".ToColour(theme.RangeLowBackground, bg)}{"  Show setup screen, where you can configure metre display options, choose visible columns, select colour themes and other settings.".ToColour(fg, bg)}
{"F5:".ToColour(theme.RangeLowBackground, bg)}{"  Refresh the Startup, Apps, Services or Drivers list (same as r).".ToColour(fg, bg)}
{"F10:".ToColour(theme.RangeLowBackground, bg)}{" Exit Task Monitor.".ToColour(fg, bg)}

End Task:
{"k:".ToColour(theme.RangeLowBackground, bg)}{"   End the selected process, or every checked process when multiple selection (x) is on. Child processes are also ended.".ToColour(fg, bg)}");

        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;

        helpView.Y = 10; // Room for colourHelpText.
        helpView.X = 0;
        helpView.Width = runContext.Terminal.WindowWidth - helpView.X - 2;
        helpView.Height = helpView.Items.Count + 1;

        helpView.ColumnHeaders[0].Width = 10;
        helpView.ColumnHeaders[1].Width = 40;
        helpView.ColumnHeaders[2].Width = 10;
        helpView.ColumnHeaders[3].Width = 40;
        
        base.OnResize();
    }

    protected override void OnUnload()
    {
        helpView.Items.Clear();
        colourHelpText.Clear();
        functionHelpText.Clear();
        
        base.OnUnload();
        Terminal.CursorVisible = true;
    }
}
