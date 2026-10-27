using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ListSorterTests
{
    private readonly RunContext      runContext;
    private readonly ListView        targetView;
    private readonly ListSorter<Row> sorter;

    private sealed record Row(string Name, string? Version, long? Size);

    public ListSorterTests()
    {
        runContext = new RunContextHelper().GetRunContext();
        targetView = new ListView(runContext.Terminal);

        targetView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("VERSION"))
            .Add(new ListViewColumnHeader("SIZE"));

        sorter = new ListSorter<Row>(runContext.Terminal, targetView, [
            ListSortColumn<Row>.ByText("NAME", 0, row => row.Name),
            ListSortColumn<Row>.ByVersion("VERSION", 1, row => row.Version),
            ListSortColumn<Row>.ByValue<long>("SIZE", 2, row => row.Size)
        ]);

        sorter.ApplyTheme(runContext.AppConfig.Theme);
        sorter.Layout(0, 0, 20);
    }

    [Fact]
    public void A_And_D_Change_The_Direction()
    {
        Assert.True(Press(ConsoleKey.D, out bool handled));
        Assert.True(handled);
        Assert.Equal("0:desc", sorter.Signature);

        Press(ConsoleKey.A, out _);
        Assert.Equal("0:asc", sorter.Signature);
    }

    [Fact]
    public void ByVersion_Compares_The_Numeric_Part_And_Puts_Unparsable_Versions_Last()
    {
        Press(ConsoleKey.S, out _);
        Press(ConsoleKey.DownArrow, out _);
        Press(ConsoleKey.Enter, out _);

        List<Row> sorted = sorter.Sort([
            new("a", "10.0", 1), new("b", "9.1", 1), new("c", "9.10", 1), new("d", "6.0.0d8", 1), new("e", "n/a", 1), new("f", "9420.2", 1)
        ]);

        Assert.Equal(["d", "b", "c", "a", "f", "e"], sorted.Select(row => row.Name));

        Press(ConsoleKey.D, out _);
        sorted = sorter.Sort(sorted);

        Assert.Equal(["f", "a", "c", "b", "d", "e"], sorted.Select(row => row.Name));
    }

    [Fact]
    public void DecorateHeaders_Marks_Only_The_Sorted_Column()
    {
        sorter.DecorateHeaders();

        Assert.Equal("NAME↑", targetView.ColumnHeaders[0].Text);
        Assert.NotNull(targetView.ColumnHeaders[0].BackgroundColour);

        Press(ConsoleKey.S, out _);
        Press(ConsoleKey.DownArrow, out _);
        Press(ConsoleKey.Enter, out _);
        Press(ConsoleKey.D, out _);
        sorter.DecorateHeaders();

        Assert.Equal("NAME", targetView.ColumnHeaders[0].Text);
        Assert.Null(targetView.ColumnHeaders[0].BackgroundColour);
        Assert.Equal("VERSION↓", targetView.ColumnHeaders[1].Text);
        Assert.NotNull(targetView.ColumnHeaders[1].BackgroundColour);
    }

    [Fact]
    public void Enter_Picks_The_Highlighted_Column_And_Closes_The_Picker()
    {
        Press(ConsoleKey.S, out _);
        Assert.False(Press(ConsoleKey.DownArrow, out bool handled));
        Assert.True(handled);

        Assert.True(Press(ConsoleKey.Enter, out _));
        Assert.False(sorter.IsSelectionActive);
        Assert.Equal("1:asc", sorter.Signature);
    }

    [Fact]
    public void Escape_Closes_The_Picker_And_Is_Left_Unhandled_Otherwise()
    {
        Assert.False(Press(ConsoleKey.Escape, out bool handled));
        Assert.False(handled);

        Press(ConsoleKey.S, out _);

        Assert.True(Press(ConsoleKey.Escape, out handled));
        Assert.True(handled);
        Assert.False(sorter.IsSelectionActive);
    }

    [Fact]
    public void Left_And_Right_Are_Swallowed_Only_While_The_Picker_Is_Open()
    {
        Press(ConsoleKey.LeftArrow, out bool handled);
        Assert.False(handled);

        Press(ConsoleKey.S, out _);

        Press(ConsoleKey.LeftArrow, out handled);
        Assert.True(handled);
        Press(ConsoleKey.RightArrow, out handled);
        Assert.True(handled);
        Assert.True(sorter.IsSelectionActive);
    }

    private bool Press(ConsoleKey key, out bool handled)
    {
        handled = false;
        return sorter.KeyPressed(ControlHelper.GetConsoleKeyInfo(key), ref handled);
    }

    [Fact]
    public void Sort_Is_Stable_And_Puts_Blanks_Last_In_Both_Directions()
    {
        Row[] rows = [new("b", "1.0", 1), new("", "1.0", 2), new("a", "1.0", 3), new("a", "1.0", 4)];

        Assert.Equal([3, 4, 1, 2], sorter.Sort(rows).Select(row => (int)row.Size!));

        Press(ConsoleKey.D, out _);

        Assert.Equal([1, 3, 4, 2], sorter.Sort(rows).Select(row => (int)row.Size!));
    }

    [Fact]
    public void S_Opens_And_Closes_The_Picker_And_Sets_The_Picker_Width()
    {
        Assert.Equal(0, sorter.PickerWidth);

        Assert.True(Press(ConsoleKey.S, out _));
        Assert.True(sorter.IsSelectionActive);
        Assert.Equal(21, sorter.PickerWidth);
        Assert.Same(sorter.SortView, sorter.FocusTarget);

        Assert.True(Press(ConsoleKey.S, out _));
        Assert.False(sorter.IsSelectionActive);
        Assert.Equal(0, sorter.PickerWidth);
        Assert.Same(targetView, sorter.FocusTarget);
    }
}
