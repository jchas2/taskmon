using Moq;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.Tests.Common;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;

namespace Task.Monitor.System.Tests.Controls.MessageBox;

public sealed class MessageBoxTests
{
    [Fact]
    public void MessageBox_Canary_Test() =>
        Assert.Equal(15, CanaryTestHelper.GetPropertyCount<MessageBoxControl>());

    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        MessageBoxControl control = new(terminal.Object);
        
        Assert.Equal(ConsoleColor.Black, control.BackgroundColour);
        Assert.Empty(control.Controls);
        Assert.Equal(ConsoleColor.White, control.ForegroundColour);
        Assert.Equal(0, control.Height);
        Assert.Equal(MessageBoxButtons.OkCancel, control.Buttons);
        Assert.NotNull(control.Name);
        Assert.Equal(MessageBoxResult.None, control.Result);
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
        const string text = "Are you sure you want to continue?";
        const string title = "Confirm Delete";
        
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        
        MessageBoxControl control = new(terminal.Object) {
            BackgroundColour = ConsoleColor.Gray,
            ForegroundColour = ConsoleColor.Black,
            Buttons = MessageBoxButtons.OkCancel,
            Height = 12,
            Text = text,
            Title = title,
            Visible =  true,
            Width = 40,
            X = 5,
            Y = 10
        };

        Assert.Equal(ConsoleColor.Gray, control.BackgroundColour);
        Assert.Equal(ConsoleColor.Black, control.ForegroundColour);
        Assert.True(control.Buttons == MessageBoxButtons.OkCancel);
        Assert.Equal(12, control.Height);
        Assert.Equal(text, control.Text);
        Assert.Equal(title, control.Title);
        Assert.True(control.Visible);
        Assert.Equal(40, control.Width);
        Assert.Equal(5, control.X);
        Assert.Equal(10, control.Y);
    }

    public static TheoryData<List<ConsoleKey>, MessageBoxResult> KeyPressData()
        => new() {
            // Key combinations with defined behaviour.
            { new List<ConsoleKey>() { ConsoleKey.Escape }, MessageBoxResult.Cancel },
            { new List<ConsoleKey>() { ConsoleKey.Enter }, MessageBoxResult.Ok },
            { new List<ConsoleKey>() { ConsoleKey.O, ConsoleKey.Enter }, MessageBoxResult.Ok },
            { new List<ConsoleKey>() { ConsoleKey.Y, ConsoleKey.Enter }, MessageBoxResult.Ok },
            { new List<ConsoleKey>() { ConsoleKey.LeftArrow, ConsoleKey.Enter }, MessageBoxResult.Ok },
            { new List<ConsoleKey>() { ConsoleKey.LeftArrow, ConsoleKey.Escape }, MessageBoxResult.Cancel },
            { new List<ConsoleKey>() { ConsoleKey.RightArrow, ConsoleKey.Enter }, MessageBoxResult.Cancel },
            { new List<ConsoleKey>() { ConsoleKey.C, ConsoleKey.Enter }, MessageBoxResult.Cancel },
            { new List<ConsoleKey>() { ConsoleKey.N, ConsoleKey.Enter }, MessageBoxResult.Cancel },
            { new List<ConsoleKey>() { ConsoleKey.RightArrow, ConsoleKey.Escape }, MessageBoxResult.Cancel },
            // Keys with no action.
            { new List<ConsoleKey>() { ConsoleKey.A }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.B }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.D }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.E }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.G }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.H }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.I }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.J }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.K }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.L }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.M }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.P }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Q }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.R }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.S }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.T }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.U }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.V }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.W }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.X }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Z }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Add }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Backspace }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Clear }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Decimal }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Delete }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Divide }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.End }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F1 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F2 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F3 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F4 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F5 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F6 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F7 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F8 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F9 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F10 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F11 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.F12 }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Help }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Home }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Insert }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Multiply }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Print }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Separator }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Spacebar }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Subtract }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.Tab }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.DownArrow }, MessageBoxResult.None },
            { new List<ConsoleKey>() { ConsoleKey.UpArrow }, MessageBoxResult.None },
        };
    
    [Theory]
    [MemberData(nameof(KeyPressData))]
    public void MessageBox_On_ConsoleKey_Should_Set_Result(List<ConsoleKey> consoleKeys, MessageBoxResult result)
    {
        MessageBoxControl control = new(TerminalMock.Setup().Object) {
            Buttons = MessageBoxButtons.OkCancel,
            Height = 12,
            Text = "Text",
            Title = "Title",
            Visible =  true,
            Width = 40,
            X = 5,
            Y = 10
        };

        bool handled = false;

        foreach (var consoleKey in consoleKeys) {
            control.KeyPressed(ControlHelper.GetConsoleKeyInfo(consoleKey), ref handled);
        }
        
        Assert.True(handled);
        Assert.True(result == control.Result);
    }

    [Fact]
    public void Should_Draw()
    {
        const string text = "Are you sure?";
        const string title = "End Task";
        const string help = "Use \u2190 \u2192 and \u21B5 to select";
        
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        
        MessageBoxControl control = new(terminal.Object) {
            Buttons = MessageBoxButtons.OkCancel,
            Height = 12,
            Text = text,
            Title = title,
            Visible =  true,
            Width = 40,
            X = 5,
            Y = 10
        };
        
        control.ShowMessageBox();
        
        terminal.Verify(t => t.SetCursorPosition(5, 10), Times.Exactly(2));
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(title))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(text))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(help))), Times.Once);
    }
}