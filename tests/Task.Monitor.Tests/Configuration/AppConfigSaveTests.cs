using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;

namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigSaveTests
{
    private const string CommentedIni =
        "; my settings\n" +
        "[ux]\n" +
        "; too busy\n" +
        "show-tips=False\n";

    private const string ConfigPath = "/dummy/path/taskmon.ini";

    private readonly Mock<IFileSystem> fileSystem = new();

    private string? savedText;
    private string  fileText = string.Empty;

    public AppConfigSaveTests()
    {
        fileSystem.Setup(fs => fs.FileExists(ConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(ConfigPath)).Returns(() => fileText);
        fileSystem.Setup(fs => fs.ReplaceAllText(ConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => savedText = fileText = text!);
    }

    [Fact]
    public void An_Edit_Made_While_Running_Survives_A_Save()
    {
        AppConfig appConfig = Load("[stats]\ndelay=1000\n");
        Assert.True(appConfig.TrySave(ConfigPath));

        fileText = fileText.Replace("delay=1000", "delay=500 ; faster");
        appConfig.ShowTips = false;

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("delay=500 ; faster\n", savedText);
    }

    [Fact]
    public void A_Change_Keeps_Comments_And_Other_Lines()
    {
        AppConfig appConfig = Load(CommentedIni);
        Assert.True(appConfig.TrySave(ConfigPath));

        appConfig.ShowTips = true;

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("; my settings\n[ux]\n; too busy\nshow-tips=True\n", savedText);
    }

    [Fact]
    public void A_File_That_Failed_To_Load_Is_Left_Alone()
    {
        fileText = "[ux]\nshow-tips\n";

        AppConfig appConfig = new(fileSystem.Object);

        Assert.False(appConfig.TryLoad(ConfigPath));
        Assert.False(appConfig.TrySave(ConfigPath));
        Assert.Null(savedText);
        fileSystem.Verify(fs => fs.WriteAllText(ConfigPath, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void A_File_That_Stops_Parsing_While_Running_Is_Left_Alone()
    {
        AppConfig appConfig = Load(CommentedIni);

        fileText = "[ux]\nshow-tips\n";
        appConfig.ShowTips = true;

        Assert.False(appConfig.TrySave(ConfigPath));
        Assert.Contains("Line 2:", appConfig.LastSaveError);
        Assert.Null(savedText);
    }

    [Fact]
    public void A_Save_After_The_File_Is_Fixed_Writes_Only_What_Changed()
    {
        fileText = "[ux]\nshow-tips\n";

        AppConfig appConfig = new(fileSystem.Object);
        Assert.False(appConfig.TryLoad(ConfigPath));

        fileText = "[ux]\n; fixed\nshow-tips=False\n[stats]\ndelay=500\n";
        appConfig.ConfirmTaskDelete = false;

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("; fixed\nshow-tips=False\n", savedText);
        Assert.Contains("delay=500\n", savedText);
        Assert.Contains("confirm-task-delete=False\n", savedText);
    }

    [Fact]
    public void A_Setter_Given_The_Same_Value_Does_Not_Touch_The_File()
    {
        AppConfig appConfig = Load(CommentedIni);
        Assert.True(appConfig.TrySave(ConfigPath));
        savedText = null;

        appConfig.ShowTips = false;

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Null(savedText);
    }

    [Fact]
    public void A_Theme_Name_In_The_Wrong_Case_Still_Loads()
    {
        AppConfig appConfig = Load("[ux]\ndefault-theme=nord\n");

        Assert.Equal("Nord", appConfig.Theme.Name);
        Assert.NotNull(appConfig.DefaultLayout);
    }

    [Fact]
    public void A_Theme_That_Does_Not_Exist_Stays_In_The_File()
    {
        AppConfig appConfig = Load("[ux]\ndefault-theme=Not Installed\n");

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("default-theme=Not Installed\n", savedText);
    }

    [Fact]
    public void A_Version_1_File_Only_Gains_Keys()
    {
        AppConfig appConfig = Load("[stats]\ndelay=1000\n\n[ux]\nconfirm-task-delete=False\nold-v1-key=1\n");

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("[stats]\ndelay=1000\n", savedText);
        Assert.Contains("confirm-task-delete=False\nold-v1-key=1\n", savedText);
        Assert.Contains("show-tips=True\n", savedText);
    }

    [Fact]
    public void Every_Shipped_Theme_And_Layout_Still_Parses()
    {
        AppConfig appConfig = new(new Mock<IFileSystem>().Object);

        Assert.Equal(18, appConfig.Themes.Count);
        Assert.Contains(appConfig.Layouts, layout => layout.Name == "All Charts");
    }

    private AppConfig Load(string text)
    {
        fileText = text;

        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TryLoad(ConfigPath));

        return appConfig;
    }

    [Fact]
    public void Nothing_To_Add_Does_Not_Touch_The_File()
    {
        AppConfig appConfig = Load(CommentedIni);
        Assert.True(appConfig.TrySave(ConfigPath));
        savedText = null;

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Null(savedText);
    }

    [Fact]
    public void The_First_Save_Writes_A_New_File()
    {
        string? written = null;

        fileSystem.Setup(fs => fs.FileExists(ConfigPath)).Returns(false);
        fileSystem.Setup(fs => fs.WriteAllText(ConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => written = text);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("[ux]", written);
        Assert.Contains("[stats]", written);
        fileSystem.Verify(fs => fs.ReplaceAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
