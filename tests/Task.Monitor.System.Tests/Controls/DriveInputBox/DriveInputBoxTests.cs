using Moq;
using Task.Monitor.System.Controls.DriveInputBox;
using Task.Monitor.Tests.Common;
using DriveInputBoxControl = Task.Monitor.System.Controls.DriveInputBox.DriveInputBox;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.System.Tests.Controls.DriveInputBox;

public sealed class DriveInputBoxTests
{
    private static DriveInputBoxControl CreateBox(Mock<ISystemTerminal>? terminal = null)
    {
        terminal ??= TerminalMock.Setup();

        DriveInputBoxControl control = new(new ForwardingTerminal(terminal.Object)) {
            Height = 12,
            Title = "Select a drive",
            Visible = true,
            Width = 40,
            X = 5,
            Y = 10
        };

        control.SetCandidates(["C:\\", "D:\\"]);
        return control;
    }

    [Fact]
    public void DriveInputBox_Canary_Test() =>
        Assert.Equal(25, CanaryTestHelper.GetPropertyCount<DriveInputBoxControl>());

    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        DriveInputBoxControl control = new(new ForwardingTerminal(terminal.Object));

        Assert.Equal(ConsolePalette.Gray, control.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.Black, control.DialogBorderColour);
        Assert.Equal(ConsolePalette.DarkGray, control.DialogButtonBackgroundColour);
        Assert.Equal(ConsolePalette.Black, control.DialogButtonForegroundColour);
        Assert.Equal(ConsolePalette.Black, control.DialogForegroundColour);
        Assert.NotNull(control.Title);
        Assert.Empty(control.Title);
        Assert.Equal(DriveInputBoxResult.None, control.Result);
        Assert.Null(control.SelectedPath);
        Assert.False(control.CustomPathRequested);
        Assert.True(control.Visible);
    }

    [Fact]
    public void SetCandidates_Always_Appends_A_Custom_Path_Row()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        // Selecting past the two real candidates lands on "Custom path...".
        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(DriveInputBoxResult.Ok, control.Result);
        Assert.True(control.CustomPathRequested);
        Assert.Null(control.SelectedPath);
    }

    [Fact]
    public void Enter_With_The_First_Candidate_Highlighted_Selects_It()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.True(handled);
        Assert.Equal(DriveInputBoxResult.Ok, control.Result);
        Assert.Equal("C:\\", control.SelectedPath);
        Assert.False(control.CustomPathRequested);
    }

    [Fact]
    public void DownArrow_Then_Enter_Selects_The_Second_Candidate()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(DriveInputBoxResult.Ok, control.Result);
        Assert.Equal("D:\\", control.SelectedPath);
    }

    [Fact]
    public void Escape_Cancels_Regardless_Of_Which_Button_Is_Focused()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape), ref handled);

        Assert.True(handled);
        Assert.Equal(DriveInputBoxResult.Cancel, control.Result);
        Assert.Null(control.SelectedPath);
    }

    [Fact]
    public void RightArrow_Then_Enter_Cancels_Without_Setting_A_Selection()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(DriveInputBoxResult.Cancel, control.Result);
        Assert.Null(control.SelectedPath);
        Assert.False(control.CustomPathRequested);
    }

    [Fact]
    public void LeftArrow_After_RightArrow_Restores_Ok_Focus()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(DriveInputBoxResult.Ok, control.Result);
        Assert.Equal("C:\\", control.SelectedPath);
    }

    [Fact]
    public void SetCandidates_Resets_Any_Previous_Result_And_Focus()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);
        Assert.Equal(DriveInputBoxResult.Cancel, control.Result);

        control.SetCandidates(["C:\\"]);

        Assert.Equal(DriveInputBoxResult.None, control.Result);
        Assert.Null(control.SelectedPath);
        Assert.False(control.CustomPathRequested);
    }

    [Fact]
    public void ListX_ListY_And_ListWidth_Reflect_The_Inner_Lists_Layout_After_Resize()
    {
        DriveInputBoxControl control = CreateBox();
        control.ShowDriveInputBox();

        // Matches DriveInputBox.OnResize: list.X = X + 1, list.Y = Y + 2, list.Width = Width - 2.
        Assert.Equal(control.X + 1, control.ListX);
        Assert.Equal(control.Y + 2, control.ListY);
        Assert.Equal(control.Width - 2, control.ListWidth);
    }

    [Fact]
    public void Should_Draw()
    {
        const string title = "Select a drive";

        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        DriveInputBoxControl control = CreateBox(terminal);

        control.ShowDriveInputBox();

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(title))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Custom path..."))), Times.AtLeastOnce);
    }
}
