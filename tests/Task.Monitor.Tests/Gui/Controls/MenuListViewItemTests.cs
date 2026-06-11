using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

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
        ConsoleColor backgroundColor = ConsoleColor.Blue;
        ConsoleColor foregroundColor = ConsoleColor.White;

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
    
    [Theory]
    [InlineData(ConsoleColor.Black, ConsoleColor.White)]
    [InlineData(ConsoleColor.Red, ConsoleColor.Yellow)]
    [InlineData(ConsoleColor.Green, ConsoleColor.Black)]
    [InlineData(ConsoleColor.DarkGray, ConsoleColor.Cyan)]
    public void Constructor_WithVariousColorCombinations_SetsColorsCorrectly(
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor)
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