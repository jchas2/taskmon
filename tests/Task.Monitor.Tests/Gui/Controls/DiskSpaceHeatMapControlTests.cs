using System.Drawing;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.System.Services.DiskSpace;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class DiskSpaceHeatMapControlTests
{
    private readonly RunContext runContext = new RunContextHelper().GetRunContext();

    private DiskSpaceHeatMapControl CreateControl(int width, int height)
    {
        DiskSpaceHeatMapControl ctrl = new(new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            X = 0,
            Y = 0,
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private static DiskSpaceSpecs Specs(params (string Name, long Bytes)[] folders) =>
        new() {
            RootPath = @"C:\",
            State = DiskSpaceScanState.Scanning,
            RootNode = new DiskSpaceFolderNode {
                Path = @"C:\",
                Name = @"C:\",
                Children = [.. folders
                    .OrderByDescending(folder => folder.Bytes)
                    .Select(folder => new DiskSpaceFolderNode {
                        Path = $@"C:\{folder.Name}",
                        Name = folder.Name,
                        TotalBytes = folder.Bytes
                    })]
            }
        };

    private static TreemapCell Cell(string id, int width, int height) =>
        new() { Id = id, Bounds = new Rectangle(0, 0, width, height) };

    [Fact]
    public void RankByArea_Puts_The_Largest_Drawn_Cell_First()
    {
        // Layout order is heaviest first, but rounding has drawn "b" a cell bigger than "a".
        TreemapCell[] cells = [Cell("a", 4, 5), Cell("b", 3, 7), Cell("c", 2, 2)];

        Dictionary<string, int> ranks = DiskSpaceHeatMapControl.RankByArea(cells);

        Assert.Equal(0, ranks["b"]);
        Assert.Equal(1, ranks["a"]);
        Assert.Equal(2, ranks["c"]);
    }

    [Fact]
    public void RankByArea_Keeps_Layout_Order_For_Equal_Areas()
    {
        TreemapCell[] cells = [Cell("a", 4, 4), Cell("b", 2, 8), Cell("c", 8, 2)];

        Dictionary<string, int> ranks = DiskSpaceHeatMapControl.RankByArea(cells);

        Assert.Equal([0, 1, 2], new[] { ranks["a"], ranks["b"], ranks["c"] });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void RankColour_Gives_The_Top_Rank_Exactly_The_High_Colour(int cellCount)
    {
        DiskSpaceHeatMapControl ctrl = CreateControl(40, 12);

        Assert.Equal(
            runContext.AppConfig.Theme.RangeHighBackground.ToArgb(),
            ctrl.RankColour(0, cellCount).ToArgb());
    }

    [Fact]
    public void RankColour_Gives_The_Bottom_Rank_Exactly_The_Low_Colour()
    {
        DiskSpaceHeatMapControl ctrl = CreateControl(40, 12);

        Assert.Equal(
            runContext.AppConfig.Theme.RangeLowBackground.ToArgb(),
            ctrl.RankColour(6, 7).ToArgb());
    }

    [Fact]
    public void The_Largest_Rectangle_Is_Drawn_In_The_High_Colour()
    {
        DiskSpaceHeatMapControl ctrl = CreateControl(60, 16);
        ctrl.Sample(Specs(("Windows", 40_000), ("Users", 30_000), ("Program Files", 20_000), ("Temp", 5_000)));
        ctrl.Draw();

        IReadOnlyDictionary<string, Color> colours = ctrl.HeatColoursForTests;

        Assert.Equal(4, colours.Count);
        Assert.Equal(runContext.AppConfig.Theme.RangeHighBackground.ToArgb(), colours[@"C:\Windows"].ToArgb());
        Assert.Equal(runContext.AppConfig.Theme.RangeLowBackground.ToArgb(), colours[@"C:\Temp"].ToArgb());

        ctrl.Unload();
    }

    // The bug: every visible cell's colour moved whenever the scan found another folder too small
    // to be given a rectangle, because the gradient was spread across every child folder rather
    // than the cells actually drawn.
    [Fact]
    public void Colours_Do_Not_Change_When_A_Folder_Too_Small_To_Draw_Is_Added()
    {
        // A realistic drive-root count: few enough folders that one more undrawn folder moves the
        // old gradient by a visible amount, not a sub-unit change lost to rounding.
        (string, long)[] folders = [
            ("Windows", 1_000_000), ("Users", 400_000), ("Program Files", 200_000), ("ProgramData", 100_000),
            ("tiny0", 1L), ("tiny1", 1L), ("tiny2", 1L)
        ];

        DiskSpaceHeatMapControl ctrl = CreateControl(40, 12);
        ctrl.Sample(Specs(folders));
        ctrl.Draw();

        Dictionary<string, Color> before = new(ctrl.HeatColoursForTests);

        // The premise: the tiny folders are already left undrawn at this size.
        Assert.Equal(4, before.Count);

        ctrl.Sample(Specs([.. folders, ("tiny3", 1L)]));
        ctrl.Draw();

        Dictionary<string, Color> after = new(ctrl.HeatColoursForTests);

        Assert.DoesNotContain(@"C:\tiny3", after.Keys);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());

        foreach ((string id, Color colour) in before) {
            Assert.Equal(colour.ToArgb(), after[id].ToArgb());
        }

        ctrl.Unload();
    }
}
