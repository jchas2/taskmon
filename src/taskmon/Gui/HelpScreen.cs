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
        Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.MenubarBackground;
        Terminal.ForegroundColor = runContext.AppConfig.DefaultTheme.MenubarForeground;

        string menubar = "TASK MONITOR HELP";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar.ToBold());
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);
        Terminal.WriteLine(colourHelpText.ToString());
        
        helpView.Draw();
        
        Terminal.SetCursorPosition(0, helpView.Y + helpView.Height);
        Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.Background;
        Terminal.ForegroundColor = runContext.AppConfig.DefaultTheme.Foreground;
        Terminal.WriteEmptyLine();
        Terminal.WriteLine(functionHelpText.ToString());
        
        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Height - 1,
            10,
            runContext.AppConfig.DefaultTheme,
            enabled: true,
            runContext.Terminal);
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;
        
        Color bg = runContext.AppConfig.DefaultTheme.Background;
        Color fg = runContext.AppConfig.DefaultTheme.Foreground;
        Color keyColour = runContext.AppConfig.DefaultTheme.RangeLowBackground;
        Theme theme = runContext.AppConfig.DefaultTheme;

        BackgroundColour = bg;
        ForegroundColour = fg;

        helpView.BackgroundColour = bg;
        helpView.ForegroundColour = fg;

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
        helpView.Items.Add(new ListViewItem(new[] { "Pg Down:", "Move down one page in list",         "ESC:", "Exit current screen or dialog" }));
        helpView.Items.Add(new ListViewItem(new[] { "a:", "Sort processes ascending",                 "d:", "Sort processes descending" }));
        helpView.Items.Add(new ListViewItem(new[] { "F3:", "Select sort column",                      "p g m:", "Sort processes on CPU%, GPU%, Memory" }));
        helpView.Items.Add(new ListViewItem(new[] { "F4:", "Filter by Process, User, Pid, Path",      "x:", "Toggle multiple process selection" }));
        helpView.Items.Add(new ListViewItem(new[] { "u:", "Uncheck all selected processes",           "\u2423:", "Space-bar to check/uncheck items" }));
        helpView.Items.Add(new ListViewItem(new[] { "F6:", "Kill process/selected processes",         "F5:", "Show process info" }));
        helpView.Items.Add(new ListViewItem(new[] { "f z:", "Freeze process updates",                 "i:", "Toggle Irix mode CPU reporting" }));
        
        for (int i = 0; i < helpView.Items.Count; i++) {
            helpView.Items[i].SubItems[0].ForegroundColor = keyColour;
            helpView.Items[i].SubItems[0].BackgroundColor = bg;
            helpView.Items[i].SubItems[2].ForegroundColor = keyColour;
            helpView.Items[i].SubItems[2].BackgroundColor = bg;
        }

        functionHelpText.AppendLine(
            @$"Function Keys:
{"F2:".ToColour(theme.RangeLowBackground, bg)}{"  Show setup screen, where you can configure metre display options, choose visible columns, select colour themes and other settings.".ToColour(fg, bg)}
{"F3:".ToColour(theme.RangeLowBackground, bg)}{"  Prompt to sort process list by one of the visible columns. Press ENTER to accept selection or ESC to abandon.".ToColour(fg, bg)}
{"F4:".ToColour(theme.RangeLowBackground, bg)}{"  Filter the current process list: Enter a partial name and processes with partial matching names, paths, PIDs or user names will show. To cancel filtering, enter F4 again and clear or ESC. If --pid, --username or --process used on start, filter is applied to existing filters.".ToColour(fg, bg)}
{"F5:".ToColour(theme.RangeLowBackground, bg)}{"  Show detailed process info, including threads, cpu time, loaded modules and handles (OS specific).".ToColour(fg, bg)}
{"F6:".ToColour(theme.RangeLowBackground, bg)}{"  Terminate the selected task in the process list. If checkboxes are enabled in F2 Setup, all checked processes will be terminated.".ToColour(fg, bg)}
{"F7:".ToColour(theme.RangeLowBackground, bg)}{"  Cycle between themes.".ToColour(fg, bg)}
{"F8:".ToColour(theme.RangeLowBackground, bg)}{"  Cycle between layouts.".ToColour(fg, bg)}
{"F9:".ToColour(theme.RangeLowBackground, bg)}{"  Show information about this app and the system.".ToColour(fg, bg)}");

        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.Background;

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
