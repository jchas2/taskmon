using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ListFilterTests : IDisposable
{
    private readonly RunContext runContext;
    private readonly ListFilter filter;

    public ListFilterTests()
    {
        runContext = new RunContextHelper().GetRunContext();

        Control owner = new(runContext.Terminal) {
            Width  = 80,
            Height = 20
        };

        filter = new ListFilter(owner, runContext.Terminal, "Filter Test");
        filter.ApplyTheme(runContext.AppConfig.Theme);
    }

    [Fact]
    public void BuildHeaderText_Shows_The_Match_Count_Only_While_Filtered()
    {
        Assert.Empty(filter.BuildHeaderText(3, 10));

        filter.Text = "abc";

        Assert.Equal("Filter: abc (3 of 10)", filter.BuildHeaderText(3, 10));
        Assert.Equal("Filter: abc (3)",       filter.BuildHeaderText(3, 10, showTotal: false));
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Enter_In_The_Dialog_Applies_The_Trimmed_Text()
    {
        Press(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false));
        Type("  abc ");

        bool redraw = Press(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));

        Assert.True(redraw);
        Assert.Equal("abc", filter.Text);
        Assert.False(filter.IsDialogVisible);
        Assert.True(Control.RedrawEnabled);
    }

    [Fact]
    public void Escape_Clears_An_Active_Filter()
    {
        filter.Text = "abc";

        bool handled = false;
        bool redraw  = filter.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape), ref handled);

        Assert.True(handled);
        Assert.True(redraw);
        Assert.False(filter.IsActive);
    }

    [Fact]
    public void Escape_In_The_Dialog_Keeps_The_Previous_Text()
    {
        filter.Text = "abc";
        Press(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false));
        Type("xyz");

        bool redraw = Press(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape));

        Assert.True(redraw);
        Assert.Equal("abc", filter.Text);
        Assert.False(filter.IsDialogVisible);
        Assert.True(Control.RedrawEnabled);
    }

    [Fact]
    public void Escape_Without_A_Filter_Is_Left_Unhandled()
    {
        bool handled = false;
        bool redraw  = filter.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape), ref handled);

        Assert.False(handled);
        Assert.False(redraw);
    }

    [Fact]
    public void Every_Key_Is_Handled_While_The_Dialog_Is_Open()
    {
        Press(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false));

        foreach (ConsoleKey key in new[] { ConsoleKey.Tab, ConsoleKey.F2, ConsoleKey.LeftArrow, ConsoleKey.R }) {
            bool handled = false;
            filter.KeyPressed(ControlHelper.GetConsoleKeyInfo(key), ref handled);

            Assert.True(handled);
            Assert.True(filter.IsDialogVisible);
        }
    }

    [Fact]
    public void FooterHint_Adds_The_Clear_Hint_While_Filtered()
    {
        Assert.Equal("f Filter", filter.FooterHint);

        filter.Text = "abc";

        Assert.Equal("f Filter     Esc Clear Filter", filter.FooterHint);
    }

    [Fact]
    public void F_Opens_The_Dialog_And_Turns_Redraws_Off()
    {
        bool handled = false;
        bool redraw  = filter.KeyPressed(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false), ref handled);

        Assert.True(handled);
        Assert.False(redraw);
        Assert.True(filter.IsDialogVisible);
        Assert.False(Control.RedrawEnabled);
    }

    [Fact]
    public void Matches_Everything_Without_A_Filter()
    {
        Assert.True(filter.Matches("anything"));
        Assert.True(filter.Matches());
    }

    [Fact]
    public void Matches_Is_Case_Insensitive_And_Ignores_Null_Fields()
    {
        filter.Text = "NVIDIA";

        Assert.True(filter.Matches(null, "nvidia-drv"));
        Assert.False(filter.Matches(null, "amd-gpu"));
    }

    private bool Press(ConsoleKeyInfo keyInfo)
    {
        bool handled = false;
        return filter.KeyPressed(keyInfo, ref handled);
    }

    private void Type(string text)
    {
        foreach (char ch in text) {
            Press(new ConsoleKeyInfo(ch, 0, false, false, false));
        }
    }
}
