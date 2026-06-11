using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class KeyBindControlTests
{
    [Fact]
    public void Should_Draw_Key_Bind_Text()
    {
        Mock<ISystemTerminal> terminal = new();
        terminal.Setup(t => t.WindowWidth).Returns(64);
        terminal.Setup(t => t.WindowHeight).Returns(24);

        Theme theme = new();

        KeyBindControl.Draw(
            "F10",
            "Exit",
            x: 0,
            y: 23,
            width: 10,
            theme,
            enabled: true,
            terminal.Object);
        
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("F10"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Exit"))), Times.Once);
    }
}