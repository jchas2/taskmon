using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;

using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigTests
{
    private readonly Mock<IFileSystem> fileSystem;
    private readonly string testConfigPath = "/dummy/path/taskmon.ini";
    
    public AppConfigTests() =>
        fileSystem = new Mock<IFileSystem>();
    
    [Fact]
    public void Constructor_With_FileSystem_Initialises_Successfully()
    {
        var appConfig = new AppConfig(fileSystem.Object);

        Assert.NotNull(appConfig);
        Assert.NotNull(appConfig.Themes);
    }

    [Fact]
    public void Constructor_With_FileSystem_And_Config_Initialises_Successfully()
    {
        Config config = new();
        AppConfig appConfig = new(fileSystem.Object, config);

        Assert.NotNull(appConfig);
        Assert.NotNull(appConfig.Themes);
    }
    
    private static string DefaultIniFile => @"
[filter]
pid=-1
username=
process=

[sort]
col=Cpu
asc=False

[stats]
cols=Process, Pid, User, Pri, Cpu, Thrd, Gpu, Mem, Path, Disk
delay=1500
nprocs=-1

[ux]
confirm-task-delete=True
default-theme=Taskmon Default
highlight-daemons=True
highlight-stats-col-update=True
metre-style=Dots
multi-select-procs=False
show-metre-cpu-numerically=True
show-metre-disk-numerically=True
show-metre-mem-numerically=True
show-metre-swap-numerically=True
show-y-axis-scale=True
use-irix-cpu-reporting=True
";
    
    public static TheoryData<string> IniFileData()
        => new()
        {
            DefaultIniFile,     // Defaults in the ini file mapping to defaults on the AppConfig property getters.
            string.Empty        // Empty file forces AppConfig to use defaults for all property getters.
        };

    [Theory]
    [MemberData(nameof(IniFileData))]
    public void Should_Load_AndOr_Parse_DefaultIniFile(string iniFileData)
    {
        Config? iniConfig = Config.FromString(iniFileData);

        Assert.NotNull(iniConfig);

        AppConfig appConfig = new(fileSystem.Object, iniConfig);
        
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        Assert.True(appConfig.ConfirmTaskDelete);
        Assert.NotNull(appConfig.DefaultConfigFilePath);
        Assert.NotEmpty(appConfig.DefaultConfigFilePath);

        Assert.NotNull(appConfig.Theme);
        Assert.Equal("Taskmon Default", appConfig.Theme.Name);
        //Assert.Equal(ConsolePalette.Transparent, appConfig.DefaultTheme.Background);
        Assert.Equal(appConfig.Theme.Background, appConfig.Theme.ChartBackground);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.Theme.BackgroundHighlight);
        Assert.Equal(ConsolePalette.Blue,       appConfig.Theme.ColumnCommandLowPriority);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.ColumnCommandHighCpu);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.Theme.ColumnCommandIoBound);
        Assert.Equal(ConsolePalette.Green,      appConfig.Theme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.ColumnCommandScript);
        Assert.Equal(ConsolePalette.Green,      appConfig.Theme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsolePalette.Magenta,    appConfig.Theme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsolePalette.White,      appConfig.Theme.ColumnUserRoot);
        Assert.Equal(ConsolePalette.Gray,       appConfig.Theme.ColumnUserSystem);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.Theme.CommandBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.CommandForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.DeltaHighlightColour);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.Error);
        Assert.Equal(ConsolePalette.White,      appConfig.Theme.Foreground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.ForegroundHighlight);
        Assert.Equal(ConsolePalette.DarkGreen,  appConfig.Theme.HeaderBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.HeaderForeground);
        Assert.Equal(ConsolePalette.DarkBlue,   appConfig.Theme.MenubarBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.MenubarForeground);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.RangeHighBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeHighForeground);
        Assert.Equal(ConsolePalette.Green,      appConfig.Theme.RangeLowBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeLowForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.RangeMidBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeMidForeground);

        //Assert.Equal(Processor.DefaultDelayInMilliseconds, appConfig.DelayInMilliseconds);
        Assert.Equal(-1, appConfig.FilterPid);
        Assert.Equal(string.Empty, appConfig.FilterUserName);
        Assert.Equal(string.Empty, appConfig.FilterProcess);
        Assert.True(appConfig.HighlightDaemons);
        Assert.Equal(MetreControlStyle.Dots, appConfig.MetreStyle);
        Assert.False(appConfig.MultiSelectProcesses);
        Assert.Equal(-1, appConfig.NumberOfProcesses);
        Assert.Equal(Statistics.Cpu, appConfig.SortColumn);
        Assert.False(appConfig.SortAscending);
        Assert.True(appConfig.ShowMetreCpuNumerically);
        Assert.True(appConfig.ShowMetreGpuNumerically);
        Assert.True(appConfig.ShowMetreDiskNumerically);
        Assert.True(appConfig.ShowMetreMemoryNumerically);
        Assert.True(appConfig.ShowMetreGpuMemNumerically);
        Assert.True(appConfig.ShowMetreSwapNumerically);
        Assert.True(appConfig.ShowMetreNetworkNumerically);
        Assert.True(appConfig.ShowYAxisScale);

        if (!string.IsNullOrEmpty(iniFileData)) {
            Assert.True(appConfig.UseIrixReporting);
        }
    }
    
    internal static string CustomIniFile => @"
