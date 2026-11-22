using System.Drawing;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed class EmptyPaneControl : Control
{
    private const char   BarGlyph     = '⣿';
    private const char   Ellipsis     = '…';
    private const int    MinBoxHeight = 3;
    private const int    MinBoxWidth  = 4;
    private const char   ShadeGlyph   = '░';

    private const string ChartHint = "⣀⣀⣠⣤⣴⣶⣾⣿⣷⣶⣦⣤⣄⣀";
    private const string ListHint  = "────────── ────── ──────────────";

    private const int ListHintRows = 3;

    private static readonly double[] BarFills = [0.45, 0.7, 0.3, 0.85, 0.55];

    public EmptyPaneControl(ISystemTerminal terminal) : base(terminal) { }

    public string? Detail { get; init; }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text
      : width <= 1           ? text[..Math.Max(0, width)]
                             : text[..(width - 1)] + Ellipsis;

    public int? FixedHeight { get; init; }

    public PaneHint Hint { get; init; }

    public Color HintColour { get; set; }

    public string Label { get; init; } = Strings.SummaryEmptyPane;

    protected override void OnDraw()
    {
        if (Width <= 0 || Height <= 0) {
            return;
        }

        Terminal.BackgroundColor = BackgroundColour;

        if (Width < MinBoxWidth || Height < MinBoxHeight) {
            Terminal.ForegroundColor = DisplayBorderColour;

            for (int y = Y; y < Y + Height; y++) {
                Terminal.SetCursorPosition(X, y);
                Terminal.Write(ShadeGlyph, Width);
            }

            return;
        }

        int innerWidth = Width - 2;
        int bottomY    = Y + Height - 1;
        int labelY     = Y + Height / 2;
        int detailY    = Detail is null ? labelY : labelY + 1;

        for (int y = Y; y <= bottomY; y++) {
            Terminal.SetCursorPosition(X, y);
            Terminal.ForegroundColor = DisplayBorderColour;

            if (y == Y || y == bottomY) {
                Terminal.Write(y == Y ? '╭' : '╰');
                Terminal.Write('─', innerWidth);
                Terminal.Write(y == Y ? '╮' : '╯');
                continue;
            }

            Terminal.Write('│');

            if (y == labelY && Hint == PaneHint.Metre) {
                string label = Fit(Label, Math.Max(0, innerWidth - 2));
                int    room  = innerWidth - label.Length - 2;

                Terminal.Write(' ');
                Terminal.ForegroundColor = ForegroundColour;
                Terminal.Write(label);
                Terminal.Write(' ');
                WriteBar(room, BarFills[0]);
            }
            else if (y == labelY) {
                WriteCentred(Fit(Label, innerWidth), innerWidth, ForegroundColour);
            }
            else if (y == detailY) {
                WriteCentred(Fit(Detail!, innerWidth), innerWidth, ForegroundColour);
            }
            else if (Hint == PaneHint.Chart && y == bottomY - 1) {
                Terminal.ForegroundColor = HintColour;

                for (int x = 0; x < innerWidth; x++) {
                    Terminal.Write(ChartHint[x % ChartHint.Length]);
                }
            }
            else if (Hint == PaneHint.Metre) {
                Terminal.Write(' ');
                WriteBar(innerWidth - 2, BarFills[(y - Y) % BarFills.Length]);
                Terminal.Write(' ');
            }
            else if (Hint == PaneHint.List && y > detailY && y <= detailY + ListHintRows) {
                WriteCentred(ListHint[..Math.Min(ListHint.Length, innerWidth - 2)], innerWidth, HintColour);
            }
            else {
                Terminal.Write(' ', innerWidth);
            }

            Terminal.ForegroundColor = DisplayBorderColour;
            Terminal.Write('│');
        }
    }

    private void WriteBar(int width, double fill)
    {
        if (width <= 0) {
            return;
        }

        int filled = (int)Math.Round(width * fill);

        Terminal.ForegroundColor = HintColour;
        Terminal.Write(BarGlyph, filled);
        Terminal.Write(' ', width - filled);
    }

    private void WriteCentred(string text, int width, Color colour)
    {
        int left = (width - text.Length) / 2;

        Terminal.Write(' ', left);
        Terminal.ForegroundColor = colour;
        Terminal.Write(text);
        Terminal.Write(' ', width - left - text.Length);
    }
}
