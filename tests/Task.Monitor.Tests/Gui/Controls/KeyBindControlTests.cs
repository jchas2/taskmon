using System.Drawing;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class KeyBindControlTests
{
    // A key that can't be used draws its key and label in the theme's disabled colour.
    [Fact]
    public void A_Disabled_Key_Takes_The_Disabled_Colour()
    {
        Mock<ISystemTerminal> terminal = new();
        List<Color>           written  = [];

        terminal.SetupSet(t => t.ForegroundColor = It.IsAny<Color>()).Callback<Color>(written.Add);

        Theme theme = new(new ConfigSection("theme-test")) { CommandForegroundDisabled = Color.FromArgb(1, 2, 3) };

        KeyBindControl.Draw("F5", "Refresh", x: 0, y: 0, width: 10, theme, enabled: false, terminal.Object);

        Assert.Equal([Color.FromArgb(1, 2, 3), Color.FromArgb(1, 2, 3)], written.Take(2));
    }

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