[filter]
pid=123456
username=root
process=kernel_task

[sort]
col=Mem
asc=True

[stats]
cols=Process, Pid, User, Pri, Cpu, Thrd, Gpu, Mem, Path, Disk, AvgCpu, AvgGpu, AvgMem, AvgDisk, MaxCpu, MaxGpu, MaxMem, MaxDisk
delay=2000
nprocs=5

[ux]
confirm-task-delete=False
default-theme=MS-DOS
highlight-daemons=False
highlight-stats-col-update=False
metre-style=Bars
multi-select-procs=True
show-metre-cpu-numerically=False
show-metre-disk-numerically=False
show-metre-mem-numerically=False
show-metre-swap-numerically=False
show-metre-gpu-numerically=False
show-metre-gpu-mem-numerically=False
show-metre-network-numerically=False
show-y-axis-scale=False
use-irix-cpu-reporting=False
";
    
    [Fact]
    public void Should_Load_And_Parse_CustomIniFile()
    {
        Config? iniConfig = Config.FromString(CustomIniFile);
        
        Assert.NotNull(iniConfig);
        
        AppConfig appConfig = new(fileSystem.Object, iniConfig);
        
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        Assert.False(appConfig.ConfirmTaskDelete);
        Assert.NotNull(appConfig.DefaultConfigFilePath);
        Assert.NotEmpty(appConfig.DefaultConfigFilePath);
        
        Assert.NotNull(appConfig.Theme);
        Assert.Equal("MS-DOS", appConfig.Theme.Name);
        Assert.Equal(ConsolePalette.DarkBlue,   appConfig.Theme.Background);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.Theme.BackgroundHighlight);
        Assert.Equal(ConsolePalette.Gray,       appConfig.Theme.ColumnCommandLowPriority);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.ColumnCommandHighCpu);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.ColumnCommandIoBound);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.ColumnCommandScript);
        Assert.Equal(ConsolePalette.Gray,       appConfig.Theme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsolePalette.DarkGray,   appConfig.Theme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.ColumnUserRoot);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.ColumnUserSystem);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.Theme.CommandBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.CommandForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.DeltaHighlightColour);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.Error);
        Assert.Equal(ConsolePalette.DarkGray,   appConfig.Theme.Foreground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.ForegroundHighlight);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.Theme.HeaderBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.HeaderForeground);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.Theme.MenubarBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.MenubarForeground);
        Assert.Equal(ConsolePalette.Red,        appConfig.Theme.RangeHighBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeHighForeground);
        Assert.Equal(ConsolePalette.Green,      appConfig.Theme.RangeLowBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeLowForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.Theme.RangeMidBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.Theme.RangeMidForeground);

        Assert.Equal(2000, appConfig.DelayInMilliseconds);
        Assert.Equal(123456, appConfig.FilterPid);
        Assert.Equal("root", appConfig.FilterUserName);
        Assert.Equal("kernel_task", appConfig.FilterProcess);
        Assert.False(appConfig.HighlightDaemons);
        Assert.Equal(MetreControlStyle.Bars, appConfig.MetreStyle);
        Assert.True(appConfig.MultiSelectProcesses);
        Assert.Equal(5, appConfig.NumberOfProcesses);
        Assert.Equal(Statistics.Mem, appConfig.SortColumn);
        Assert.True(appConfig.SortAscending);
        Assert.False(appConfig.ShowMetreCpuNumerically);
        Assert.False(appConfig.ShowMetreDiskNumerically);
        Assert.False(appConfig.ShowMetreMemoryNumerically);
        Assert.False(appConfig.ShowMetreSwapNumerically);
        Assert.False(appConfig.ShowMetreGpuNumerically);
        Assert.False(appConfig.ShowMetreGpuMemNumerically);
        Assert.False(appConfig.ShowMetreNetworkNumerically);
        Assert.False(appConfig.ShowYAxisScale);
        Assert.False(appConfig.UseIrixReporting);
    }

    [Fact]
    public void Themes_After_Construction_Returns_Non_Empty_List()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotNull(appConfig.Themes);
        Assert.NotEmpty(appConfig.Themes);
    }

    [Fact]
    public void Themes_After_Construction_Contains_Expected_Themes()
    {
        AppConfig appConfig = new(fileSystem.Object);
        var themeNames = appConfig.Themes.Select(t => t.Name.ToLower()).ToList();

        // TODO:
        // foreach (string themeName in PredefinedThemes) {
        //     Assert.Contains(themeName, themeNames);
        // }
        //
        // Assert.Equal(ThemeCount, appConfig.Themes.Count);        
    }

    [Fact]
    public void Default_Theme_After_Construction_Returns_Colour_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotNull(appConfig.Theme);
        Assert.Equal("Taskmon Default", appConfig.Theme.Name);
    }

    [Fact]
    public void Default_Theme_Set_To_Valid_Theme_Updates_Default_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme theme = appConfig.Themes.First(t => t.Name == "MS-DOS");

        appConfig.Theme = theme;
        Assert.Equal(theme, appConfig.Theme);
    }

    [Fact]
    public void Default_Theme_Set_To_Invalid_Theme_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme invalidTheme = new(new ConfigSection("theme-invalid"));

        Assert.Throws<InvalidOperationException>(() => appConfig.Theme = invalidTheme);
    }    

    // TODO:
    // public static TheoryData<string> ThemeNameData()
    //     => new() 
    //     {
    //         Constants.Sections.ThemeColour,
    //         Constants.Sections.ThemeMono,
    //         Constants.Sections.ThemeMatrix,
    //         Constants.Sections.ThemeTokyoNight,
    //         Constants.Sections.ThemeMsDos
    //     };
    //
    // [Theory]
    // [MemberData(nameof(ThemeNameData))]
    // public void Should_Load_Valid_UxTheme_From_Name_Without_Theme_Section_Defined(string themeName)
    // {
    //     string iniString = $"[ux]\ndefault-theme={themeName}\n";
    //     Config? iniConfig = Config.FromString(iniString);
    //     
    //     Assert.NotNull(iniConfig);
    //     
    //     AppConfig appConfig = new(fileSystem.Object, iniConfig);
    //
    //     Assert.NotNull(appConfig.DefaultTheme);
    //     Assert.Equal(themeName, appConfig.DefaultTheme.Name);
    // }
    
    [Fact]
    public void TryLoad_With_Empty_Config_Returns_True()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Config config = new();
        bool result = appConfig.TryLoad(config);

        Assert.True(result);
    }

    [Fact]
    public void TryLoad_With_Valid_Path_Returns_True()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(DefaultIniFile);

        bool result = appConfig.TryLoad(testConfigPath);

        Assert.True(result);
    }

    [Fact]
    public void TryLoad_With_Invalid_Path_Returns_False()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(false);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Throws(new FileNotFoundException());

        bool result = appConfig.TryLoad(testConfigPath);

        Assert.False(result);
    }

    [Fact]
    public void New_Config_ToString_Contains_Expected_Sections()
    {
        AppConfig appConfig = new(fileSystem.Object);
        string iniConfig = appConfig.ToString();
        List<string> expectedSections = new() {
            "[filter]",
            "[sort]",
            "[stats]",
            "[ux]",
        };
        
        Assert.NotNull(iniConfig);
        Assert.NotEmpty(iniConfig);
        expectedSections.ForEach(s => Assert.Contains(s, iniConfig));
        
        // Have any extras been added we don't know about.
        Assert.Equal(iniConfig.Count(ch => ch == '['), expectedSections.Count);
        Assert.Equal(iniConfig.Count(ch => ch == ']'), expectedSections.Count);
    }
    
    [Fact]
    public void TrySave_With_Valid_Path_Returns_True()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.WriteAllText(testConfigPath, It.IsAny<string>()));

        bool result = appConfig.TrySave(testConfigPath);

        Assert.True(result);
    }

    [Fact]
    public void TrySave_With_Invalid_Path_Returns_False()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.WriteAllText(testConfigPath, It.IsAny<string>())).Throws(new IOException());

        bool result = appConfig.TrySave(testConfigPath);

        Assert.False(result);
    }

    [Fact]
    public void DefaultConfigFilePath_ReturnsNonNullPath()
    {
        AppConfig appConfig = new(fileSystem.Object);

        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        
        string? result = appConfig.DefaultConfigFilePath;

        Assert.NotNull(result);
        Assert.Contains("taskmon.ini", result);
    }

    [Fact]
    public void LoadThemes_ManifestThemes_Write_To_Disk_On_Load()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotEmpty(appConfig.Themes);
        Assert.Contains(appConfig.Themes, t => t.Name == "Taskmon Default");
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith($"Taskmon Default{Constants.ThemeExtension}")),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void LoadThemes_Custom_Themes_On_Disk_Are_Loaded()
    {
        string customThemeIni = @"
[My Custom Theme]
colour-mode=truecolour
control.background=#123456
control.foreground=#abcdef
";
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(["/fake/path/themes/My Custom Theme.theme"]);
        fileSystem.Setup(fs => fs.ReadAllText("/fake/path/themes/My Custom Theme.theme")).Returns(customThemeIni);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.Contains(appConfig.Themes, t => t.Name == "My Custom Theme");
        Theme customTheme = appConfig.Themes.First(t => t.Name == "My Custom Theme");
        Assert.Equal(ColorTranslator.FromHtml("#123456"), customTheme.Background);
    }

    // Every shipped layout, and the charts each row held under the old grid format (row by row,
    // left to right) - the trees they were converted to must reproduce exactly that order, with
    // the process list last.
    public static TheoryData<string, PaneControlType[]> ShippedLayouts()
    {
        PaneControlType[] allCharts = [
            PaneControlType.Cpu, PaneControlType.Gpu, PaneControlType.Disk, PaneControlType.NetworkSent,
            PaneControlType.Memory, PaneControlType.GpuMemory, PaneControlType.VirtualMemory, PaneControlType.NetworkReceived,
            PaneControlType.Process
        ];
        PaneControlType[] cpuMemory = [PaneControlType.Cpu, PaneControlType.Memory, PaneControlType.Process];
        PaneControlType[] gpuGpuMemory = [PaneControlType.Gpu, PaneControlType.GpuMemory, PaneControlType.Process];
        PaneControlType[] network = [PaneControlType.NetworkSent, PaneControlType.NetworkReceived, PaneControlType.Process];
        PaneControlType[] disk = [PaneControlType.Disk, PaneControlType.Process];

        return new() {
            { "All Charts", allCharts },
            { "All Charts Large", allCharts },
            { "Cpu and Memory", cpuMemory },
            { "Cpu and Memory Large", cpuMemory },
            { "Gpu and Gpu Memory", gpuGpuMemory },
            { "Gpu and Gpu Memory Large", gpuGpuMemory },
            { "Network Send and Receive Bytes", network },
            { "Network Send and Receive Bytes Large", network },
            { "Disk Read and Write Bytes", disk },
            { "Disk Read and Write Bytes Large", disk },
        };
    }

    [Theory]
    [MemberData(nameof(ShippedLayouts))]
    public void Shipped_Layouts_Load_As_Trees_With_The_Original_Chart_Order(string name, PaneControlType[] expected)
    {
        // No fileSystem setup - the on-disk scan is skipped, leaving only the embedded layouts.
        AppConfig appConfig = new(fileSystem.Object);

        SummaryControlLayout layout = appConfig.Layouts.Single(l => l.Name == name);
        SummaryLayoutTree tree = layout.ToTree();

        Assert.Equal(expected, tree.Panes().Select(p => p.ControlType).ToArray());

        // Like the old grid, the process list uses the app-wide columns - no per-pane override.
        Assert.Null(tree.Panes().Single(p => p.ControlType == PaneControlType.Process).ProcessColumns);
    }

    // The 1/N, 1/(N-1), ... ratio chains that stand in for the old grid's equal cells: every
    // chart in All Charts comes out the same size, and the charts keep the grid's 60% height.
    [Fact]
    public void Shipped_All_Charts_Reproduces_The_Grid_Geometry()
    {
        AppConfig appConfig = new(fileSystem.Object);
        SummaryLayoutTree tree = appConfig.Layouts.Single(l => l.Name == "All Charts").ToTree();

        SummaryLayoutRenderer renderer = new();
        renderer.Layout(tree, new Dictionary<int, Task.Monitor.System.Controls.Control>(), 0, 0, 120, 40);

        List<Rectangle> charts = tree.Panes()
            .Where(p => p.ControlType != PaneControlType.Process)
            .Select(p => renderer.PaneBounds[p.Id])
            .ToList();

        Assert.All(charts, r => Assert.Equal(new Size(30, 12), r.Size));

        Rectangle process = renderer.PaneBounds[tree.Panes().Single(p => p.ControlType == PaneControlType.Process).Id];
        Assert.Equal(new Rectangle(0, 24, 120, 16), process);
    }

    [Fact]
    public void DefaultLayout_Falls_Back_To_All_Charts_When_None_Is_Configured()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Equal("All Charts", appConfig.DefaultLayout?.Name);
    }

    [Fact]
    public void LoadLayouts_Loads_A_Custom_Tree_Layout_From_Disk()
    {
        string treeLayoutIni = @"
[My Dashboard]
layout-type=tree
root=0
nodes=0,1,2
node.0=split,row,0.5,1,2
node.1=pane,cpu
node.2=pane,process,process+pid+cpu+mem
";
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(["/fake/path/summary-layouts/My Dashboard.layout"]);
        fileSystem.Setup(fs => fs.ReadAllText("/fake/path/summary-layouts/My Dashboard.layout")).Returns(treeLayoutIni);

        AppConfig appConfig = new(fileSystem.Object);

        SummaryLayoutTree tree = appConfig.Layouts.Single(t => t.Name == "My Dashboard").ToTree();
        SummaryLayoutNode processPane = tree.Panes().Single(p => p.ControlType == PaneControlType.Process);

        Assert.Equal(
            Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem,
            processPane.ProcessColumns);
    }

    // A version 1 fixed-grid layout file (no layout-type key) copied into the folder is skipped,
    // not loaded as a tree with default contents.
    [Fact]
    public void LoadLayouts_Skips_A_Version_1_Grid_Layout_On_Disk()
    {
        string gridLayoutIni = @"
[My Grid]
ratio=0.6
num-rows=2
num-cols=4
charts=0,1,2,3,4,5,6,7
";
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(["/fake/path/summary-layouts/My Grid.layout"]);
        fileSystem.Setup(fs => fs.ReadAllText("/fake/path/summary-layouts/My Grid.layout")).Returns(gridLayoutIni);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.DoesNotContain(appConfig.Layouts, t => t.Name == "My Grid");
        Assert.Contains(appConfig.Layouts, t => t.Name == "All Charts");
    }

    [Fact]
    public void LoadLayouts_Flags_Shipped_Layouts_As_Built_In_And_Custom_Ones_As_Not()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(["/fake/path/summary-layouts/My Dashboard.layout"]);
        fileSystem.Setup(fs => fs.ReadAllText("/fake/path/summary-layouts/My Dashboard.layout")).Returns(TreeLayoutIni("My Dashboard"));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.True(appConfig.Layouts.Single(l => l.Name == "All Charts").IsBuiltIn);
        Assert.False(appConfig.Layouts.Single(l => l.Name == "My Dashboard").IsBuiltIn);
    }

    // The file on disk under a shipped layout's name is its deployed copy, so the shipped layout is
    // what loads - an edited copy doesn't shadow it, the same as a shipped theme.
    [Theory]
    [InlineData("/fake/path/summary-layouts/All Charts.layout")]
    [InlineData("/fake/path/summary-layouts/Renamed File.layout")]
    public void LoadLayouts_The_Shipped_Layout_Wins_Over_A_File_On_Disk_With_Its_Name(string layoutFile)
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns([layoutFile]);
        fileSystem.Setup(fs => fs.ReadAllText(layoutFile)).Returns(TreeLayoutIni("All Charts"));

        AppConfig appConfig = new(fileSystem.Object);

        SummaryControlLayout allCharts = Assert.Single(appConfig.Layouts, l => l.Name == "All Charts");
        Assert.True(allCharts.IsBuiltIn);
        Assert.Equal(9, allCharts.ToTree().Panes().Count());
    }

    [Fact]
    public void LoadLayouts_Deploys_A_Shipped_Layout_Whose_Copy_On_Disk_Has_Changed()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(It.IsAny<string>())).Returns("[All Charts]\nstale=true\n");

        _ = new AppConfig(fileSystem.Object);

        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith($"All Charts{Constants.LayoutExtension}")),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("All Charts", Constants.LayoutExtension)]
    [InlineData("Taskmon Default", Constants.ThemeExtension)]
    public void Load_Does_Not_Rewrite_A_Shipped_File_Whose_Copy_On_Disk_Is_Unchanged(string name, string extension)
    {
        // A first load with nothing on disk captures the text each shipped file is deployed with.
        Dictionary<string, string> deployed = new();
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, text) => deployed[path] = text);

        _ = new AppConfig(fileSystem.Object);

        string filePath = deployed.Keys.Single(p => p.EndsWith($"{name}{extension}"));

        // A second load finds that same text on disk, so leaves the file alone.
        fileSystem.Invocations.Clear();
        fileSystem.Setup(fs => fs.FileExists(filePath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(filePath)).Returns(deployed[filePath]);

        _ = new AppConfig(fileSystem.Object);

        fileSystem.Verify(fs => fs.WriteAllText(filePath, It.IsAny<string>()), Times.Never);
    }

    // Version 1's layouts folder holds fixed-grid layouts this version can't read. It is left as
    // it is, so a version 1 install alongside keeps working.
    [Fact]
    public void Layouts_Are_Never_Read_From_Or_Written_To_The_Version_1_Layouts_Folder()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        appConfig.SaveLayout(SummaryControlLayout.FromTree("Saved Dashboard", SummaryLayoutTree.CreateExample()));

        fileSystem.Verify(fs => fs.GetFiles(It.Is<string>(p => Path.GetFileName(p) == "summary-layouts")), Times.Once);
        fileSystem.Verify(fs => fs.GetFiles(It.Is<string>(p => Path.GetFileName(p) == "layouts")), Times.Never);
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => Path.GetFileName(Path.GetDirectoryName(p)) == "layouts"),
            It.IsAny<string>()), Times.Never);
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith(Path.Combine("summary-layouts", $"Saved Dashboard{Constants.LayoutExtension}"))),
            It.IsAny<string>()), Times.Once);
    }

    private AppConfig LoadFromIni(string iniText)
    {
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(iniText);

        return new AppConfig(fileSystem.Object, Config.FromFile(fileSystem.Object, testConfigPath));
    }

    [Fact]
    public void The_Default_Layout_Is_Read_From_The_Config()
    {
        AppConfig appConfig = LoadFromIni("[ux]\ndefault-summary-layout=Cpu and Memory\n");

        Assert.Equal("Cpu and Memory", appConfig.DefaultLayout?.Name);
    }

    [Fact]
    public void A_New_Config_Starts_With_All_Charts_As_The_Default_Layout()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Contains("default-summary-layout=All Charts", appConfig.ToString());
        Assert.Equal("All Charts", appConfig.DefaultLayout?.Name);
    }

    // Version 1's keys belong to a version 1 install alongside: left alone when present, and not
    // added to a config that doesn't have them.
    [Fact]
    public void Version_1_Layout_Keys_Are_Left_Alone_But_Not_Added()
    {
        AppConfig existing = LoadFromIni("[ux]\ndefault-layout=My Grid\nuse-large-charts=True\n");

        Assert.Contains("default-layout=My Grid", existing.ToString());
        Assert.Contains("use-large-charts=True", existing.ToString());
        Assert.Equal("All Charts", existing.DefaultLayout?.Name);

        AppConfig fresh = new(fileSystem.Object);

        Assert.DoesNotContain("default-layout=", fresh.ToString());
        Assert.DoesNotContain("use-large-charts", fresh.ToString());
    }

    private static string TreeLayoutIni(string name) => $@"
