using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Gui.Controls;

public sealed class MenuListViewItemTests
{
    private readonly Mock<ISystemTerminal> terminal = new();
    
    [Fact]
    public void Constructor_With_Text_Only_Sets_Text_And_AssociatedControl()
    {
        Control control = new(terminal.Object);
        string text = "Test Menu Item";
        MenuListViewItem menuItem = new(control, text);

        Assert.Equal(text, menuItem.Text);
        Assert.Same(control, menuItem.AssociatedControl);
    }

    [Fact]
    public void Constructor_With_Colors_Sets_All_Properties()
    {
        Control control = new(terminal.Object);
        string text = "Colored Menu Item";
        Color backgroundColor = ConsolePalette.Blue;
        Color foregroundColor = ConsolePalette.White;

        MenuListViewItem menuItem = new(
            control,
            text,
            backgroundColor,
            foregroundColor);

        Assert.Equal(text, menuItem.Text);
        Assert.Same(control, menuItem.AssociatedControl);
        Assert.Equal(backgroundColor, menuItem.BackgroundColour);
        Assert.Equal(foregroundColor, menuItem.ForegroundColour);
    }
    
    [Fact]
    public void Constructor_With_Empty_Text_Accepts_Empty_String()
    {
        Control control = new(terminal.Object);
        string text = string.Empty;
        MenuListViewItem menuItem = new(control, text);

        Assert.Equal(string.Empty, menuItem.Text);
        Assert.Same(control, menuItem.AssociatedControl);
    }
    
    public static TheoryData<Color, Color> ColourCombinations() => new()
    {
        { ConsolePalette.Black, ConsolePalette.White },
        { ConsolePalette.Red, ConsolePalette.Yellow },
        { ConsolePalette.Green, ConsolePalette.Black },
        { ConsolePalette.DarkGray, ConsolePalette.Cyan },
    };

    [Theory]
    [MemberData(nameof(ColourCombinations))]
    public void Constructor_WithVariousColorCombinations_SetsColorsCorrectly(
        Color backgroundColor,
        Color foregroundColor)
    {
        Control control = new(terminal.Object);
        string text = "Colored Item";

        MenuListViewItem menuItem = new(
            control,
            text,
            backgroundColor,
            foregroundColor);

        Assert.Equal(backgroundColor, menuItem.BackgroundColour);
        Assert.Equal(foregroundColor, menuItem.ForegroundColour);
    }
}