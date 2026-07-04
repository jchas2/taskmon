using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Process;
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
    
    internal static string DefaultIniFile => @"
[filter]
pid=-1
username=
process=

[iterations]
limit=0

[sort]
col=Cpu
asc=False

[stats]
cols=Process, Pid, User, Pri, Cpu, Thrd, Gpu, Mem, Path, Disk
delay=1500
nprocs=-1

[ux]
confirm-task-delete=True
default-theme=Classic HTop
highlight-daemons=True
highlight-stats-col-update=True
metre-style=Dots
multi-select-procs=False
show-metre-cpu-numerically=True
show-metre-disk-numerically=True
show-metre-mem-numerically=True
show-metre-swap-numerically=True
use-large-charts=False
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

        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("Classic HTop", appConfig.DefaultTheme.Name);
        //Assert.Equal(ConsolePalette.Transparent, appConfig.DefaultTheme.Background);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.DefaultTheme.BackgroundHighlight);
        Assert.Equal(ConsolePalette.Blue,       appConfig.DefaultTheme.ColumnCommandLowPriority);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.ColumnCommandHighCpu);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.DefaultTheme.ColumnCommandIoBound);
        Assert.Equal(ConsolePalette.Green,      appConfig.DefaultTheme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.ColumnCommandScript);
        Assert.Equal(ConsolePalette.Green,      appConfig.DefaultTheme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsolePalette.Magenta,    appConfig.DefaultTheme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsolePalette.White,      appConfig.DefaultTheme.ColumnUserRoot);
        Assert.Equal(ConsolePalette.Gray,       appConfig.DefaultTheme.ColumnUserSystem);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.DefaultTheme.CommandBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.DefaultTheme.CommandForeground);
        Assert.Equal(ConsolePalette.DarkYellow, appConfig.DefaultTheme.DeltaHighlightColour);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.Error);
        Assert.Equal(ConsolePalette.White,      appConfig.DefaultTheme.Foreground);
        Assert.Equal(ConsolePalette.Black,      appConfig.DefaultTheme.ForegroundHighlight);
        Assert.Equal(ConsolePalette.DarkGreen,  appConfig.DefaultTheme.HeaderBackground);
        Assert.Equal(ConsolePalette.Black,      appConfig.DefaultTheme.HeaderForeground);
        Assert.Equal(ConsolePalette.DarkBlue,   appConfig.DefaultTheme.MenubarBackground);
        Assert.Equal(ConsolePalette.White,      appConfig.DefaultTheme.MenubarForeground);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.RangeHighBackground);
        Assert.Equal(ConsolePalette.White,      appConfig.DefaultTheme.RangeHighForeground);
        Assert.Equal(ConsolePalette.Green,      appConfig.DefaultTheme.RangeLowBackground);
        Assert.Equal(ConsolePalette.White,      appConfig.DefaultTheme.RangeLowForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.RangeMidBackground);
        Assert.Equal(ConsolePalette.DarkYellow, appConfig.DefaultTheme.RangeMidForeground);

        Assert.Equal(Processor.DefaultDelayInMilliseconds, appConfig.DelayInMilliseconds);
        Assert.Equal(-1, appConfig.FilterPid);
        Assert.Equal(string.Empty, appConfig.FilterUserName);
        Assert.Equal(string.Empty, appConfig.FilterProcess);
        Assert.True(appConfig.HighlightDaemons);
        Assert.Equal(MetreControlStyle.Dots, appConfig.MetreStyle);
        Assert.False(appConfig.MultiSelectProcesses);
        Assert.Equal(-1, appConfig.NumberOfProcesses);
        Assert.Equal(Statistics.Cpu, appConfig.SortColumn);
        Assert.False(appConfig.SortAscending);
        Assert.Equal(0, appConfig.IterationLimit);
        Assert.True(appConfig.ShowMetreCpuNumerically);
        Assert.True(appConfig.ShowMetreGpuNumerically);
        Assert.True(appConfig.ShowMetreDiskNumerically);
        Assert.True(appConfig.ShowMetreMemoryNumerically);
        Assert.True(appConfig.ShowMetreGpuMemNumerically);
        Assert.True(appConfig.ShowMetreSwapNumerically);
        Assert.True(appConfig.ShowMetreNetworkNumerically);
        Assert.False(appConfig.UseLargeCharts);

        if (!string.IsNullOrEmpty(iniFileData)) {
            Assert.True(appConfig.UseIrixReporting);
        }
    }
    
    internal static string CustomIniFile => @"