[{name}]
layout-type=tree
root=0
nodes=0,1,2
node.0=split,row,0.5,1,2
node.1=pane,cpu
node.2=pane,memory
";

    [Fact]
    public void SaveLayout_Writes_The_File_And_Adds_It_To_Layouts()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Saved Dashboard", SummaryLayoutTree.CreateExample());

        bool result = appConfig.SaveLayout(layout);

        Assert.True(result);
        Assert.Contains(appConfig.Layouts, t => t.Name == "Saved Dashboard");
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith($"Saved Dashboard{Constants.LayoutExtension}")),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void DefaultLayout_Set_To_Invalid_Layout_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout invalid = SummaryControlLayout.FromTree("Not Registered", SummaryLayoutTree.CreateExample());

        Assert.Throws<InvalidOperationException>(() => appConfig.DefaultLayout = invalid);
    }

    [Fact]
    public void DefaultLayout_Set_To_A_Saved_Layout_Updates_The_Property()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Saved Dashboard", SummaryLayoutTree.CreateExample());
        appConfig.SaveLayout(layout);

        appConfig.DefaultLayout = layout;

        Assert.Equal(layout, appConfig.DefaultLayout);
    }

    // Regression test: SaveLayout replaces the list entry for a re-saved name, but the
    // default kept pointing at the replaced instance - so edits to the layout in use never
    // reached the SUMMARY screen (which rebuilds when the default instance changes).
    [Fact]
    public void Resaving_The_Default_Layout_Repoints_The_Default_At_The_New_Version()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout original = SummaryControlLayout.FromTree("My Dashboard", SummaryLayoutTree.CreateExample());
        appConfig.SaveLayout(original);
        appConfig.DefaultLayout = original;

        SummaryControlLayout edited = SummaryControlLayout.FromTree(original.Name, SummaryLayoutTree.CreateEmpty());
        appConfig.SaveLayout(edited);

        Assert.Same(edited, appConfig.DefaultLayout);
    }

    [Fact]
    public void SaveLayout_Refuses_A_Built_In_Layouts_Name()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout shipped = appConfig.Layouts.Single(l => l.Name == "All Charts");
        fileSystem.Invocations.Clear();

        bool saved = appConfig.SaveLayout(SummaryControlLayout.FromTree("all charts", SummaryLayoutTree.CreateEmpty()));

        Assert.False(saved);
        Assert.Same(shipped, appConfig.Layouts.Single(l => l.Name.Equals("All Charts", StringComparison.OrdinalIgnoreCase)));
        fileSystem.Verify(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("All Charts", true)]
    [InlineData("all charts", true)]
    [InlineData("My Dashboard", false)]
    public void IsBuiltInLayout_Matches_Only_Shipped_Layout_Names(string name, bool expected)
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        appConfig.SaveLayout(SummaryControlLayout.FromTree("My Dashboard", SummaryLayoutTree.CreateExample()));

        Assert.Equal(expected, appConfig.IsBuiltInLayout(name));
    }

    [Fact]
    public void Saving_A_Different_Layout_Leaves_The_Default_Alone()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout original = appConfig.DefaultLayout!;

        appConfig.SaveLayout(SummaryControlLayout.FromTree("Something Else", SummaryLayoutTree.CreateExample()));

        Assert.Same(original, appConfig.DefaultLayout);
    }
}
