using System.Reflection;
using Moq;
using Task.Monitor.Gui;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui;

public sealed class HelpScreenTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;
    
    public HelpScreenTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }
    
    [Fact]
    public void HelpScreen_Canary_Test() =>
        Assert.Equal(12, CanaryTestHelper.GetPropertyCount<HelpScreen>());

    [Fact]
    public void Constructor_With_Valid_Run_Context_Initialises_Successfully()
    {
        HelpScreen helpScreen = new(runContext);
        
        Assert.NotNull(helpScreen);
    }

    [Fact]
    public void Constructor_With_Null_RunContext_Throws_ArgumentNullException() =>
        Assert.Throws<NullReferenceException>(() => new HelpScreen(null!));
    
    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        HelpScreen helpScreen = new(runContext);

        Assert.Equal(ConsoleColor.Black, helpScreen.BackgroundColour);
        Assert.Empty(helpScreen.Controls);
        Assert.True(helpScreen.CursorVisible);
        Assert.Equal(ConsoleColor.White, helpScreen.ForegroundColour);
        Assert.Equal(0, helpScreen.Height);
        Assert.NotNull(helpScreen.Name);
        Assert.Empty(helpScreen.Name);
        Assert.True(0 == helpScreen.TabIndex);
        Assert.False(helpScreen.TabStop);
        Assert.True(helpScreen.Visible);
        Assert.Equal(0, helpScreen.Width);
        Assert.Equal(0, helpScreen.X);
        Assert.Equal(0, helpScreen.Y);
    }
    
    [Fact]
    public void Load_Calls_OnLoad_Sets_CursorVisible_False()
    {
        HelpScreen helpScreen = new(runContext);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = false).Verifiable();
        
        helpScreen.Load();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = false, Times.AtLeastOnce);
    }

    [Fact]
    public void Load_Calls_OnUnload_Sets_CursorVisible_True()
    {
        HelpScreen helpScreen = new(runContext);
        runContextHelper.terminal.SetupSet(t => t.CursorVisible = true).Verifiable();
        
        helpScreen.Unload();

        runContextHelper.terminal.VerifySet(t => t.CursorVisible = true, Times.Once);
    }

    public static TheoryData<string> HelpTextData()
        => new() 
        {
            "taskmon",
            "Metre Colours:",
            "Process and Path Colours:",
            "Screen Navigation",
            "List Navigation",
            "Function Keys",
            "Press ESC to exit Help"
        };

    [Theory]
    [MemberData(nameof(HelpTextData))]
    public void Should_Generate_Help_Text_OnLoad_And_OnDraw(string helpText)
    {
        HelpScreen helpScreen = new(runContext);
        string capturedText = String.Empty;

        runContextHelper.terminal.Setup(t => t.WriteLine(It.IsAny<string>()))
            .Callback<string>(txt => capturedText = txt);
        
        helpScreen.Load();
        helpScreen.Draw();

        Assert.Contains(helpText, capturedText);
    }

    [Fact]
    public void Draw_Uses_Theme_Colours()
    {
        HelpScreen helpScreen = new(runContext)
        {
            Visible = true,
            Width = 80,
            Height = 25
        };
        
        ConsoleColor? capturedBg = null;
        ConsoleColor? capturedFg = null;

        runContextHelper.terminal.Object.BackgroundColor = ConsoleColor.Cyan;
        runContextHelper.terminal.Object.ForegroundColor = ConsoleColor.Magenta;

        runContextHelper.terminal.SetupSet(t => t.BackgroundColor = It.IsAny<ConsoleColor>())
            .Callback<ConsoleColor>(color => capturedBg = color);
        runContextHelper.terminal.SetupSet(t => t.ForegroundColor = It.IsAny<ConsoleColor>())
            .Callback<ConsoleColor>(color => capturedFg = color);

        helpScreen.Load();
        helpScreen.Draw();

        Assert.NotNull(capturedBg);
        Assert.NotNull(capturedFg);
        Assert.Equal(runContext.AppConfig.DefaultTheme.Background, capturedBg);
        Assert.Equal(runContext.AppConfig.DefaultTheme.Foreground, capturedFg);
    }
}
