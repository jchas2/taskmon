using Moq;
using Task.Monitor.Gui;
using Task.Monitor.System;
using Task.Monitor.System.Screens;
using Task.Monitor.Tests.Common;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Gui;

public sealed class MainScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public MainScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void MainScreen_Canary_Test() =>
        Assert.Equal(17, CanaryTestHelper.GetPropertyCount<MainScreen>());

    [Fact]
    public void Constructor_With_Valid_Run_Context_Initialises_Successfully()
    {
        Mock<ISystemTerminal> terminalMock = new();
        ScreenApplication screenApp = new(terminalMock.Object);
        MainScreen mainScreen = new(screenApp, runContext);

        Assert.NotNull(mainScreen);
    }

    [Fact]
    public void Constructor_With_Null_Args_Throws_NullReferenceException() =>
        Assert.Throws<NullReferenceException>(() => new MainScreen(null!, null!));

    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        Mock<ISystemTerminal> terminalMock = new();
        ScreenApplication screenApp = new(terminalMock.Object);
        MainScreen mainScreen = new(screenApp, runContext);

        Assert.Equal(ConsolePalette.Black, mainScreen.BackgroundColour);
        Assert.NotEmpty(mainScreen.Controls);
        Assert.True(mainScreen.CursorVisible);
        Assert.Equal(ConsolePalette.Gray, mainScreen.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.Black, mainScreen.DialogBorderColour);
        Assert.Equal(ConsolePalette.DarkGray, mainScreen.DialogButtonBackgroundColour);
        Assert.Equal(ConsolePalette.Black, mainScreen.DialogButtonForegroundColour);
        Assert.Equal(ConsolePalette.Gray, mainScreen.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.White, mainScreen.ForegroundColour);
        Assert.Equal(0, mainScreen.Height);
        Assert.NotNull(mainScreen.Name);
        Assert.Empty(mainScreen.Name);
        Assert.True(0 == mainScreen.TabIndex);
        Assert.False(mainScreen.TabStop);
        Assert.True(mainScreen.Visible);
        Assert.Equal(0, mainScreen.Width);
        Assert.Equal(0, mainScreen.X);
        Assert.Equal(0, mainScreen.Y);
    }
}