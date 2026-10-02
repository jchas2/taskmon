using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.Gui.Controls.DiskSpace;

public sealed class DiskSpaceHeatMapControl : Control
{
    private const int FadeSteps = 4;
    private const int MinCellWidthForLabel = 6;

    private const string BorderHint = "s Start scanning   c Cancel";

    private readonly AppConfig appConfig;
    private readonly AnsiScreenBuffer frame = new();
    private readonly StableTreemapLayout stableLayout = new();
    private readonly Dictionary<string, int> fadeStepByCellId = new();
    private readonly Dictionary<string, Color> heatColourById = new();

    private DiskSpaceSpecs? specs;

    public DiskSpaceHeatMapControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal) =>
        this.appConfig = appConfig;

    public void Sample(DiskSpaceSpecs newSpecs) => specs = newSpecs;

    protected override void OnDraw()
    {
        int treemapTop = Y;
        int treemapHeight = Height;

        if (treemapHeight <= 0 || Width <= 0) {
            return;
        }

        DrawTreemapBorder(treemapTop, treemapHeight);

        int innerLeft = X + 1;
        int innerTop = treemapTop + 1;
        int innerWidth = Math.Max(0, Width - 2);
        int innerHeight = Math.Max(0, treemapHeight - 2);

        if (innerWidth <= 0 || innerHeight <= 0) {
            return;
        }

        DrawHeader(innerLeft, innerTop, innerWidth);

        int cellsTop = innerTop + 1;
        int cellsHeight = Math.Max(0, innerHeight - 1);

        if (cellsHeight <= 0) {
            return;
        }

        if (specs?.RootNode is not { Children.Count: > 0 } root) {
            DrawPlaceholder(
                innerLeft, 
                cellsTop, 
                innerWidth, 
                cellsHeight);
            
            return;
        }

        TreemapItem[] items = [.. root.Children
            .Where(child => child.TotalBytes > 0)
            .Select(child => new TreemapItem {
                Id = child.Path, 
                Weight = child.TotalBytes
            })];

        if (items.Length == 0) {
            DrawPlaceholder(
                innerLeft, 
                cellsTop, 
                innerWidth, 
                cellsHeight);
            
            return;
        }

        Rectangle bounds = new(
            innerLeft, 
            cellsTop, 
            innerWidth, 
            cellsHeight);
        
        IReadOnlyList<TreemapCell> cells = stableLayout.Layout(items, bounds);
        Dictionary<string, DiskSpaceFolderNode> nodesById = root.Children.ToDictionary(child => child.Path);

        PruneFadeState(cells);

        Dictionary<string, int> rankById = RankByArea(cells);
        heatColourById.Clear();

        foreach (TreemapCell cell in cells) {
            DrawCell(cell, nodesById[cell.Id], rankById[cell.Id], cells.Count);
        }
    }

    internal static Dictionary<string, int> RankByArea(IReadOnlyList<TreemapCell> cells)
    {
        Dictionary<string, int> rankById = new();
        int rank = 0;

        foreach (TreemapCell cell in cells.OrderByDescending(cell => (long)cell.Bounds.Width * cell.Bounds.Height)) {
            rankById[cell.Id] = rank++;
        }

        return rankById;
    }

    internal IReadOnlyDictionary<string, Color> HeatColoursForTests => heatColourById;

    private void DrawHeader(int left, int top, int width)
    {
        Color stateColour = specs?.State switch {
            DiskSpaceScanState.Scanning => appConfig.Theme.HeatmapStateScanning,
            DiskSpaceScanState.Completed => appConfig.Theme.HeatmapStateCompleted,
            DiskSpaceScanState.Faulted => appConfig.Theme.HeatmapStateFaulted,
            _ => ForegroundColour
        };

        string text = specs is null or { State: DiskSpaceScanState.Idle }
            ? "DISK SPACE"
            : $"DISK SPACE  {specs.RootPath}  {DescribeState(specs)}";

        WriteLine(
            left, 
            top, 
            width, 
            text, 
            stateColour);
    }

    private static string DescribeState(DiskSpaceSpecs specs) => specs.State switch {
        DiskSpaceScanState.Scanning =>
            $"Scanning… {specs.FilesScanned:N0} files / {specs.TotalBytesScanned.ToFormattedByteSize()}",
        DiskSpaceScanState.Cancelling => "Cancelling…",
        DiskSpaceScanState.Completed =>
            $"Completed - {specs.TotalBytesScanned.ToFormattedByteSize()} in {specs.ElapsedMilliseconds / 1000.0:0.0}s",
        DiskSpaceScanState.Cancelled =>
            $"Cancelled - {specs.TotalBytesScanned.ToFormattedByteSize()} scanned",
        DiskSpaceScanState.Faulted => $"Failed: {specs.ErrorMessage}",
        _ => string.Empty
    };

    private void DrawTreemapBorder(int top, int height)
    {
        if (Width < 2 || height < 2) {
            return;
        }

        int innerWidth = Width - 2;
        Color borderForeground = appConfig.Theme.ListViewBorderForeground;
        Color borderBackground = appConfig.Theme.ListViewBorderBackground;

        frame.Clear();
        frame.MoveTo(X, top);
        frame.SetColour(borderForeground, borderBackground);
        frame.Append('╭');
        frame.Append('─', innerWidth);
        frame.Append('╮');

        for (int row = 1; row < height - 1; row++) {
            frame.MoveTo(X, top + row);
            frame.SetColour(borderForeground, borderBackground);
            frame.Append('│');

            frame.MoveTo(X + Width - 1, top + row);
            frame.Append('│');
        }

        string footerLabel = $" {BorderHint} ";
        int footerLabelLen = Math.Min(footerLabel.Length, innerWidth);
        int footerLeftDashes = (innerWidth - footerLabelLen) / 2;
        int footerRightDashes = innerWidth - footerLabelLen - footerLeftDashes;

        frame.MoveTo(X, top + height - 1);
        frame.SetColour(borderForeground, borderBackground);
        frame.Append('╰');
        frame.Append('─', footerLeftDashes);
        frame.SetColour(ForegroundColour, borderBackground);
        frame.Append(footerLabelLen < footerLabel.Length ? footerLabel[..footerLabelLen] : footerLabel);
        frame.SetColour(borderForeground, borderBackground);
        frame.Append('─', footerRightDashes);
        frame.Append('╯');

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private void DrawPlaceholder(
        int left, 
        int top, 
        int width, 
        int height)
    {
        DrawRectangle(
            left, 
            top, 
            width, 
            height, 
            BackgroundColour);

        if (specs?.State != DiskSpaceScanState.Faulted) {
            return;
        }

        string message = specs.ErrorMessage ?? "Scan failed.";
        
        WriteLine(
            left, 
            top, 
            width, 
            message, 
            ForegroundColour);
    }

    private void DrawCell(
        TreemapCell cell, 
        DiskSpaceFolderNode node, 
        int rank, 
        int cellCount)
    {
        Color heatColour = RankColour(rank, cellCount);
        heatColourById[cell.Id] = heatColour;
        Color fillColour = ApplyFade(cell.Id, heatColour);

        DrawRectangle(
            cell.Bounds.X, 
            cell.Bounds.Y, 
            cell.Bounds.Width, 
            cell.Bounds.Height, 
            fillColour);

        if (cell.Bounds.Width >= MinCellWidthForLabel && cell.Bounds.Height >= 1) {
            DrawCellLabel(cell.Bounds, node, fillColour);
        }
    }

    internal Color RankColour(int rank, int cellCount)
    {
        double t = cellCount <= 1 ? 1.0 : 1.0 - (rank / (double)(cellCount - 1));

        return t <= 0.5
            ? Lerp(appConfig.Theme.RangeLowBackground, appConfig.Theme.RangeMidBackground, t / 0.5)
            : Lerp(appConfig.Theme.RangeMidBackground, appConfig.Theme.RangeHighBackground, (t - 0.5) / 0.5);
    }

    internal bool IsFading => fadeStepByCellId.Values.Any(step => step < FadeSteps);

    private Color ApplyFade(string cellId, Color targetColour)
    {
        int step = fadeStepByCellId.GetValueOrDefault(cellId);

        if (step < FadeSteps) {
            fadeStepByCellId[cellId] = step + 1;
        }

        double t = Math.Min(1.0, (step + 1) / (double)FadeSteps);
        return Lerp(BackgroundColour, targetColour, t);
    }

    private static Color Lerp(Color from, Color to, double t) => Color.FromArgb(
        255,
        (int)(from.R + ((to.R - from.R) * t)),
        (int)(from.G + ((to.G - from.G) * t)),
        (int)(from.B + ((to.B - from.B) * t)));

    private void PruneFadeState(IReadOnlyList<TreemapCell> cells)
    {
        if (fadeStepByCellId.Count == 0) {
            return;
        }

        HashSet<string> currentIds = [.. cells.Select(cell => cell.Id)];

        foreach (string staleId in fadeStepByCellId.Keys.Where(id => !currentIds.Contains(id)).ToList()) {
            fadeStepByCellId.Remove(staleId);
        }
    }

    private void DrawCellLabel(Rectangle bounds, DiskSpaceFolderNode node, Color background)
    {
        int usableWidth = bounds.Width - 2;

        if (usableWidth <= 0) {
            return;
        }

        string label = $"{node.Name} {node.TotalBytes.ToFormattedByteSize()}";
        List<string> lines = WrapLabelToLines(label, usableWidth, Math.Max(1, bounds.Height));

        for (int row = 0; row < lines.Count; row++) {
            string padded = lines[row] + new string(' ', usableWidth - lines[row].TerminalWidth());
            frame.Clear();
            frame.MoveTo(bounds.X + 1, bounds.Y + row);
            frame.SetColour(ReadableTextColour(background), background);
            frame.Append(padded);
            frame.ResetColour();
            Terminal.Write(frame.AsSpan());
        }
    }

    private static List<string> WrapLabelToLines(string text, int width, int maxLines)
    {
        List<string> lines = new();
        int pos = 0;
        int len = text.Length;
    
        while (pos < len && lines.Count < maxLines) {
            ReadOnlySpan<char> remaining = text.AsSpan(pos);
            bool isLastAllowedLine = lines.Count == maxLines - 1;
            int charsToTake = remaining.TruncateToTerminalWidth(width, out _);
    
            if (charsToTake >= remaining.Length) {
                lines.Add(text[pos..]);
                pos = len;
                continue;
            }
    
            if (isLastAllowedLine) {
                lines.Add(text[pos..(pos + charsToTake)]);
                pos = len;
                continue;
            }
    
            int windowStart = pos;
            int windowEnd = pos + charsToTake;
            int lastSpace = text.LastIndexOf(' ', windowEnd - 1, charsToTake);
    
            if (lastSpace > windowStart) {
                lines.Add(text[windowStart..lastSpace]);
                pos = lastSpace;

                    while (pos < len && text[pos] == ' ') {
                        pos++;
                    }
            }
            else {
                int take = Math.Max(1, charsToTake);
                lines.Add(text[windowStart..(windowStart + take)]);
                pos = windowStart + take;
    
                while (pos < len && text[pos] == ' ') {
                    pos++;
                }
            }
        }
        
        return lines;
    }    
    
    private static Color ReadableTextColour(Color background)
    {
        double luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
        return luminance > 140 ? Color.Black : Color.White;
    }

    private void WriteLine(
        int x, 
        int y, 
        int width, 
        string text, 
        Color foreground)
    {
        if (width <= 0) {
            return;
        }

        int charsToTake = text.TruncateToTerminalWidth(width, out int actualWidth);
        string padded = text[..charsToTake] + new string(' ', width - actualWidth);

        frame.Clear();
        frame.MoveTo(x, y);
        frame.SetColour(foreground, BackgroundColour);
        frame.Append(padded);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
}
