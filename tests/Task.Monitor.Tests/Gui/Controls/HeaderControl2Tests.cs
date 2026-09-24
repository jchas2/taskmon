using System.Text.RegularExpressions;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class HeaderControl2Tests
{
    private static readonly Regex CursorMove = new(@"\x1b\[(\d+);(\d+)H", RegexOptions.Compiled);
    private static readonly Regex Sgr = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public HeaderControl2Tests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private HeaderControl2 CreateControl(int width)
    {
        HeaderControl2 ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            X = 0,
            Y = 0,
            Width = width,
            Height = HeaderControl2.HeaderRows
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    // Replays the cursor moves in the bordered frame to rebuild each screen row (0-based) as plain
    // text, with the colour codes stripped.
    private static Dictionary<int, string> FrameRows(string output)
    {
        Dictionary<int, string> rows = new();
        MatchCollection moves = CursorMove.Matches(output);

        for (int i = 0; i < moves.Count; i++) {
            int row = int.Parse(moves[i].Groups[1].Value) - 1;
            int start = moves[i].Index + moves[i].Length;
            int end = i + 1 < moves.Count ? moves[i + 1].Index : output.Length;

            rows[row] = Sgr.Replace(output[start..end], string.Empty);
        }

        return rows;
    }

    [Fact]
    public void Header_Is_Five_Rows_High() =>
        Assert.Equal(5, HeaderControl2.HeaderRows);

    [Fact]
    public void Draws_The_Banner_Above_A_Bordered_Box()
    {
        const int Width = 120;
        HeaderControl2 ctrl = CreateControl(Width);
        ctrl.Draw();

        string output = CapturedOutput();
        Dictionary<int, string> rows = FrameRows(output);

        Assert.Contains("TASK MONITOR", output);
        Assert.False(rows.ContainsKey(0), "The banner row should sit outside the bordered box.");

        Assert.Equal($"╭{new string('─', Width - 2)}╮", rows[1]);
        Assert.Equal($"╰{new string('─', Width - 2)}╯", rows[4]);

        foreach (int row in new[] { 2, 3 }) {
            Assert.Equal(Width, rows[row].Length);
            Assert.StartsWith("│", rows[row]);
            Assert.EndsWith("│", rows[row]);
        }

        Assert.StartsWith("│Machine: ", rows[2]);
        Assert.StartsWith("│Cpu: ", rows[3]);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Right_Aligns_The_Theme_Name_Inside_The_Border()
    {
        HeaderControl2 ctrl = CreateControl(200);
        ctrl.Draw();

        Dictionary<int, string> rows = FrameRows(CapturedOutput());

        Assert.EndsWith($"{runContext.AppConfig.Theme.Name} │", rows[2]);

        ctrl.Unload();
    }

    [Fact]
    public void Truncates_The_Info_Rows_To_Keep_The_Border_Intact_When_Narrow()
    {
        const int Width = 20;
        HeaderControl2 ctrl = CreateControl(Width);
        ctrl.Draw();

        Dictionary<int, string> rows = FrameRows(CapturedOutput());

        foreach (int row in new[] { 1, 2, 3, 4 }) {
            Assert.Equal(Width, rows[row].Length);
        }

        Assert.EndsWith("│", rows[2]);
        Assert.EndsWith("│", rows[3]);
        Assert.DoesNotContain(runContext.AppConfig.Theme.Name, rows[2]);

        ctrl.Unload();
    }
}
