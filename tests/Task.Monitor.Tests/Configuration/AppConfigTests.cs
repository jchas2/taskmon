using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Process;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigTests
{
    private readonly Mock<IFileSystem> fileSystem;
    private readonly string testConfigPath = "/test/path/taskmon.ini";
    
    private static string[] PredefinedThemes = {
        Constants.Sections.ThemeColour,
        Constants.Sections.ThemeMono,
        Constants.Sections.ThemeMatrix,
        Constants.Sections.ThemeTokyoNight,
        Constants.Sections.ThemeMsDos,
        Constants.Sections.ThemeSolar
    };

    private static int ThemeCount = PredefinedThemes.Length;
    
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
cols=Process, Pid, User, Pri, Cpu, Thrd, Mem, Path, Disk
delay=1500
nprocs=-1

[ux]
confirm-task-delete=True
default-theme=theme-colour
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

[theme-colour]
background=black
background-highlight=cyan
col-cmd-normal-user-space=green
col-cmd-low-priority=blue
col-cmd-high-cpu=red
col-cmd-io-bound=cyan
col-cmd-script=yellow
col-user-current-non-root=green
col-user-other-non-root=magenta
col-user-system=gray
col-user-root=white
command-foreground=black
command-background=cyan
error=red
foreground=white
foreground-highlight=black
menubar-foreground=white
menubar-background=darkblue
range-high-background=red
range-low-background=green
range-mid-background=yellow
range-high-foreground=white
range-low-foreground=white
range-mid-foreground=darkyellow
header-background=darkgreen
header-foreground=black
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

        Assert.True(appConfig.ConfirmTaskDelete);
        Assert.NotNull(appConfig.DefaultConfigPath);
        Assert.NotEmpty(appConfig.DefaultConfigPath);

        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("theme-colour", appConfig.DefaultTheme.Name);
        Assert.Equal(ConsoleColor.Black, appConfig.DefaultTheme.Background);
        Assert.Equal(ConsoleColor.Cyan, appConfig.DefaultTheme.BackgroundHighlight);
        Assert.Equal(ConsoleColor.Blue, appConfig.DefaultTheme.ColumnCommandLowPriority);
        Assert.Equal(ConsoleColor.Red, appConfig.DefaultTheme.ColumnCommandHighCpu);
        Assert.Equal(ConsoleColor.Cyan, appConfig.DefaultTheme.ColumnCommandIoBound);
        Assert.Equal(ConsoleColor.Green, appConfig.DefaultTheme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsoleColor.Yellow, appConfig.DefaultTheme.ColumnCommandScript);
        Assert.Equal(ConsoleColor.Green, appConfig.DefaultTheme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsoleColor.Magenta, appConfig.DefaultTheme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsoleColor.White, appConfig.DefaultTheme.ColumnUserRoot);
        Assert.Equal(ConsoleColor.Gray, appConfig.DefaultTheme.ColumnUserSystem);
        Assert.Equal(ConsoleColor.Cyan, appConfig.DefaultTheme.CommandBackground);
        Assert.Equal(ConsoleColor.Black, appConfig.DefaultTheme.CommandForeground);
        Assert.Equal(ConsoleColor.Red, appConfig.DefaultTheme.Error);
        Assert.Equal(ConsoleColor.White, appConfig.DefaultTheme.Foreground);
        Assert.Equal(ConsoleColor.Black, appConfig.DefaultTheme.ForegroundHighlight);
        Assert.Equal(ConsoleColor.DarkGreen, appConfig.DefaultTheme.HeaderBackground);
        Assert.Equal(ConsoleColor.Black, appConfig.DefaultTheme.HeaderForeground);
        Assert.Equal(ConsoleColor.DarkBlue, appConfig.DefaultTheme.MenubarBackground);
        Assert.Equal(ConsoleColor.White, appConfig.DefaultTheme.MenubarForeground);
        Assert.Equal(ConsoleColor.Red, appConfig.DefaultTheme.RangeHighBackground);
        Assert.Equal(ConsoleColor.White, appConfig.DefaultTheme.RangeHighForeground);
        Assert.Equal(ConsoleColor.Green, appConfig.DefaultTheme.RangeLowBackground);
        Assert.Equal(ConsoleColor.White, appConfig.DefaultTheme.RangeLowForeground);
        Assert.Equal(ConsoleColor.Yellow, appConfig.DefaultTheme.RangeMidBackground);
        Assert.Equal(ConsoleColor.DarkYellow, appConfig.DefaultTheme.RangeMidForeground);

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
cols=Process, Pid, User, Pri, Cpu, Thrd, Mem, Path, Disk
delay=2000
nprocs=5

[ux]
confirm-task-delete=False
default-theme=theme-custom
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

[theme-custom]
background=blue
background-highlight=cyan
col-cmd-normal-user-space=darkgreen
col-cmd-low-priority=darkblue
col-cmd-high-cpu=darkred
col-cmd-io-bound=darkcyan
col-cmd-script=darkyellow
col-user-current-non-root=darkgreen
col-user-other-non-root=red
col-user-system=white
col-user-root=gray
command-foreground=white
command-background=yellow
error=magenta
foreground=yellow
foreground-highlight=yellow
menubar-foreground=green
menubar-background=blue
range-high-background=magenta
range-low-background=green
range-mid-background=yellow
range-high-foreground=cyan
range-low-foreground=gray
range-mid-foreground=gray
header-background=green
header-foreground=darkgray
";
    
    [Fact]
    public void Should_Load_And_Parse_CustomIniFile()
    {
        Config? iniConfig = Config.FromString(CustomIniFile);
        
        Assert.NotNull(iniConfig);
        
        AppConfig appConfig = new(fileSystem.Object, iniConfig);

        Assert.False(appConfig.ConfirmTaskDelete);
        Assert.NotNull(appConfig.DefaultConfigPath);
        Assert.NotEmpty(appConfig.DefaultConfigPath);
        
        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("theme-custom", appConfig.DefaultTheme.Name);
        Assert.Equal(ConsoleColor.Blue,       appConfig.DefaultTheme.Background);
        Assert.Equal(ConsoleColor.Cyan,       appConfig.DefaultTheme.BackgroundHighlight);
        Assert.Equal(ConsoleColor.DarkBlue,   appConfig.DefaultTheme.ColumnCommandLowPriority);
        Assert.Equal(ConsoleColor.DarkRed,    appConfig.DefaultTheme.ColumnCommandHighCpu);
        Assert.Equal(ConsoleColor.DarkCyan,   appConfig.DefaultTheme.ColumnCommandIoBound);
        Assert.Equal(ConsoleColor.DarkGreen,  appConfig.DefaultTheme.ColumnCommandNormalUserSpace);
        Assert.Equal(ConsoleColor.DarkYellow, appConfig.DefaultTheme.ColumnCommandScript);
        Assert.Equal(ConsoleColor.DarkGreen,  appConfig.DefaultTheme.ColumnUserCurrentNonRoot);
        Assert.Equal(ConsoleColor.Red,        appConfig.DefaultTheme.ColumnUserOtherNonRoot);
        Assert.Equal(ConsoleColor.Gray,       appConfig.DefaultTheme.ColumnUserRoot);
        Assert.Equal(ConsoleColor.White,      appConfig.DefaultTheme.ColumnUserSystem);
        Assert.Equal(ConsoleColor.Yellow,     appConfig.DefaultTheme.CommandBackground);
        Assert.Equal(ConsoleColor.White,      appConfig.DefaultTheme.CommandForeground);
        Assert.Equal(ConsoleColor.Magenta,    appConfig.DefaultTheme.Error);
        Assert.Equal(ConsoleColor.Yellow,     appConfig.DefaultTheme.Foreground);
        Assert.Equal(ConsoleColor.Yellow,     appConfig.DefaultTheme.ForegroundHighlight);
        Assert.Equal(ConsoleColor.Green,      appConfig.DefaultTheme.HeaderBackground);
        Assert.Equal(ConsoleColor.DarkGray,   appConfig.DefaultTheme.HeaderForeground);
        Assert.Equal(ConsoleColor.Blue,       appConfig.DefaultTheme.MenubarBackground);
        Assert.Equal(ConsoleColor.Green,      appConfig.DefaultTheme.MenubarForeground);
        Assert.Equal(ConsoleColor.Magenta,    appConfig.DefaultTheme.RangeHighBackground);
        Assert.Equal(ConsoleColor.Cyan,       appConfig.DefaultTheme.RangeHighForeground);
        Assert.Equal(ConsoleColor.Green,      appConfig.DefaultTheme.RangeLowBackground);
        Assert.Equal(ConsoleColor.Gray,       appConfig.DefaultTheme.RangeLowForeground);
        Assert.Equal(ConsoleColor.Yellow,     appConfig.DefaultTheme.RangeMidBackground);
        Assert.Equal(ConsoleColor.Gray,       appConfig.DefaultTheme.RangeMidForeground);

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

        foreach (string themeName in PredefinedThemes) {
            Assert.Contains(themeName, themeNames);
        }
        
        Assert.Equal(ThemeCount, appConfig.Themes.Count);        
    }

    [Fact]
    public void Default_Theme_After_Construction_Returns_Colour_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal("theme-colour", appConfig.DefaultTheme.Name.ToLower());
    }

    [Fact]
    public void Default_Theme_Set_To_Valid_Theme_Updates_Default_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme monoTheme = appConfig.Themes.First(t => t.Name.ToLower() == "theme-mono");

        appConfig.DefaultTheme = monoTheme;
        Assert.Equal(monoTheme, appConfig.DefaultTheme);
    }

    [Fact]
    public void Default_Theme_Set_To_Invalid_Theme_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme invalidTheme = new(new ConfigSection("theme-invalid"));

        Assert.Throws<InvalidOperationException>(() => appConfig.DefaultTheme = invalidTheme);
    }    
    
    public static TheoryData<string> ThemeNameData()
        => new() 
        {
            Constants.Sections.ThemeColour,
            Constants.Sections.ThemeMono,
            Constants.Sections.ThemeMatrix,
            Constants.Sections.ThemeTokyoNight,
            Constants.Sections.ThemeMsDos
        };

    [Theory]
    [MemberData(nameof(ThemeNameData))]
    public void Should_Load_Valid_UxTheme_From_Name_Without_Theme_Section_Defined(string themeName)
    {
        string iniString = $"[ux]\ndefault-theme={themeName}\n";
        Config? iniConfig = Config.FromString(iniString);
        
        Assert.NotNull(iniConfig);
        
        AppConfig appConfig = new(fileSystem.Object, iniConfig);

        Assert.NotNull(appConfig.DefaultTheme);
        Assert.Equal(themeName, appConfig.DefaultTheme.Name);
    }
    
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
        
        fileSystem.Setup(fs => fs.Exists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(DefaultIniFile);

        bool result = appConfig.TryLoad(testConfigPath);

        Assert.True(result);
    }

    [Fact]
    public void TryLoad_With_Invalid_Path_Returns_False()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.Exists(testConfigPath)).Returns(false);
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
    public void DefaultConfigPath_ReturnsNonNullPath()
    {
        AppConfig appConfig = new(fileSystem.Object);
        string? result = appConfig.DefaultConfigPath;

        Assert.NotNull(result);
        Assert.Contains("taskmon.ini", result);
    }
}
