using Moq;
using Task.Monitor.System.Controls;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.System.Tests.Controls;

public class ControlTests
{
    private static readonly Mock<ISystemTerminal> SystemTerminalSingleton = new();
    
    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminalMock = TerminalMock.Setup();
        Control control = new(terminalMock.Object);

        Assert.Equal(ConsolePalette.Black, control.BackgroundColour);
        Assert.Equal(0, control.ControlCount);
        Assert.Empty(control.Controls);
        Assert.Equal(ConsolePalette.White, control.ForegroundColour);
        Assert.Equal(0, control.Height);
        Assert.True(control.Visible);
        Assert.Equal(0, control.Width);
        Assert.Equal(0, control.X);
        Assert.Equal(0, control.Y);
    }
    
    public static TheoryData<string> BorderedControls() => new() { "ListView", "Chart", "Metre" };

    private static Control CreateBorderedControl(string kind, ISystemTerminal terminal) => kind switch {
        "ListView" => new Task.Monitor.System.Controls.ListView.ListView(terminal),
        "Chart"    => new Task.Monitor.System.Controls.Chart.Chart(terminal),
        _          => new Task.Monitor.System.Controls.Metre.MetreControl(terminal)
    };

    // Regression test: focus used to be shown by swapping BorderColour to the focus colour, so a
    // screen re-applying its theme to a focused control (on every Load or Draw) wiped the cue out.
    // Focus is now applied when the border is drawn; theming only ever sets the unfocused colour.
    // Reads Control.FocusSelectionColour rather than setting it - it is a process-wide static.
    [Theory]
    [MemberData(nameof(BorderedControls))]
    public void Retheming_A_Focused_Control_Keeps_The_Focus_Colour_Until_Focus_Leaves(string kind)
    {
        Color focusColour = Control.FocusSelectionColour;
        Color themeColour = focusColour.ToArgb() == Color.Teal.ToArgb() ? Color.Olive : Color.Teal;

        Control control = CreateBorderedControl(kind, TerminalMock.Setup().Object);
        control.Focused = true;

        control.BorderColour = themeColour;

        Assert.Equal(focusColour.ToArgb(), control.DisplayBorderColour.ToArgb());

        control.Focused = false;

        Assert.Equal(themeColour.ToArgb(), control.DisplayBorderColour.ToArgb());
    }

    public static List<Control> GetControlData()
        => new() {
            new Control(SystemTerminalSingleton.Object),
            new Control(SystemTerminalSingleton.Object),
            new Control(SystemTerminalSingleton.Object)
        };

    [Fact]
    public void Should_Add_All_Items()
    {
        var control = new Control(SystemTerminalSingleton.Object);
        control.Controls.AddRange(GetControlData().ToArray());
        
        Assert.True(control.Controls.Count == 3);
    }
}
