using System.Drawing;
using System.Reflection;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.System.Controls.ListView;
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

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Null_RunContext_Throws_ArgumentNullException() =>
        Assert.Throws<NullReferenceException>(() => new HelpScreen(null!));

    [Fact]
    public void Constructor_With_Valid_Run_Context_Initialises_Successfully()
    {
        HelpScreen helpScreen = new(runContext);
        
        Assert.NotNull(helpScreen);
    }

    [Fact]
    public void Default_Properties_After_Construction_Have_Default_Values()
    {
        HelpScreen helpScreen = new(runContext);

        Assert.Equal(ConsolePalette.Black, helpScreen.BackgroundColour);
        Assert.Equal(5, helpScreen.Controls.Count);
        Assert.Equal(ConsolePalette.White, helpScreen.ForegroundColour);
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
    public void Draw_Uses_Theme_Colours()
    {
        HelpScreen helpScreen = new(runContext)
        {
            Visible = true,
            Width = 80,
            Height = 25
        };
        
        Color? capturedBg = null;
        Color? capturedFg = null;

        runContextHelper.terminal.Object.BackgroundColor = ConsolePalette.Cyan;
        runContextHelper.terminal.Object.ForegroundColor = ConsolePalette.Magenta;

        runContextHelper.terminal.SetupSet(t => t.BackgroundColor = It.IsAny<Color>())
            .Callback<Color>(color => capturedBg = color);
        runContextHelper.terminal.SetupSet(t => t.ForegroundColor = It.IsAny<Color>())
            .Callback<Color>(color => capturedFg = color);

        helpScreen.Load();
        helpScreen.Draw();

        Assert.NotNull(capturedBg);
        Assert.NotNull(capturedFg);
        Assert.Equal(runContext.AppConfig.Theme.Background, capturedBg);
        Assert.Equal(runContext.AppConfig.Theme.Foreground, capturedFg);
    }

    [Fact]
    public void HelpScreen_Canary_Test() =>
        Assert.Equal(15, CanaryTestHelper.GetPropertyCount<HelpScreen>());

    public static TheoryData<string> HelpTextData()
        => new()
        {
            "CHART COLOURS",
            "PROCESS AND PATH COLOURS",
            "FUNCTION KEYS",
            "OTHER KEYS",
            "Switch to the next colour theme",
            "Switch SUMMARY to the next layout",
            "Show the about screen",
            "Show the next tip in the footer"
        };

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

    // The old "End Task:" text section is now the OTHER KEYS box.
    [Fact]
    public void OnLoad_Relabels_End_Task_As_Other_Keys()
    {
        HelpScreen helpScreen = Sized(new HelpScreen(runContext));

        ListViewItem row = Assert.Single(Rows(Section(helpScreen, "OTHER KEYS")));

        Assert.Equal("k:", row.SubItems[0].Text);
        Assert.DoesNotContain(Sections(helpScreen), section => section.HeaderText.Contains("END TASK"));
    }

    // At 120 columns no text is cut short: each fits its column, less the trailing space every
    // cell keeps.
    [Fact]
    public void OnResize_Fits_Every_Text_At_120_Columns()
    {
        HelpScreen helpScreen = Sized(new HelpScreen(runContext));

        Assert.All(Sections(helpScreen), section =>
            Assert.All(Rows(section), row => {
                List<ListViewSubItem> cells = row.SubItems.Cast<ListViewSubItem>().ToList();

                for (int i = 0; i < cells.Count; i++) {
                    string text = cells[i].Text;

                    Assert.True(
                        text.Length <= section.ColumnHeaders[i].Width - 1,
                        $"'{text}' doesn't fit {section.HeaderText} column {i}");
                }
            }));
    }

    // The border takes two rows, so a section is two taller than its rows; the key list's last row
    // ("t:") used to be cut off.
    [Fact]
    public void OnResize_Gives_Every_Section_Room_For_All_Its_Rows()
    {
        HelpScreen helpScreen = Sized(new HelpScreen(runContext));

        Assert.All(Sections(helpScreen), section => Assert.Equal(section.Items.Count + 2, section.Height));
        Assert.Equal("t:", Rows(Section(helpScreen, "KEYS"))[^1].SubItems[0].Text);
    }

    // A short terminal loses whole sections from the bottom, never one drawn over the exit key.
    [Fact]
    public void OnResize_Hides_The_Sections_That_Do_Not_Fit_Above_The_Exit_Key()
    {
        HelpScreen     helpScreen = Sized(new HelpScreen(runContext), height: 30);
        List<ListView> sections   = Sections(helpScreen);

        Assert.All(sections.Where(section => section.Visible), section =>
            Assert.True(section.Y + section.Height <= helpScreen.Height - 1));
        Assert.Contains(sections, section => !section.Visible);
        Assert.Equal(sections.TakeWhile(section => section.Visible).Count(), sections.Count(section => section.Visible));
    }

    [Fact]
    public void OnResize_Stacks_The_Sections_Under_The_Banner_In_Order()
    {
        HelpScreen     helpScreen = Sized(new HelpScreen(runContext));
        List<ListView> sections   = Sections(helpScreen);

        Assert.Equal(
            new[] { "CHART COLOURS", "PROCESS AND PATH COLOURS", "KEYS", "FUNCTION KEYS", "OTHER KEYS" },
            sections.Select(section => section.HeaderText));
        Assert.Equal(1, sections[0].Y);

        for (int i = 1; i < sections.Count; i++) {
            Assert.Equal(sections[i - 1].Y + sections[i - 1].Height, sections[i].Y);
        }

        Assert.All(sections, section => Assert.True(section.Visible));
    }

    private static List<ListViewItem> Rows(ListView section) =>
        section.Items.Cast<ListViewItem>().ToList();

    private static ListView Section(HelpScreen helpScreen, string title) =>
        Sections(helpScreen).Single(section => section.HeaderText == title);

    private static List<ListView> Sections(HelpScreen helpScreen) =>
        helpScreen.Controls.OfType<ListView>().ToList();

    [Theory]
    [MemberData(nameof(HelpTextData))]
    public void Should_Generate_Help_Text_OnLoad_And_OnDraw(string helpText)
    {
        HelpScreen helpScreen = Sized(new HelpScreen(runContext));

        helpScreen.Draw();

        Assert.Contains(helpText, CapturedOutput());
    }

    // Loaded and laid out at 120 columns.
    private static HelpScreen Sized(HelpScreen helpScreen, int height = 40)
    {
        helpScreen.Width  = 120;
        helpScreen.Height = height;

        helpScreen.Load();
        helpScreen.Resize();

        return helpScreen;
    }
}