[filter]
pid=123456
username=root
process=kernel_task

[iterations]
limit=10

[sort]
col=Mem
asc=True

[stats]
cols=Process, Pid, User, Pri, Cpu, Thrd, Gpu, Mem, Path, Disk, AvgCpu, AvgGpu, AvgMem, AvgDisk, MaxCpu, MaxGpu, MaxMem, MaxDisk
delay=2000
nprocs=5

[ux]
confirm-task-delete=False
default-theme=MSDOS
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
use-large-charts=True
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
        
        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("MSDOS", appConfig.DefaultTheme.Name);
        Assert.Equal(ConsolePalette.DarkBlue,   appConfig.DefaultTheme.Background);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.DefaultTheme.BackgroundHighlight);
        Assert.Equal(ConsolePalette.Gray,       appConfig.DefaultTheme.ColumnCommandLowPriority);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.ColumnCommandHighCpu);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.ColumnCommandIoBound);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.ColumnCommandScript);
        Assert.Equal(ConsolePalette.Gray,       appConfig.DefaultTheme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsolePalette.DarkGray,   appConfig.DefaultTheme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.ColumnUserRoot);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.ColumnUserSystem);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.DefaultTheme.CommandBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.CommandForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.DeltaHighlightColour);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.Error);
        Assert.Equal(ConsolePalette.DarkGray,   appConfig.DefaultTheme.Foreground);
        Assert.Equal(ConsolePalette.Black,      appConfig.DefaultTheme.ForegroundHighlight);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.DefaultTheme.HeaderBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.HeaderForeground);
        Assert.Equal(ConsolePalette.DarkCyan,   appConfig.DefaultTheme.MenubarBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.MenubarForeground);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.RangeHighBackground);
        Assert.Equal(ConsolePalette.Red,        appConfig.DefaultTheme.RangeHighForeground);
        Assert.Equal(ConsolePalette.Green,      appConfig.DefaultTheme.RangeLowBackground);
        Assert.Equal(ConsolePalette.Cyan,       appConfig.DefaultTheme.RangeLowForeground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.RangeMidBackground);
        Assert.Equal(ConsolePalette.Yellow,     appConfig.DefaultTheme.RangeMidForeground);

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
        Assert.Equal(10, appConfig.IterationLimit);
        Assert.False(appConfig.ShowMetreCpuNumerically);
        Assert.False(appConfig.ShowMetreDiskNumerically);
        Assert.False(appConfig.ShowMetreMemoryNumerically);
        Assert.False(appConfig.ShowMetreSwapNumerically);
        Assert.False(appConfig.ShowMetreGpuNumerically);
        Assert.False(appConfig.ShowMetreGpuMemNumerically);
        Assert.False(appConfig.ShowMetreNetworkNumerically);
        Assert.True(appConfig.UseLargeCharts);
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

        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("Classic HTop", appConfig.DefaultTheme.Name);
    }

    [Fact]
    public void Default_Theme_Set_To_Valid_Theme_Updates_Default_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme theme = appConfig.Themes.First(t => t.Name == "MSDOS");

        appConfig.DefaultTheme = theme;
        Assert.Equal(theme, appConfig.DefaultTheme);
    }

    [Fact]
    public void Default_Theme_Set_To_Invalid_Theme_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme invalidTheme = new(new ConfigSection("theme-invalid"));

        Assert.Throws<InvalidOperationException>(() => appConfig.DefaultTheme = invalidTheme);
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
}
