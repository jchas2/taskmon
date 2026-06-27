using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.Tests.Common;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Gui;

public sealed class SetupScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public SetupScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void SetupScreen_Canary_Test() =>
        Assert.Equal(12, CanaryTestHelper.GetPropertyCount<SetupScreen>());

    [Fact]
    public void Constructor_With_Valid_Run_Context_Initialises_Successfully()
    {
        SetupScreen setupScreen = new(runContext);

        Assert.NotNull(setupScreen);
    }

    [Fact]
    public void Constructor_With_Null_RunContext_Throws_NullReferenceException() =>
        Assert.Throws<NullReferenceException>(() => new SetupScreen(null!));

    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        SetupScreen setupScreen = new(runContext);

        Assert.Equal(ConsolePalette.Black, setupScreen.BackgroundColour);
        Assert.NotEmpty(setupScreen.Controls);
        Assert.True(setupScreen.CursorVisible);
        Assert.Equal(ConsolePalette.White, setupScreen.ForegroundColour);
        Assert.Equal(0, setupScreen.Height);
        Assert.NotNull(setupScreen.Name);
        Assert.Empty(setupScreen.Name);
        Assert.True(0 == setupScreen.TabIndex);
        Assert.False(setupScreen.TabStop);
        Assert.True(setupScreen.Visible);
        Assert.Equal(0, setupScreen.Width);
        Assert.Equal(0, setupScreen.X);
        Assert.Equal(0, setupScreen.Y);
    }

    [Fact]
    public void Load_Calls_OnLoad_Sets_CursorVisible_False()
    {
        SetupScreen setupScreen = new(runContext);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = false).Verifiable();

        setupScreen.Load();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = false, Times.AtLeastOnce);
        
        setupScreen.Unload();
    }

    [Fact]
    public void Unload_Calls_OnUnload_Sets_CursorVisible_True()
    {
        SetupScreen setupScreen = new(runContext);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = true).Verifiable();

        setupScreen.Unload();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = true, Times.Once);
    }

    [Fact]
    public void Load_Initialises_Header_Table()
    {
        string header = "Changes are saved to the following config file:";
        SetupScreen setupScreen = new(runContext);
        setupScreen.Load();

        Assert.NotNull(setupScreen.Controls);
        Assert.NotEmpty(setupScreen.Controls);

        ListView headerView = setupScreen.Controls
            .OfType<ListView>()
            .Single(c => c.Name == nameof(headerView));
        
        Assert.True(headerView.Items[0].Text == header);
        
        setupScreen.Unload();
    }

    public static TheoryData<string, string> ControlSettingData()
        => new() {
            { "GENERAL",                                           "menuView" },
            { "COLUMNS",                                           "menuView" },
            { "THEMES",                                            "menuView" },
            { "METRES",                                            "menuView" },
            { "DELAY",                                             "menuView" },
            { "LIMIT",                                             "menuView" },
            { "PROCESSES",                                         "menuView" },

            { "CPU %",                                             "columnsView" },
            { "Average CPU %",                                     "columnsView" },
            { "Max CPU %",                                         "columnsView" },
            { "Average GPU %",                                     "columnsView" },
            { "Max GPU %",                                         "columnsView" },
            { "Average Memory",                                    "columnsView" },
            { "Max Memory",                                        "columnsView" },
            { "Average Disk",                                      "columnsView" },
            { "Max Disk",                                          "columnsView" },
            { "Path",                                              "columnsView" },

            { "Confirm Task delete",                               "generalView" },
#if __WIN32__
            { "Highlight Windows Services",                        "generalView" },
#endif
#if __APPLE__
            { "Highlight daemons",                                 "generalView" },
#endif
            { "Highlight changed values",                          "generalView" },
            { "Enable multiple process selection",                 "generalView" },
            { "Show Cpu meter numerically",                        "generalView" },
            { "Show Disk metre numerically",                       "generalView" },
            { "Show Memory metre numerically",                     "generalView" },
#if __WIN32__
            { "Show Virtual memory numerically",                   "generalView" },
#endif
#if __APPLE__
            { "Show Swap memory numerically",                      "generalView" },
#endif
#if __WIN32__
            { "Use Irix mode for process CPU% (Unix default)",     "generalView" },
#endif
#if __APPLE__
            { "Use Irix mode for process CPU% (Activity Monitor)", "generalView" },
#endif
            { Constants.Sections.ThemeColour,                      "themeView" },
            { Constants.Sections.ThemeMono,                        "themeView" },
            { Constants.Sections.ThemeMsDos,                       "themeView" },
            { Constants.Sections.ThemeTokyoNight,                  "themeView" },
            { Constants.Sections.ThemeMatrix,                      "themeView" },
            
            { "Blocks",                                            "metreView" },
            { "Bars",                                              "metreView" },
            { "Dots",                                              "metreView" },
            
            { "1000",                                              "delayView" },
            { "1500",                                              "delayView" },
            { "2000",                                              "delayView" },
            { "5000",                                              "delayView" },
            { "10000",                                             "delayView" },

            { "0",                                                 "limitView" },
            { "1",                                                 "limitView" },
            { "3",                                                 "limitView" },
            { "5",                                                 "limitView" },
            { "10",                                                "limitView" },
            { "20",                                                "limitView" },
            { "50",                                                "limitView" },
            { "100",                                               "limitView" },
            { "500",                                               "limitView" },
            { "1000",                                              "limitView" },

            { "-1",                                                "numProcsView" },
            { "5",                                                 "numProcsView" },
            { "10",                                                "numProcsView" },
            { "20",                                                "numProcsView" },
            { "50",                                                "numProcsView" },
            { "100",                                               "numProcsView" },
            { "500",                                               "numProcsView" },
            { "1000",                                              "numProcsView" },
        };

    [Theory]
    [MemberData(nameof(ControlSettingData))]
    public void Load_Initialises_Control_With_Settings(string setting, string controlName)
    {
        SetupScreen setupScreen = new(runContext);
        setupScreen.Load();

        Assert.NotNull(setupScreen.Controls);
        Assert.NotEmpty(setupScreen.Controls);

        ListView listView = setupScreen.Controls
            .OfType<ListView>()
            .Single(c => c.Name == controlName);

        bool result = listView.Items.Any(item => item.Text == setting);

        Assert.True(result);
        
        setupScreen.Unload();
    }
    
    [Fact]
    public void Draw_Uses_Theme_Colours()
    {
        runContext.AppConfig.DefaultTheme.Background = ConsolePalette.Magenta;
        runContext.AppConfig.DefaultTheme.Foreground = ConsolePalette.DarkCyan;
        
        SetupScreen setupScreen = new(runContext)
        {
            Visible = true,
            Width = 80,
            Height = 25
        };

        List<Color> capturedBgColors = [];
        List<Color> capturedFgColors = [];

        runContextHelper.terminal.SetupSet(t => t.BackgroundColor = It.IsAny<Color>())
            .Callback<Color>(color => capturedBgColors.Add(color));
        runContextHelper.terminal.SetupSet(t => t.ForegroundColor = It.IsAny<Color>())
            .Callback<Color>(color => capturedFgColors.Add(color));

        setupScreen.Load();
        setupScreen.Draw();

        Assert.NotEmpty(capturedBgColors);
        Assert.NotEmpty(capturedFgColors);
        Assert.Contains(ConsolePalette.Magenta, capturedBgColors);
        Assert.Contains(ConsolePalette.DarkCyan, capturedFgColors);
    }
    
    [Fact]
    public void Load_Sets_General_View_Visible_By_Default()
    {
        SetupScreen setupScreen = new(runContext);

        setupScreen.Load();
        
        ListView generalView = setupScreen.Controls
            .OfType<ListView>()
            .Single(c => c.Name == nameof(generalView));

        Assert.True(generalView.Visible);
    }
}
