using Task.Monitor.Gui.Controls.Performance;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformanceControlScrollTests
{
    [Theory]
    // Selection inside the viewport: the offset does not move.
    [InlineData(0, 0, 3, 5, 0)]
    [InlineData(2, 0, 3, 5, 0)]
    [InlineData(3, 2, 3, 5, 2)]
    // Selection past the bottom edge: the offset follows by one.
    [InlineData(3, 0, 3, 5, 1)]
    [InlineData(4, 1, 3, 5, 2)]
    // Selection above the top edge: the offset snaps to the selection.
    [InlineData(1, 3, 3, 5, 1)]
    [InlineData(0, 2, 3, 5, 0)]
    // Viewport at least as tall as the list: never scrolls.
    [InlineData(4, 0, 5, 5, 0)]
    [InlineData(4, 0, 8, 5, 0)]
    // Stale offset after the viewport grows: clamped back to the last full page.
    [InlineData(0, 4, 3, 5, 0)]
    [InlineData(4, 9, 3, 5, 2)]
    public void ClampScrollOffset_KeepsSelectionVisible(
        int selectedIndex,
        int scrollOffset,
        int visibleCount,
        int total,
        int expected)
    {
        int result = PerformanceControl.ClampScrollOffset(selectedIndex, scrollOffset, visibleCount, total);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 3, 5)]
    [InlineData(3, 0, 3, 5)]
    [InlineData(4, 1, 2, 5)]
    [InlineData(2, 5, 4, 5)]
    [InlineData(4, 9, 3, 5)]
    public void ClampScrollOffset_LeavesSelectionVisibleAndInBounds(
        int selectedIndex,
        int scrollOffset,
        int visibleCount,
        int total)
    {
        int result = PerformanceControl.ClampScrollOffset(selectedIndex, scrollOffset, visibleCount, total);

        Assert.InRange(result, 0, Math.Max(0, total - visibleCount));
        Assert.InRange(selectedIndex, result, result + visibleCount - 1);
    }
}
