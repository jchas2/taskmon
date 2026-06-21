using System.CodeDom;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public class HelpScreen : Screen
{
    private readonly RunContext runContext;
    private StringBuilder helpText = new();

    public HelpScreen(RunContext runContext) : base(runContext.Terminal) => this.runContext = runContext;

    protected override void OnDraw()
    {
        DrawRectangle(
            X,
            Y,
            Width,
            Height,
            runContext.AppConfig.DefaultTheme.Background);

        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.MenubarBackground;
        Terminal.ForegroundColor = runContext.AppConfig.DefaultTheme.MenubarForeground;

        string menubar = "TASK MONITOR HELP";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar.ToBold());
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);
        
        Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.Background;
        Terminal.ForegroundColor = runContext.AppConfig.DefaultTheme.Foreground;
        Terminal.WriteEmptyLine();
        Terminal.WriteLine(helpText.ToString());
        
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
        
        ConsoleColor fg = runContext.AppConfig.DefaultTheme.Foreground;
        ConsoleColor bg = runContext.AppConfig.DefaultTheme.Background;
        Theme theme = runContext.AppConfig.DefaultTheme;
        
        helpText.Clear();
        helpText.AppendLine();

        helpText.AppendLine("Metre Colours:".ToColour(fg, bg));
        helpText.Append("Low ".ToColour(theme.RangeLowBackground, bg));
        helpText.Append("Mid ".ToColour(theme.RangeMidBackground, bg));
        helpText.AppendLine("High".ToColour(theme.RangeHighBackground, bg));
        helpText.AppendLine();
        
        helpText.AppendLine("Process and Path Colours:".ToColour(fg, bg));
        helpText.AppendLine("Normal process".ToColour(theme.ColumnCommandNormalUserSpace, bg));
        helpText.AppendLine("Low priority (nice) process".ToColour(theme.ColumnCommandLowPriority, bg));
        helpText.AppendLine("High Cpu usage (> 1 core)".ToColour(theme.ForegroundHighlight, theme.RangeHighBackground));
        helpText.AppendLine("I/O bound process".ToColour(theme.ColumnCommandIoBound, bg));
        helpText.Append("Metric ".ToColour(fg, bg));
        helpText.Append("Low ".ToColour(theme.ForegroundHighlight, theme.RangeLowBackground));
        helpText.Append("Mid ".ToColour(theme.ForegroundHighlight, theme.RangeMidBackground));
        helpText.AppendLine("High".ToColour(theme.ForegroundHighlight, theme.RangeHighBackground));
        helpText.AppendLine("Metric changed".ToColour(theme.RangeMidForeground, bg));
        helpText.AppendLine();
        helpText.AppendLine("Screen Navigation".ToColour(fg, bg));
        helpText.AppendLine("\u2190    Move left to next screen component".ToColour(fg, bg));
        helpText.AppendLine("\u2192    Move right to next screen component".ToColour(fg, bg));
        helpText.AppendLine("\u21B5    Enter to select screen or dialog component".ToColour(fg, bg));
        helpText.AppendLine("ESC  Exit current screen or dialog".ToColour(fg, bg));
        helpText.AppendLine();
        helpText.AppendLine("List Navigation".ToColour(fg, bg));
        helpText.AppendLine("\u2191    Arrow to scroll up".ToColour(fg, bg));
        helpText.AppendLine("\u2193    Arrow to scroll down".ToColour(fg, bg));
        helpText.AppendLine("\u21B5    Enter to select item in list".ToColour(fg, bg));
        helpText.AppendLine("\u2423    Space-bar to check/uncheck item in list (Checkboxes must be enabled in F2 Setup)".ToColour(fg, bg));
        helpText.AppendLine();

        helpText.AppendLine("A    Sort column ascending".ToColour(fg, bg));
        helpText.AppendLine("D    Sort column descending".ToColour(fg, bg));
        helpText.AppendLine();
        
        helpText.AppendLine(
@"Function Keys
  F1   Show this help screen.
  F2   Show setup screen, where you can configure metre display options, choose visible columns, select colour themes and other settings.  
  F3   Prompt to sort process list by one of the visible columns. Press ENTER to accept selection or ESC to abandon.
  F4   Filter the current process list: Enter a partial name and processes with partial matching names, paths, PIDs or user names will show. To cancel filtering, enter F4 again and clear or ESC. If --pid, --username or --process used on start, filter is applied to existing filters.
  F5   Show detailed process info, including threads, cpu time, loaded modules and handles (OS specific).
  F6   Terminate the selected task in the process list. If checkboxes are enabled in F2 Setup, all checked processes will be terminated.
  F7   Cycle between themes.
  F8   Cycle between layouts.
  F9   Show information about this app and the system.
  F10  Exit App.".ToColour(fg, bg));
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        Terminal.CursorVisible = true;
    }
}
