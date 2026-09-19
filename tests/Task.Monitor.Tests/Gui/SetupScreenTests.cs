using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;
using Task.Monitor.Tests.Common;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Gui;

public sealed class SetupScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;
    private readonly ScreenApplication screenApp;

    public SetupScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
        screenApp = new ScreenApplication(runContext.Terminal);
    }

    [Fact]
    public void SetupScreen_Canary_Test() =>
        Assert.Equal(20, CanaryTestHelper.GetPropertyCount<SetupScreen>());

    [Fact]
    public void Constructor_With_Valid_Run_Context_Initialises_Successfully()
    {
        SetupScreen setupScreen = new(runContext, screenApp);

        Assert.NotNull(setupScreen);
    }

    [Fact]
    public void Constructor_With_Null_RunContext_Throws_NullReferenceException() =>
        Assert.Throws<NullReferenceException>(() => new SetupScreen(null!, screenApp));

    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        SetupScreen setupScreen = new(runContext, screenApp);

        Assert.Equal(ConsolePalette.Black, setupScreen.BackgroundColour);
        Assert.NotEmpty(setupScreen.Controls);
        Assert.True(setupScreen.CursorVisible);
        Assert.Equal(ConsolePalette.Gray, setupScreen.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.Black, setupScreen.DialogBorderColour);
        Assert.Equal(ConsolePalette.DarkGray, setupScreen.DialogButtonBackgroundColour);
        Assert.Equal(ConsolePalette.Black, setupScreen.DialogButtonForegroundColour);
        Assert.Equal(ConsolePalette.Gray, setupScreen.DialogBackgroundColour);
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
        SetupScreen setupScreen = new(runContext, screenApp);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = false).Verifiable();

        setupScreen.Load();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = false, Times.AtLeastOnce);
        
        setupScreen.Unload();
    }

    [Fact]
    public void Unload_Calls_OnUnload_Sets_CursorVisible_True()
    {
        SetupScreen setupScreen = new(runContext, screenApp);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = true).Verifiable();

        setupScreen.Unload();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = true, Times.Once);
    }

    [Fact]
    public void Load_Initialises_Header_Table()
    {
        string header = "Changes are saved to the following config file:";
        SetupScreen setupScreen = new(runContext, screenApp);
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
            { "Show Cpu chart label numerically",                  "generalView" },
            { "Show Gpu chart label numerically",                  "generalView" },
            { "Show Memory chart label numerically",               "generalView" },
            { "Show Gpu Memory chart label numerically",           "generalView" },
#if __WIN32__
            { "Show Virtual memory chart label numerically",       "generalView" },
#endif
#if __APPLE__
            { "Show Swap memory chart label numerically",          "generalView" },
#endif
            { "Show Disk chart label numerically",                 "generalView" },
            { "Show Network chart label numerically",              "generalView" },
            { "Show chart Y axis scale",                           "generalView" },
#if __WIN32__
            { "Use Irix mode for per-process CPU% (individual core saturation)",     "generalView" },
#endif
#if __APPLE__
            { "Use Irix mode for per-process CPU% (Activity Monitor)",               "generalView" },
#endif
            { Constants.Sections.ThemeTaskmonDefault,              "themeView" },
            { Constants.Sections.ThemeMsDos,                       "themeView" },
            
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
        SetupScreen setupScreen = new(runContext, screenApp);
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
        runContext.AppConfig.Theme.Background = ConsolePalette.Magenta;
        runContext.AppConfig.Theme.Foreground = ConsolePalette.DarkCyan;
        
        SetupScreen setupScreen = new(runContext, screenApp)
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

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    // Regression test: every list here defaults to ShowBorder = true, which insets the actual
    // drawable viewport by one column each side - OnResize was setting column 0's width to the
    // control's raw Width/MenuViewWidth with no allowance for that inset, which tripped
    // ListView.DrawItem's viewport-fit guard on every row's first column and silently blanked
    // every list on the screen. No prior test caught this because none of them called Resize()
    // before Draw() - only setting Width/Height via the object initializer leaves every column
    // at its unconfigured default, never touching the bug at all.
    [Fact]
    public void Draw_After_Resize_Shows_Menu_And_Tab_Row_Text()
    {
        SetupScreen setupScreen = new(runContext, screenApp) {
            Width = 100,
            Height = 30
        };

        setupScreen.Load();
        setupScreen.Resize();
        setupScreen.Draw();

        string output = CapturedOutput();

        Assert.Contains("GENERAL", output);
        Assert.Contains("COLUMNS", output);
        Assert.Contains("LAYOUTS", output);
        Assert.Contains("Confirm Task delete", output);

        setupScreen.Unload();
    }
    
    // Regression coverage for the minimal LayoutDesignerScreen entry point: 'N' only means
    // anything while the LAYOUTS tab is the active one, and it must call Open() (a fresh example
    // tree, no name) before showing the designer, since that's the only way the singleton screen
    // ever picks up per-visit state.
    [Fact]
    public void N_Key_On_The_Layouts_Tab_Opens_The_Layout_Designer_On_A_Fresh_Tree()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp);
        setupScreen.Load();

        foreach (ListView tab in setupScreen.Controls.OfType<ListView>()
            .Where(c => c.Name is "generalView" or "columnsView" or "themeView"
                or "layoutView" or "metreView" or "delayView" or "numProcsView")) {
            tab.Visible = tab.Name == "layoutView";
        }

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Null(designer.LayoutName);
        Assert.Contains(designer.Tree.Panes(), p => p.ControlType == PaneControlType.Process);

        setupScreen.Unload();
    }

    [Fact]
    public void N_Key_On_A_Different_Tab_Does_Nothing()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp);
        setupScreen.Load(); // GENERAL is the active tab by default, not LAYOUTS

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false), ref handled);

        Assert.False(handled);

        setupScreen.Unload();
    }

    private static ListView TabView(SetupScreen setupScreen, string name) =>
        setupScreen.Controls.OfType<ListView>().Single(c => c.Name == name);

    // Makes LAYOUTS the active tab with its list focused - what Right arrow does after picking
    // LAYOUTS in the category menu.
    private static ListView ActivateLayoutsTab(SetupScreen setupScreen)
    {
        foreach (ListView tab in setupScreen.Controls.OfType<ListView>()
            .Where(c => c.Name is "generalView" or "columnsView" or "themeView"
                or "layoutView" or "metreView" or "delayView" or "numProcsView")) {
            tab.Visible = tab.Name == "layoutView";
        }

        ListView layoutView = TabView(setupScreen, "layoutView");
        layoutView.SetFocus();
        return layoutView;
    }

    private static void SelectRow(ListView listView, string text)
    {
        for (int i = 0; i < listView.Items.Count; i++) {
            if (listView.Items[i].Text == text) {
                listView.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"No row '{text}'.");
    }

    [Fact]
    public void Layouts_Tab_Lists_New_Layout_Then_The_Summary_Layouts_With_The_Default_Highlighted()
    {
        SetupScreen setupScreen = new(runContext, screenApp);
        setupScreen.Load();

        ListView layoutView = TabView(setupScreen, "layoutView");
        List<string> rows = Enumerable.Range(0, layoutView.Items.Count).Select(i => layoutView.Items[i].Text).ToList();

        Assert.Equal("+ New Layout", rows[0]);
        Assert.Equal(
            runContext.AppConfig.SummaryLayouts2.Select(l => l.Name).OrderBy(n => n),
            rows.Skip(1));
        Assert.Equal("All Charts", layoutView.SelectedItem?.Text);

        setupScreen.Unload();
    }

    [Fact]
    public void Enter_On_A_Layout_Opens_It_In_The_Designer()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp) { Width = 100, Height = 30 };
        localScreenApp.RegisterScreen(setupScreen);
        setupScreen.Load();

        ListView layoutView = ActivateLayoutsTab(setupScreen);
        SelectRow(layoutView, "Cpu and Memory");

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Equal("Cpu and Memory", designer.LayoutName);
        Assert.Equal(
            [PaneControlType.Cpu, PaneControlType.Memory, PaneControlType.Process],
            designer.Tree.Panes().Select(p => p.ControlType));
    }

    [Fact]
    public void Enter_On_New_Layout_Opens_An_Unnamed_Example_In_The_Designer()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        designer.Open(SummaryLayoutTree.CreateExample(), "Leftover From Before");
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp) { Width = 100, Height = 30 };
        setupScreen.Load();

        ListView layoutView = ActivateLayoutsTab(setupScreen);
        SelectRow(layoutView, "+ New Layout");

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Null(designer.LayoutName);
        Assert.Contains(designer.Tree.Panes(), p => p.ControlType == PaneControlType.Process);
    }

    // Enter only acts once the list itself has focus (Right arrow into it) - with focus still on
    // the category menu it must not open anything.
    [Fact]
    public void Enter_While_The_Category_Menu_Has_Focus_Does_Not_Open_The_Designer()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp) { Width = 100, Height = 30 };
        setupScreen.Load();

        ActivateLayoutsTab(setupScreen);
        TabView(setupScreen, "menuView").SetFocus();

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), ref handled);

        Assert.False(handled);

        setupScreen.Unload();
    }

    // The highlighted layout becomes the SUMMARY screen's layout on F10 Done, like the THEMES tab.
    [Fact]
    public void F10_Makes_The_Highlighted_Layout_The_Summary_Default()
    {
        // F10 also pushes the sampling settings onto the running ProcessService (ApplySamplingSettings).
        runContext.ServiceController.AddService(() => new Task.Monitor.System.Services.Process.ProcessService());

        SetupScreen setupScreen = new(runContext, screenApp) { Width = 100, Height = 30 };
        setupScreen.Load();

        SelectRow(TabView(setupScreen, "layoutView"), "Disk Read and Write Bytes");

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.F10, false, false, false), ref handled);

        Assert.Equal("Disk Read and Write Bytes", runContext.AppConfig.DefaultSummaryLayout2?.Name);

        setupScreen.Unload();
    }

    [Fact]
    public void F10_With_New_Layout_Highlighted_Keeps_The_Current_Default()
    {
        // F10 also pushes the sampling settings onto the running ProcessService (ApplySamplingSettings).
        runContext.ServiceController.AddService(() => new Task.Monitor.System.Services.Process.ProcessService());

        SetupScreen setupScreen = new(runContext, screenApp) { Width = 100, Height = 30 };
        setupScreen.Load();

        SelectRow(TabView(setupScreen, "layoutView"), "+ New Layout");

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.F10, false, false, false), ref handled);

        Assert.Equal("All Charts", runContext.AppConfig.DefaultSummaryLayout2?.Name);

        setupScreen.Unload();
    }

    // Coming back from the designer (its Esc pops back to this screen, which is Show()n again)
    // lands on the LAYOUTS tab with the list focused, not reset to GENERAL.
    [Fact]
    public void Returning_From_The_Designer_Reopens_The_Layouts_Tab()
    {
        ScreenApplication localScreenApp = new(runContext.Terminal);
        LayoutDesignerScreen designer = new(runContext);
        localScreenApp.RegisterScreen(designer);

        SetupScreen setupScreen = new(runContext, localScreenApp) { Width = 100, Height = 30 };
        setupScreen.Load();

        ActivateLayoutsTab(setupScreen);

        bool handled = false;
        setupScreen.KeyPressed(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false), ref handled);

        setupScreen.Unload();
        setupScreen.Show();

        Assert.True(TabView(setupScreen, "layoutView").Visible);
        Assert.False(TabView(setupScreen, "generalView").Visible);
        Assert.True(TabView(setupScreen, "layoutView").Focused);
        Assert.Equal("LAYOUTS", TabView(setupScreen, "menuView").SelectedItem?.Text);

        // A later, ordinary visit resets to GENERAL again.
        setupScreen.Unload();
        setupScreen.Show();

        Assert.True(TabView(setupScreen, "generalView").Visible);

        setupScreen.Unload();
    }

    [Fact]
    public void Load_Sets_General_View_Visible_By_Default()
    {
        SetupScreen setupScreen = new(runContext, screenApp);

        setupScreen.Load();
        
        ListView generalView = setupScreen.Controls
            .OfType<ListView>()
            .Single(c => c.Name == nameof(generalView));

        Assert.True(generalView.Visible);
    }
}
