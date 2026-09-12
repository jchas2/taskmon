using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class PerformancePanelControl : Control
{
    private readonly Chart chart;
    private readonly Control associatedControl;
    private readonly AppConfig appConfig;

    private AnsiScreenBuffer Frame { get; } = new();

    public PerformancePanelControl(
        ISystemTerminal terminal,
        Chart chart,
        Control associatedControl,
        string key,
        string title,
        AppConfig appConfig) : base(terminal)
    {
        this.chart = chart;
        this.associatedControl = associatedControl;
        this.appConfig = appConfig;

        Key = key;
        Title = title;
    }

    // Stable identity for the panel list, e.g. "cpu", "gpu", "gpu:0x1234", "disk:0", "net:42".
    // Drives the rebuild diff and the "keep the selection" logic when the device set changes.
    public string Key { get; }

    public string Title { get; set; }

    public Control AssociatedControl => associatedControl;

    public Chart Chart => chart;

    public string Line1 { get; set; } = string.Empty;

    public string Line2 { get; set; } = string.Empty;

    public string Line3 { get; set; } = string.Empty;

    public string Line4 { get; set; } = string.Empty;

    public bool IsSelected { get; set; } = false;

    // Applies this panel's slice of the snapshot to Line1/Line2 and feeds the mini-chart. Null
    // means the subsystem was absent from the snapshot.
    public Action<SystemSnapshot>? Bind { get; set; }

    public void Update(SystemSnapshot snapshot) => Bind?.Invoke(snapshot);

    protected override void OnDraw()
    {
        int textWidth = Width - chart.Width;

        if (IsSelected) {
            DrawRectangleWithBevel(
                X,
                Y,
                textWidth,
                Height,
                appConfig.DefaultTheme.BackgroundHighlight);
        }
        else {
            DrawRectangle(
                X,
                Y,
                textWidth,
                Height,
                BackgroundColour);
        }

        Color fgColour = IsSelected
            ? appConfig.DefaultTheme.ForegroundHighlight
            : ForegroundColour;

        Color bgColour  = IsSelected
            ? appConfig.DefaultTheme.BackgroundHighlight
            : BackgroundColour;

        Color fgMenuColour = appConfig.DefaultTheme.MenubarForeground;
        Color bgMenuColour = appConfig.DefaultTheme.MenubarBackground;

        if (!IsSelected) {
            DrawHorizontalLine(
                Y,
                X,
                X + Width - chart.Width,
                fgColour);
        }

        Frame.Clear();

        if (!IsSelected) {
            Frame.MoveTo(X, Y + 1);
            Frame.SetColour(fgMenuColour, bgMenuColour);
            Frame.Append('▌'); // ▌
            Frame.Append(Title.PadOrTruncate(' ', textWidth - 2));
            Frame.MoveTo(X + textWidth + 2, Y + 1);
            Frame.SetColour(fgMenuColour, bgMenuColour);
            Frame.Append('▐'); // ▐
        }
        else {
            // The bevel rectangle spans textWidth columns; its interior, between the ▐ and ▌
            // edges, is textWidth - 2 wide. Writing from X + 1, the title must not exceed that or
            // it paints over the right edge.
            Frame.MoveTo(X + 1, Y + 1);
            Frame.SetColour(fgColour, bgColour);
            Frame.Append(Title.PadOrTruncate(' ', textWidth - 2));
        }

        ReadOnlySpan<string> lines = [Line1, Line2, Line3, Line4];
        Frame.SetColour(fgColour, bgColour);

        for (int i = 0; i < lines.Length; i++) {
            Frame.MoveTo(X + 1 , Y + i + 2);
            Frame.Append(lines[i]);
        }
        
        Terminal.Write(Frame.AsSpan());

        if (!IsSelected) {
            DrawHorizontalLine(
                Y + 6,
                X,
                X + Width - chart.Width,
                fgColour);
        }
    }

    protected override void OnResize()
    {
        chart.Width = 14;
        chart.Height = 7;
        chart.X = X + Width - chart.Width;
        chart.Y = Y;
        chart.Resize();
    }
}
