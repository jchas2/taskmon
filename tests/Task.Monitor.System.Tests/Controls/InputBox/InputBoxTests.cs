using System.Reflection;
using Moq;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.Tests.Common;
using InputBoxControl = Task.Monitor.System.Controls.InputBox.InputBox;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.System.Tests.Controls.InputBox;

public sealed class InputBoxTests
{
    [Fact]
    public void InputBox_Canary_Test() =>
        Assert.Equal(14, CanaryTestHelper.GetPropertyCount<InputBoxControl>());
    
    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        InputBoxControl control = new(new ForwardingTerminal(terminal.Object));

        Assert.Equal(ConsolePalette.Black, control.BackgroundColour);
        Assert.Empty(control.Controls);
        Assert.Equal(ConsolePalette.White, control.ForegroundColour);
        Assert.Equal(0, control.Height);
        Assert.NotNull(control.Name);
        Assert.Empty(control.Name);
        Assert.Equal(InputBoxResult.Enter, control.Result);
        Assert.True(0 == control.TabIndex);
        Assert.False(control.TabStop);
        Assert.NotNull(control.Text);
        Assert.Empty(control.Text);
        Assert.NotNull(control.Title);
        Assert.Empty(control.Title);
        Assert.True(control.Visible);
        Assert.Equal(0, control.Width);
        Assert.Equal(0, control.X);
        Assert.Equal(0, control.Y);
    }

    [Fact]
    public void Should_Set_Initial_Properties()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        
        InputBoxControl control = new(new ForwardingTerminal(terminal.Object)) {
            BackgroundColour = ConsolePalette.Gray,
            ForegroundColour = ConsolePalette.Black,
            Height = 1,
            Title = "Enter Text",
            Visible =  true,
            Width = 42,
            X = 5,
            Y = 10
        };

        Assert.Equal(ConsolePalette.Gray, control.BackgroundColour);
        Assert.Equal(ConsolePalette.Black, control.ForegroundColour);
        Assert.Equal(1, control.Height);
        Assert.Equal("Enter Text", control.Title);
        Assert.True(control.Visible);
        Assert.Equal(42, control.Width);
        Assert.Equal(5, control.X);
        Assert.Equal(10, control.Y);
    }

    public static TheoryData<ConsoleKeyInfo, InputBoxResult> KeyPressData()
        => new()
        {
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter),      InputBoxResult.Enter },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape),     InputBoxResult.Cancel },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.Backspace),  InputBoxResult.None },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.Delete),     InputBoxResult.None },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow),  InputBoxResult.None },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), InputBoxResult.None },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.Insert),     InputBoxResult.None }
        };
    
    [Theory]
    [MemberData(nameof(KeyPressData))]
    public void KeyPress_Should_Set_InputBoxResult(ConsoleKeyInfo keyInfo, InputBoxResult expectedResult)
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        
        InputBoxControl control = new(new ForwardingTerminal(terminal.Object)) {
            X = 0,
            Y = 0, 
            Width = 42, 
            Height = 1,
            Title = "Enter Text"
        };

        bool handled = false;
        control.KeyPressed(keyInfo, ref handled);

        Assert.Equal(expectedResult, control.Result);
        Assert.True(handled);
    }

    [Fact]
    public void Should_Draw()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();

        InputBoxControl control = new(new ForwardingTerminal(terminal.Object)) {
            X = 0,
            Y = 0, 
            Width = 42, 
            Height = 1,
            Title = "Enter Text"
        };
        
        control.ShowInputBox();
        
        bool handled = false;

        control.KeyPressed(
            new ConsoleKeyInfo(
                (char)ConsoleKey.H, 
                ConsoleKey.H, 
                shift: false, 
                alt: false, 
                control: false), 
            ref handled);
         
        terminal.Verify(t => t.Write("H"), Times.Once);
        
        Assert.Equal("H", control.Text);
        Assert.True(handled);
    }
}
