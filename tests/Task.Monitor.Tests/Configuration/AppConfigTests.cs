using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Gui.Controls.Summary2;
namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigTests
{
    private readonly Mock<IFileSystem> fileSystem;
    private readonly string testConfigPath = "/dummy/path/taskmon.ini";
    
    public AppConfigTests() =>
        fileSystem = new Mock<IFileSystem>();

    [Fact]
    public void AppConfig_Canary_Test() =>
        Assert.Equal(35, CanaryTestHelper.GetPropertyCount<AppConfig>());

    [Fact]
    public void A_New_Config_Starts_With_Taskmon_Essentials_1_As_The_Default_Layout()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Contains("default-summary-layout=Taskmon Essentials 1", appConfig.ToString());
        Assert.Equal("Taskmon Essentials 1", appConfig.DefaultLayout?.Name);
    }

    private static string ConfigTemplate()
    {
        using Stream stream = typeof(AppConfig).Assembly.GetManifestResourceStream("Task.Monitor.Assets.Config.taskmon.ini")!;
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }

    [Fact]
    public void Constructor_With_FileSystem_Initialises_Successfully()
    {
        var appConfig = new AppConfig(fileSystem.Object);

        Assert.NotNull(appConfig);
        Assert.NotNull(appConfig.Themes);
    }

    [Fact]
    public void CreatePreview_Copies_Settings_And_Keeps_Theme_Changes_To_Itself()
    {
        AppConfig appConfig = new(fileSystem.Object) { MetreStyle = MetreControlStyle.Blocks };
        Theme original      = appConfig.Theme;
        Theme other         = appConfig.Themes.First(t => t.Name == "MS-DOS");
        bool preferIndexed  = ConsolePalette.PreferIndexedColours;

        AppConfig preview = appConfig.CreatePreview();

        try {
            preview.Theme = other;
        }
        finally {
            ConsolePalette.PreferIndexedColours = preferIndexed;
        }

        Assert.Equal(MetreControlStyle.Blocks, preview.MetreStyle);
        Assert.Same(other, preview.Theme);
        Assert.Same(original, appConfig.Theme);
        Assert.Contains($"default-theme={original.Name}", appConfig.ToString());
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
show-large-metre-grid=True
show-metre-cpu-numerically=False
show-metre-disk-numerically=False
show-metre-mem-numerically=False
show-metre-swap-numerically=False
show-metre-gpu-numerically=False
show-metre-npu-numerically=False
show-metre-gpu-mem-numerically=False
show-metre-network-numerically=False
show-small-metre-grid=True
show-tips=False
show-y-axis-scale=False
ui-language=it-IT
use-irix-cpu-reporting=False
";

    [Fact]
    public void DefaultConfigFilePath_ReturnsNonNullPath()
    {
        AppConfig appConfig = new(fileSystem.Object);

        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        
        string? result = appConfig.DefaultConfigFilePath;

        Assert.NotNull(result);
        Assert.Contains("taskmon.ini", result);
    }

    // Each missing folder is created on its own, from the top down, which is what lets every one
    // of them be handed to the user under sudo. Creating v2 in one go would leave the taskmon
    // folder above it owned by root.
    [Fact]
    public void DefaultConfigPath_Creates_Each_Missing_Folder_From_The_Top_Down()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        string configPath   = appConfig.DefaultConfigPath!;
        string appPath      = Path.GetDirectoryName(configPath)!;

        List<string> created = new();
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns((string? path) => path != appPath && path != configPath);
        fileSystem.Setup(fs => fs.TryCreateDirectory(It.IsAny<string>()))
            .Callback<string>(created.Add)
            .Returns(true);

        Assert.Equal(configPath, appConfig.DefaultConfigPath);
        Assert.Equal([appPath, configPath], created);
    }

    // Version 2's files live in a folder of their own, so a version 1 install alongside, or one
    // rolled back to, finds its files exactly as it left them.
    [Fact]
    public void DefaultConfigPath_Holds_Every_Config_File_Read_Or_Written()
    {
        string[] fileMethods = [
            nameof(IFileSystem.DeleteFile),
            nameof(IFileSystem.FileExists),
            nameof(IFileSystem.GetFiles),
            nameof(IFileSystem.ReadAllText),
            nameof(IFileSystem.ReplaceAllText),
            nameof(IFileSystem.WriteAllText),
            nameof(IFileSystem.WriteAllTextAtomic)
        ];

        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.SaveLayout(SummaryControlLayout.FromTree("Saved Dashboard", TestLayouts.Example())));
        Assert.True(appConfig.TrySave(appConfig.DefaultConfigFilePath!));

        string configPath = appConfig.DefaultConfigPath!;

        List<string> paths = fileSystem.Invocations
            .Where(invocation => fileMethods.Contains(invocation.Method.Name))
            .Select(invocation => (string)invocation.Arguments[0])
            .ToList();

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.StartsWith($"{configPath}{Path.DirectorySeparatorChar}", path));
    }

    [Fact]
    public void DefaultConfigPath_Is_A_Version_2_Folder_Inside_Version_1s()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        string? configPath = new AppConfig(fileSystem.Object).DefaultConfigPath;

        Assert.Equal("v2", Path.GetFileName(configPath));
        Assert.Equal("taskmon", Path.GetFileName(Path.GetDirectoryName(configPath)));
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

    [Fact]
    public void DefaultLayout_Falls_Back_To_Taskmon_Essentials_1_When_None_Is_Configured()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Equal("Taskmon Essentials 1", appConfig.DefaultLayout?.Name);
    }

    [Fact]
    public void DefaultLayout_Set_To_A_Saved_Layout_Updates_The_Property()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Saved Dashboard", TestLayouts.Example());
        appConfig.SaveLayout(layout);

        appConfig.DefaultLayout = layout;

        Assert.Equal(layout, appConfig.DefaultLayout);
    }

    [Fact]
    public void DefaultLayout_Set_To_Invalid_Layout_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout invalid = SummaryControlLayout.FromTree("Not Registered", TestLayouts.Example());

        Assert.Throws<InvalidOperationException>(() => appConfig.DefaultLayout = invalid);
    }

    [Fact]
    public void Default_Theme_After_Construction_Returns_Colour_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotNull(appConfig.Theme);
        Assert.Equal("Taskmon Default", appConfig.Theme.Name);
    }

    [Fact]
    public void Default_Theme_Set_To_Invalid_Theme_Throws_InvalidOperationException()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme invalidTheme = new(new ConfigSection("theme-invalid"));

        Assert.Throws<InvalidOperationException>(() => appConfig.Theme = invalidTheme);
    }    

    [Fact]
    public void Default_Theme_Set_To_Valid_Theme_Updates_Default_Theme()
    {
        AppConfig appConfig = new(fileSystem.Object);
        Theme theme = appConfig.Themes.First(t => t.Name == "MS-DOS");

        appConfig.Theme = theme;
        Assert.Equal(theme, appConfig.Theme);
    }

    public static TheoryData<string> IniFileData()
        => new()
        {
            DefaultIniFile,     // Defaults in the ini file mapping to defaults on the AppConfig property getters.
            string.Empty        // Empty file forces AppConfig to use defaults for all property getters.
        };

    private static bool IsBuiltInFolder(string path, string parentFolder) =>
        Path.GetFileName(path) == Constants.BuiltInDirectory &&
        Path.GetFileName(Path.GetDirectoryName(path)) == parentFolder;

    [Theory]
    [InlineData("Taskmon Essentials 1", true)]
    [InlineData("taskmon essentials 1", true)]
    [InlineData("My Dashboard", false)]
    public void IsBuiltInLayout_Matches_Only_Shipped_Layout_Names(string name, bool expected)
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        appConfig.SaveLayout(SummaryControlLayout.FromTree("My Dashboard", TestLayouts.Example()));

        Assert.Equal(expected, appConfig.IsBuiltInLayout(name));
    }

    private AppConfig LoadFromIni(string iniText)
    {
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(iniText);

        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TryLoad(Config.FromFile(fileSystem.Object, testConfigPath)));

        return appConfig;
    }

    // The built-in folder is taskmon's: a file there that isn't a built-in's copy, such as one for a
    // layout that no longer ships, is deleted. The match ignores case, as a built-in renamed only
    // by case keeps the old file name on a case-insensitive disk.
    [Fact]
    public void LoadLayouts_Deletes_Files_In_The_Built_In_Folder_That_Are_Not_Built_Ins()
    {
        const string RetiredFile   = "/fake/path/layouts/built-in/Retired Layout.layout";
        const string ShippedFile   = "/fake/path/layouts/built-in/taskmon essentials 1.layout";

        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.GetFiles(It.Is<string>(p => IsBuiltInFolder(p, "layouts")), It.IsAny<string>()))
            .Returns([ShippedFile, RetiredFile]);

        AppConfig appConfig = new(fileSystem.Object);

        fileSystem.Verify(fs => fs.DeleteFile(RetiredFile), Times.Once);
        fileSystem.Verify(fs => fs.DeleteFile(ShippedFile), Times.Never);
        Assert.DoesNotContain(appConfig.Layouts, l => l.Name == "Retired Layout");
    }

    [Fact]
    public void LoadLayouts_Deploys_A_Shipped_Layout_Whose_Copy_On_Disk_Has_Changed()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(It.IsAny<string>())).Returns("[Taskmon Essentials 1]\nstale=true\n");

        _ = new AppConfig(fileSystem.Object);

        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith(Path.Combine("layouts", "built-in", $"Taskmon Essentials 1{Constants.LayoutExtension}"))),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void LoadLayouts_Flags_Shipped_Layouts_As_Built_In_And_Custom_Ones_As_Not()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("layouts", ("My Dashboard.layout", TreeLayoutIni("My Dashboard")));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.True(appConfig.Layouts.Single(l => l.Name == "Taskmon Essentials 1").IsBuiltIn);
        Assert.False(appConfig.Layouts.Single(l => l.Name == "My Dashboard").IsBuiltIn);
    }

    [Fact]
    public void LoadLayouts_Loads_A_Custom_Tree_Layout_From_Disk()
    {
        string treeLayoutIni = @"
[My Dashboard]
version=2
root=0
nodes=0,1,2
node.0=split,row,0.5,1,2
node.1=pane,cpu
node.2=pane,process,process+pid+cpu+mem
";
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("layouts", ("My Dashboard.layout", treeLayoutIni));

        AppConfig appConfig = new(fileSystem.Object);

        LayoutTree<SummaryPane> tree = appConfig.Layouts.Single(t => t.Name == "My Dashboard").ToTree();
        SummaryPane processPane = tree.Panes().Single(p => p.Pane.ControlType == PaneControlType.Process).Pane;

        Assert.Equal(
            Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem,
            processPane.ProcessColumns);
    }

    // A layout file is loaded only at the version this build writes. One with no version, or
    // another version, isn't loaded as a tree with whatever it happens to contain.
    [Theory]
    [InlineData("")]
    [InlineData("version=1")]
    [InlineData("version=3")]
    [InlineData("version=2.0")]
    public void LoadLayouts_Skips_A_Layout_File_Not_At_Version_2(string versionLine)
    {
        string layoutIni = $"[My Layout]\n{versionLine}\nroot=0\nnodes=0\nnode.0=pane,cpu\n";

        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("layouts", ("My Layout.layout", layoutIni));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.DoesNotContain(appConfig.Layouts, t => t.Name == "My Layout");
    }

    // A version 1 fixed-grid layout file copied into the folder has no version, so it is skipped
    // rather than translated.
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
        SetupUserFiles("layouts", ("My Grid.layout", gridLayoutIni));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.DoesNotContain(appConfig.Layouts, t => t.Name == "My Grid");
        Assert.Contains(appConfig.Layouts, t => t.Name == "Taskmon Essentials 1");
    }

    // Built-in names are reserved: a file whose header names a built-in layout is skipped, whatever
    // the file is called, so the Layout Designer, which won't save under a built-in's name, and
    // the files on disk agree.
    [Theory]
    [InlineData("Taskmon Essentials 1.layout")]
    [InlineData("Renamed File.layout")]
    public void LoadLayouts_The_Shipped_Layout_Wins_Over_A_File_On_Disk_With_Its_Name(string fileName)
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("layouts", (fileName, TreeLayoutIni("Taskmon Essentials 1")));

        AppConfig appConfig = new(fileSystem.Object);

        SummaryControlLayout shipped = Assert.Single(appConfig.Layouts, l => l.Name == "Taskmon Essentials 1");
        Assert.True(shipped.IsBuiltIn);
        Assert.Equal(8, shipped.ToTree().Panes().Count());
    }

    [Fact]
    public void LoadThemes_Custom_Themes_On_Disk_Are_Loaded()
    {
        string customThemeIni = @"
[My Custom Theme]
version=2
colour-mode=truecolour
control.background=#123456
control.foreground=#abcdef
";
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("themes", ("My Custom Theme.theme", customThemeIni));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.Contains(appConfig.Themes, t => t.Name == "My Custom Theme");
        Theme customTheme = appConfig.Themes.First(t => t.Name == "My Custom Theme");
        Assert.Equal(ColorTranslator.FromHtml("#123456"), customTheme.Background);
    }

    // Each built-in theme's copy goes in the built-in folder, never the folder people's own themes
    // are in, so it can't overwrite one of theirs that has the same name.
    [Fact]
    public void LoadThemes_ManifestThemes_Write_To_Disk_On_Load()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotEmpty(appConfig.Themes);
        Assert.Contains(appConfig.Themes, t => t.Name == "Taskmon Default");
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith(Path.Combine("themes", "built-in", $"Taskmon Default{Constants.ThemeExtension}"))),
            It.IsAny<string>()), Times.Once);
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => Path.GetFileName(Path.GetDirectoryName(p)) == "themes"),
            It.IsAny<string>()), Times.Never);
    }

    // A theme is named by its [header], not its file name: that is the name Setup lists and the
    // default-theme setting stores.
    [Fact]
    public void LoadThemes_Names_A_Theme_By_Its_Header()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("themes", ("Old File Name.theme", "[Header Name]\nversion=2\ncontrol.background=#123456\n"));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.Contains(appConfig.Themes, t => t.Name == "Header Name");
        Assert.DoesNotContain(appConfig.Themes, t => t.Name == "Old File Name");
    }

    // A version 1 theme (no version, and the old key names) or a theme of another version is
    // skipped rather than loaded with fallback colours for every key it doesn't know.
    [Theory]
    [InlineData("background=#123456\nforeground=#abcdef\n")]
    [InlineData("version=1\ncontrol.background=#123456\n")]
    [InlineData("version=3\ncontrol.background=#123456\n")]
    public void LoadThemes_Skips_A_Theme_File_Not_At_Version_2(string keys)
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("themes", ("My Theme.theme", $"[My Theme]\n{keys}"));

        AppConfig appConfig = new(fileSystem.Object);

        Assert.DoesNotContain(appConfig.Themes, t => t.Name == "My Theme");
    }

    // Built-in names are reserved, ignoring case: a file whose header names a built-in theme is
    // skipped, so the built-in is what loads.
    [Fact]
    public void LoadThemes_Skips_A_Theme_Named_Like_A_Built_In()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        SetupUserFiles("themes", ("My Nord.theme", "[nord]\nversion=2\ncontrol.background=#123456\n"));

        AppConfig appConfig = new(fileSystem.Object);

        Theme nord = Assert.Single(appConfig.Themes, t => t.Name.Equals("Nord", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Nord", nord.Name);
        Assert.NotEqual(ColorTranslator.FromHtml("#123456"), nord.Background);
    }

    [Theory]
    [InlineData("Taskmon Essentials 1", Constants.LayoutExtension)]
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

    [Fact]
    public void New_Config_ToString_Contains_Expected_Sections()
    {
        AppConfig appConfig = new(fileSystem.Object);
        string iniConfig = appConfig.ToString();
        List<string> expectedSections = new() {
            "[filter]",
            "[health]",
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

    // Regression test: SaveLayout replaces the list entry for a re-saved name, but the
    // default kept pointing at the replaced instance - so edits to the layout in use never
    // reached the SUMMARY screen (which rebuilds when the default instance changes).
    [Fact]
    public void Resaving_The_Default_Layout_Repoints_The_Default_At_The_New_Version()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout original = SummaryControlLayout.FromTree("My Dashboard", TestLayouts.Example());
        appConfig.SaveLayout(original);
        appConfig.DefaultLayout = original;

        SummaryControlLayout edited = SummaryControlLayout.FromTree(original.Name, new LayoutTree<SummaryPane>(SummaryPane.Empty));
        appConfig.SaveLayout(edited);

        Assert.Same(edited, appConfig.DefaultLayout);
    }

    [Fact]
    public void SaveLayout_Refuses_A_Built_In_Layouts_Name()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout shipped = appConfig.Layouts.Single(l => l.Name == "Taskmon Essentials 1");
        fileSystem.Invocations.Clear();

        bool saved = appConfig.SaveLayout(SummaryControlLayout.FromTree("taskmon essentials 1", new LayoutTree<SummaryPane>(SummaryPane.Empty)));

        Assert.False(saved);
        Assert.Same(shipped, appConfig.Layouts.Single(l => l.Name.Equals("Taskmon Essentials 1", StringComparison.OrdinalIgnoreCase)));
        fileSystem.Verify(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SaveLayout_Writes_The_File_And_Adds_It_To_Layouts()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Saved Dashboard", TestLayouts.Example());

        bool result = appConfig.SaveLayout(layout);

        Assert.True(result);
        Assert.Contains(appConfig.Layouts, t => t.Name == "Saved Dashboard");
        fileSystem.Verify(fs => fs.WriteAllText(
            It.Is<string>(p => p.EndsWith($"Saved Dashboard{Constants.LayoutExtension}")),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Saving_A_Different_Layout_Leaves_The_Default_Alone()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        SummaryControlLayout original = appConfig.DefaultLayout!;

        appConfig.SaveLayout(SummaryControlLayout.FromTree("Something Else", TestLayouts.Example()));

        Assert.Same(original, appConfig.DefaultLayout);
    }

    private static Dictionary<string, string> Settings(string iniText) =>
        Config.FromString(iniText)!.ConfigSections
            .SelectMany(section => section.Entries.Select(entry => (Key: $"[{section.Name}] {entry.Key}", entry.Value)))
            .ToDictionary(setting => setting.Key, setting => setting.Value);

    // Puts files in the folder people keep their own themes or layouts in, leaving the built-in
    // folder inside it empty.
    private void SetupUserFiles(string folder, params (string FileName, string Text)[] files)
    {
        string[] paths = files.Select(file => $"/fake/path/{folder}/{file.FileName}").ToArray();

        fileSystem.Setup(fs => fs.GetFiles(It.Is<string>(p => Path.GetFileName(p) == folder), It.IsAny<string>()))
            .Returns(paths);

        foreach ((string path, string text) in paths.Zip(files.Select(file => file.Text))) {
            fileSystem.Setup(fs => fs.ReadAllText(path)).Returns(text);
        }
    }

    [Fact]
    public void Shipped_Layouts_Are_At_The_Current_Version()
    {
        Assembly asm = typeof(AppConfig).Assembly;
        string[] layoutNames = asm.GetManifestResourceNames()
            .Where(n => n.EndsWith(Constants.LayoutExtension))
            .ToArray();

        Assert.NotEmpty(layoutNames);

        foreach (string name in layoutNames) {
            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            ConfigParser parser = new(reader.ReadToEnd());
            parser.Parse();

            Assert.Equal(
                SummaryControlLayout.CurrentVersion.ToString(),
                parser.Sections[0].GetString(Constants.Keys.Version));
        }
    }

    // A control name taskmon doesn't know loads as an empty pane, and a macOS-only one shows as
    // empty on Windows, so either would leave a blank space in a shipped layout. Each also has one
    // process list, which names its own columns rather than taking the PROCESSES view's.
    [Fact]
    public void Shipped_Layouts_Name_A_Known_Control_In_Every_Pane()
    {
        // No fileSystem setup - the on-disk scan is skipped, leaving only the embedded layouts.
        AppConfig appConfig = new(fileSystem.Object);

        List<SummaryControlLayout> shipped = appConfig.Layouts.Where(l => l.IsBuiltIn).ToList();
        Assert.NotEmpty(shipped);

        foreach (SummaryControlLayout layout in shipped) {
            List<SummaryPane> panes = layout.ToTree().Panes().Select(p => p.Pane).ToList();

            Assert.True(panes.All(pane => pane.ControlType != PaneControlType.Empty), $"{layout.Name} has an empty pane");
            Assert.True(panes.All(pane => !SummaryControl2.UnavailableControlTypes.Contains(pane.ControlType)), $"{layout.Name} has a pane this platform can't show");
            Assert.NotNull(Assert.Single(panes, pane => pane.ControlType == PaneControlType.Process).ProcessColumns);
        }
    }

    // Each shipped layout's process list is sorted by what the layout is about, highest first: the
    // disk and GPU layouts by Disk and Gpu, the rest by Cpu. Network has no per-process column.
    // Taskmon Essentials 3 names no sort, so it gets SUMMARY's default.
    [Theory]
    [InlineData("Classic Monitoring 1", Statistics.Cpu)]
    [InlineData("Classic Monitoring 2", Statistics.Cpu)]
    [InlineData("Classic Monitoring 3", Statistics.Cpu)]
    [InlineData("Classic Monitoring 4", Statistics.Cpu)]
    [InlineData("Classic Monitoring 5", Statistics.Cpu)]
    [InlineData("CPU Essentials",       Statistics.Cpu)]
    [InlineData("Disk Essentials",      Statistics.Disk)]
    [InlineData("GPU Essentials",       Statistics.Gpu)]
    [InlineData("Network Essentials",   Statistics.Cpu)]
    [InlineData("Taskmon Essentials 1", Statistics.Cpu)]
    [InlineData("Taskmon Essentials 2", Statistics.Cpu)]
    [InlineData("Taskmon Essentials 3", Statistics.Cpu)]
    [InlineData("Taskmon Essentials 4", Statistics.Cpu)]
    public void Shipped_Layouts_Sort_Their_Process_List_By_What_They_Show(string name, Statistics sort)
    {
        AppConfig appConfig = new(fileSystem.Object);

        SummaryPane processPane = appConfig.Layouts.Single(l => l.Name == name).ToTree()
            .Panes().Single(p => p.Pane.ControlType == PaneControlType.Process).Pane;

        Assert.Equal(sort, processPane.SortColumn ?? SummaryControl2.DefaultProcessSort);
        Assert.False(processPane.SortAscending);
    }

    // A layout file in Assets\Layouts that isn't an EmbeddedResource in taskmon.csproj never
    // reaches the built-in folder. Setting the build action on several files at once in an IDE can
    // miss some of them, so the folder and the embedded resources must match.
    [Fact]
    public void Ships_Every_Layout_In_The_Assets_Folder()
    {
        const string ResourcePrefix = "Task.Monitor.Assets.Layouts.";

        string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Assets", "Layouts"), $"*{Constants.LayoutExtension}")
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] embedded = typeof(AppConfig).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix) && name.EndsWith(Constants.LayoutExtension))
            .Select(name => name[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        Assert.Equal(files, embedded);
    }

    [Theory]
    [MemberData(nameof(IniFileData))]
    public void Should_Load_AndOr_Parse_DefaultIniFile(string iniFileData)
    {
        Config? iniConfig = Config.FromString(iniFileData);

        Assert.NotNull(iniConfig);

        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TryLoad(iniConfig));
        
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
        Assert.Equal(Statistics.Cpu, appConfig.SortColumn);
        Assert.False(appConfig.SortAscending);
        Assert.True(appConfig.ShowMetreCpuNumerically);
        Assert.True(appConfig.ShowMetreGpuNumerically);
        Assert.True(appConfig.ShowMetreNpuNumerically);
        Assert.True(appConfig.ShowMetreDiskNumerically);
        Assert.True(appConfig.ShowMetreMemoryNumerically);
        Assert.True(appConfig.ShowMetreGpuMemNumerically);
        Assert.True(appConfig.ShowMetreSwapNumerically);
        Assert.True(appConfig.ShowMetreNetworkNumerically);
        Assert.True(appConfig.ShowTips);
        Assert.True(appConfig.ShowYAxisScale);
        Assert.False(appConfig.ShowLargeMetreGrid);
        Assert.False(appConfig.ShowSmallMetreGrid);
        Assert.Equal(string.Empty, appConfig.UiLanguage);

        if (!string.IsNullOrEmpty(iniFileData)) {
            Assert.True(appConfig.UseIrixReporting);
        }
    }

    [Fact]
    public void Should_Load_And_Parse_CustomIniFile()
    {
        Config? iniConfig = Config.FromString(CustomIniFile);
        
        Assert.NotNull(iniConfig);
        
        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TryLoad(iniConfig));
        
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        Assert.False(appConfig.ConfirmTaskDelete);
        Assert.NotNull(appConfig.DefaultConfigFilePath);
        Assert.NotEmpty(appConfig.DefaultConfigFilePath);
        
        Assert.NotNull(appConfig.Theme);
        Assert.Equal("MS-DOS", appConfig.Theme.Name);
        // The shipped MS-DOS theme: the 16-colour text-mode palette of Edit, QBasic and Turbo Vision.
        Assert.Equal(ColorTranslator.FromHtml("#0000aa"), appConfig.Theme.Background);
        Assert.Equal(ColorTranslator.FromHtml("#00aaaa"), appConfig.Theme.BackgroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#00aaaa"), appConfig.Theme.ColumnCommandLowPriority);
        Assert.Equal(ColorTranslator.FromHtml("#ff5555"), appConfig.Theme.ColumnCommandHighCpu);
        Assert.Equal(ColorTranslator.FromHtml("#ffff55"), appConfig.Theme.ColumnCommandIoBound);
        Assert.Equal(ColorTranslator.FromHtml("#aaaaaa"), appConfig.Theme.ColumnCommandNormalUserSpace);
        Assert.Equal(ColorTranslator.FromHtml("#55ff55"), appConfig.Theme.ColumnCommandScript);
        Assert.Equal(ColorTranslator.FromHtml("#55ff55"), appConfig.Theme.ColumnUserCurrentNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#55ffff"), appConfig.Theme.ColumnUserOtherNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#ff5555"), appConfig.Theme.ColumnUserRoot);
        Assert.Equal(ColorTranslator.FromHtml("#00aaaa"), appConfig.Theme.ColumnUserSystem);
        Assert.Equal(ColorTranslator.FromHtml("#aaaaaa"), appConfig.Theme.CommandBackground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.CommandForeground);
        Assert.Equal(ConsolePalette.White,                appConfig.Theme.DeltaHighlightColour);
        Assert.Equal(ColorTranslator.FromHtml("#ff5555"), appConfig.Theme.Error);
        Assert.Equal(ColorTranslator.FromHtml("#aaaaaa"), appConfig.Theme.Foreground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.ForegroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#0000aa"), appConfig.Theme.HeaderBackground);
        Assert.Equal(ColorTranslator.FromHtml("#ffff55"), appConfig.Theme.HeaderForeground);
        Assert.Equal(ColorTranslator.FromHtml("#aaaaaa"), appConfig.Theme.MenubarBackground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.MenubarForeground);
        Assert.Equal(ColorTranslator.FromHtml("#ff5555"), appConfig.Theme.RangeHighBackground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.RangeHighForeground);
        Assert.Equal(ColorTranslator.FromHtml("#55ff55"), appConfig.Theme.RangeLowBackground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.RangeLowForeground);
        Assert.Equal(ColorTranslator.FromHtml("#ffff55"), appConfig.Theme.RangeMidBackground);
        Assert.Equal(ConsolePalette.Black,                appConfig.Theme.RangeMidForeground);

        Assert.Equal(2000, appConfig.DelayInMilliseconds);
        Assert.Equal(123456, appConfig.FilterPid);
        Assert.Equal("root", appConfig.FilterUserName);
        Assert.Equal("kernel_task", appConfig.FilterProcess);
        Assert.False(appConfig.HighlightDaemons);
        Assert.False(appConfig.HighlightStatisticsColumnUpdate);
        Assert.Equal(MetreControlStyle.Bars, appConfig.MetreStyle);
        Assert.True(appConfig.MultiSelectProcesses);
        Assert.Equal(Statistics.Mem, appConfig.SortColumn);
        Assert.True(appConfig.SortAscending);
        Assert.False(appConfig.ShowMetreCpuNumerically);
        Assert.False(appConfig.ShowMetreDiskNumerically);
        Assert.False(appConfig.ShowMetreMemoryNumerically);
        Assert.False(appConfig.ShowMetreSwapNumerically);
        Assert.False(appConfig.ShowMetreGpuNumerically);
        Assert.False(appConfig.ShowMetreGpuMemNumerically);
        Assert.False(appConfig.ShowMetreNetworkNumerically);
        Assert.False(appConfig.ShowMetreNpuNumerically);
        Assert.True(appConfig.ShowLargeMetreGrid);
        Assert.True(appConfig.ShowSmallMetreGrid);
        Assert.False(appConfig.ShowTips);
        Assert.False(appConfig.ShowYAxisScale);
        Assert.Equal("it-IT", appConfig.UiLanguage);
        Assert.False(appConfig.UseIrixReporting);

        Statistics allButPower =
            Statistics.Process | Statistics.Pid     | Statistics.User   | Statistics.Pri    |
            Statistics.Cpu     | Statistics.Thrd    | Statistics.Gpu    | Statistics.Mem    |
            Statistics.Path    | Statistics.Disk    | Statistics.AvgCpu | Statistics.AvgGpu |
            Statistics.AvgMem  | Statistics.AvgDisk | Statistics.MaxCpu | Statistics.MaxGpu |
            Statistics.MaxMem  | Statistics.MaxDisk;

        Assert.Equal(allButPower, appConfig.VisibleColumns);
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
    public void Themes_After_Construction_Returns_Non_Empty_List()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.NotNull(appConfig.Themes);
        Assert.NotEmpty(appConfig.Themes);
    }

    [Fact]
    public void The_Default_Layout_Is_Read_From_The_Config()
    {
        AppConfig appConfig = LoadFromIni("[ux]\ndefault-summary-layout=Classic Monitoring 1\n");

        Assert.Equal("Classic Monitoring 1", appConfig.DefaultLayout?.Name);
    }

    // SUMMARY's default and the layouts --cpu-only and --gpu-only open are found by name, so a
    // built-in that is renamed or dropped would leave them pointing at nothing.
    [Theory]
    [InlineData(Constants.Sections.LayoutDefault)]
    [InlineData(Constants.Sections.LayoutCpuOnly)]
    [InlineData(Constants.Sections.LayoutGpuOnly)]
    public void The_Layouts_Named_In_Code_Are_Built_In(string name) =>
        Assert.True(new AppConfig(fileSystem.Object).IsBuiltInLayout(name));

    private static string TreeLayoutIni(string name) => $@"
[{name}]
version=2
root=0
nodes=0,1,2
node.0=split,row,0.5,1,2
node.1=pane,cpu
node.2=pane,memory
";

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
    
    // The theme and layout files are read once, when AppConfig is constructed. Reading them again
    // when the config loads would log every skipped file twice at start-up.
    [Fact]
    public void TryLoad_Does_Not_Read_The_Theme_And_Layout_Folders_Again()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        AppConfig appConfig = new(fileSystem.Object);
        fileSystem.Invocations.Clear();

        Assert.True(appConfig.TryLoad(Config.FromString("[ux]\ndefault-theme=Nord\ndefault-summary-layout=Classic Monitoring 1\n")!));

        fileSystem.Verify(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        fileSystem.Verify(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("Nord", appConfig.Theme.Name);
        Assert.Equal("Classic Monitoring 1", appConfig.DefaultLayout?.Name);
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
    public void TryLoad_With_Invalid_Path_Returns_False()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(false);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Throws(new FileNotFoundException());

        bool result = appConfig.TryLoad(testConfigPath);

        Assert.False(result);
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

    // A setting a newer version adds to an existing file arrives with the comment the template has
    // for it, while the settings already there are left as they were.
    [Fact]
    public void TrySave_Adds_A_Missing_Setting_With_Its_Template_Comment()
    {
        const string existing = "[ux]\nshow-tips=False\n";

        string? saved = null;
        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(existing);
        fileSystem.Setup(fs => fs.ReplaceAllText(testConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => saved = text);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.True(appConfig.TryLoad(testConfigPath));
        Assert.True(appConfig.TrySave(testConfigPath));

        Assert.StartsWith(existing, saved);
        Assert.Contains("\n\n; How metres are drawn: Blocks, Bars or Dots.\nmetre-style=Dots\n", saved);
    }

    // A missing file is created from the commented template, with every setting at its current
    // value: a value that differs from the template's, such as a platform's default, is written
    // in beside its comment.
    [Fact]
    public void TrySave_Creates_A_Missing_File_From_The_Template()
    {
        string? saved = null;
        fileSystem.Setup(fs => fs.WriteAllText(testConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => saved = text);

        AppConfig appConfig = new(fileSystem.Object) {
            MetreStyle       = MetreControlStyle.Blocks,
            UseIrixReporting = true
        };

        Assert.True(appConfig.TrySave(testConfigPath));
        Assert.NotNull(saved);

        string nl = Environment.NewLine;
        Assert.StartsWith(";============", saved);
        Assert.Contains($"; How metres are drawn: Blocks, Bars or Dots.{nl}metre-style=Blocks{nl}", saved);
        Assert.Contains($"Activity Monitor shows it.{nl}use-irix-cpu-reporting=True{nl}", saved);
        Assert.Equal(Settings(appConfig.ToString()), Settings(saved));
    }

    // A later save from Setup changes only the setting changed, so the template's comments stay.
    [Fact]
    public void TrySave_Keeps_The_Template_Comments_When_Saving_Again()
    {
        string? created = null;
        string? resaved = null;
        fileSystem.Setup(fs => fs.WriteAllText(testConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => created = text);

        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TrySave(testConfigPath));

        fileSystem.Setup(fs => fs.FileExists(testConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(testConfigPath)).Returns(created!);
        fileSystem.Setup(fs => fs.ReplaceAllText(testConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => resaved = text);

        appConfig.ShowTips = false;
        Assert.True(appConfig.TrySave(testConfigPath));

        Assert.Equal(created!.Replace("show-tips=True", "show-tips=False"), resaved);
    }

    // Every setting the code creates is in the template, so none is written without its comment,
    // and the template has no setting the code doesn't know.
    [Fact]
    public void TrySave_Template_Has_Every_Setting_And_No_Others()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Equal(
            Settings(appConfig.ToString()).Keys.Order(),
            Settings(ConfigTemplate()).Keys.Order());
    }

    // The template documents the defaults, so each value it shows is the default, except one
    // that depends on the platform, which a new file is given anyway.
    [Fact]
    public void TrySave_Template_Values_Are_The_Defaults()
    {
        Dictionary<string, string> defaults = Settings(new AppConfig(fileSystem.Object).ToString());
        Dictionary<string, string> template = Settings(ConfigTemplate());

        defaults.Remove("[ux] use-irix-cpu-reporting");
        template.Remove("[ux] use-irix-cpu-reporting");

        Assert.Equal(defaults, template);
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
    public void TrySave_With_Valid_Path_Returns_True()
    {
        AppConfig appConfig = new(fileSystem.Object);
        
        fileSystem.Setup(fs => fs.WriteAllText(testConfigPath, It.IsAny<string>()));

        bool result = appConfig.TrySave(testConfigPath);

        Assert.True(result);
    }

    // Version 2 never reads version 1's taskmon.ini, but one copied in by hand keeps its version 1
    // keys untouched, and they are not added to a config that doesn't have them.
    [Fact]
    public void Version_1_Layout_Keys_Are_Left_Alone_But_Not_Added()
    {
        AppConfig existing = LoadFromIni("[ux]\ndefault-layout=My Grid\nuse-large-charts=True\n");

        Assert.Contains("default-layout=My Grid", existing.ToString());
        Assert.Contains("use-large-charts=True", existing.ToString());
        Assert.Equal("Taskmon Essentials 1", existing.DefaultLayout?.Name);

        AppConfig fresh = new(fileSystem.Object);

        Assert.DoesNotContain("default-layout=", fresh.ToString());
        Assert.DoesNotContain("use-large-charts", fresh.ToString());
    }
}
