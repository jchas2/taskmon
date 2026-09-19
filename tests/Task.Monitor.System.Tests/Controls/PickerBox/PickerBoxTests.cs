using Moq;
using Task.Monitor.System.Controls.PickerBox;
using Task.Monitor.Tests.Common;
using PickerBoxControl = Task.Monitor.System.Controls.PickerBox.PickerBox;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.System.Tests.Controls.PickerBox;

public sealed class PickerBoxTests
{
    private static PickerBoxControl CreateBox(
        Mock<ISystemTerminal>? terminal = null,
        bool multiSelect = false,
        IReadOnlyList<string>? labels = null,
        IReadOnlyList<bool>? initiallyChecked = null)
    {
        terminal ??= TerminalMock.Setup();

        PickerBoxControl control = new(new ForwardingTerminal(terminal.Object)) {
            Height = 10,
            MultiSelect = multiSelect,
            Title = "Choose a control",
            Visible = true,
            Width = 40,
            X = 5,
            Y = 10
        };

        control.SetItems(labels ?? ["Cpu", "Memory", "Gpu"], initiallyChecked);
        return control;
    }

    [Fact]
    public void PickerBox_Canary_Test() =>
        Assert.Equal(26, CanaryTestHelper.GetPropertyCount<PickerBoxControl>());

    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = new(new ForwardingTerminal(terminal.Object));

        Assert.Equal(ConsolePalette.Gray, control.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.Black, control.DialogBorderColour);
        Assert.Equal(ConsolePalette.Black, control.DialogForegroundColour);
        Assert.NotNull(control.Title);
        Assert.Empty(control.Title);
        Assert.False(control.MultiSelect);
        Assert.Equal(PickerBoxResult.None, control.Result);
        Assert.True(control.Visible);
    }

    [Fact]
    public void Enter_With_The_First_Row_Highlighted_Selects_It()
    {
        PickerBoxControl control = CreateBox();
        control.ShowPickerBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.True(handled);
        Assert.Equal(PickerBoxResult.Ok, control.Result);
        Assert.Equal(0, control.SelectedIndex);
    }

    [Fact]
    public void DownArrow_Then_Enter_Selects_The_Second_Row()
    {
        PickerBoxControl control = CreateBox();
        control.ShowPickerBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(PickerBoxResult.Ok, control.Result);
        Assert.Equal(1, control.SelectedIndex);
    }

    [Fact]
    public void Escape_Cancels_Without_A_Selection()
    {
        PickerBoxControl control = CreateBox();
        control.ShowPickerBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape), ref handled);

        Assert.True(handled);
        Assert.Equal(PickerBoxResult.Cancel, control.Result);
    }

    [Fact]
    public void SetItems_Resets_Any_Previous_Result()
    {
        PickerBoxControl control = CreateBox();
        control.ShowPickerBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);
        Assert.Equal(PickerBoxResult.Ok, control.Result);

        control.SetItems(["Cpu"]);

        Assert.Equal(PickerBoxResult.None, control.Result);
    }

    [Fact]
    public void MultiSelect_Space_Toggles_A_Row_And_Enter_Confirms_Whatever_Is_Checked()
    {
        PickerBoxControl control = CreateBox(multiSelect: true);
        control.ShowPickerBox();

        bool handled = false;
        // Check row 0 (Cpu), move to row 2 (Gpu) and check that too, leaving row 1 (Memory)
        // unchecked - confirming that Enter reads back whatever is checked, not merely whichever
        // row happens to be highlighted at the time.
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Spacebar), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Spacebar), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(PickerBoxResult.Ok, control.Result);
        Assert.Equal([0, 2], control.CheckedIndices.ToList());
    }

    [Fact]
    public void MultiSelect_SetItems_Applies_The_Initially_Checked_Rows()
    {
        PickerBoxControl control = CreateBox(multiSelect: true, initiallyChecked: [true, false, true]);
        control.ShowPickerBox();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(PickerBoxResult.Ok, control.Result);
        Assert.Equal([0, 2], control.CheckedIndices.ToList());
    }

    [Fact]
    public void SingleSelect_Does_Not_Show_Checkboxes()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = CreateBox(terminal, multiSelect: false);
        control.ShowPickerBox();

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("[ ]") || s.Contains("[x]"))), Times.Never);
    }

    [Fact]
    public void MultiSelect_Shows_Checkboxes()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = CreateBox(terminal, multiSelect: true);
        control.ShowPickerBox();

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("[ ]"))), Times.AtLeastOnce);
    }

    // Regression test: the label column was sized to the full list width, ignoring the "[x] "
    // checkbox ListView draws ahead of it in MultiSelect mode - so the label no longer fit and
    // ListView.DrawItem skipped it, leaving rows with only a checkbox and no text.
    [Fact]
    public void MultiSelect_Shows_The_Row_Labels_Alongside_The_Checkboxes()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = CreateBox(terminal, multiSelect: true);
        control.ShowPickerBox();

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Cpu"))), Times.AtLeastOnce);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Memory"))), Times.AtLeastOnce);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Gpu"))), Times.AtLeastOnce);
    }

    [Fact]
    public void DownArrow_Does_Not_Repaint_The_Frame_Or_Title()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = CreateBox(terminal);
        control.ShowPickerBox();

        terminal.Invocations.Clear();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);

        // Browsing the list must only touch the list's own rows - repainting the border, title
        // or help text on every arrow press is what caused DriveInputBox's dialog to flicker
        // before PickerBox copied its incremental-redraw approach.
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Choose a control"))), Times.Never);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("confirm") || s.Contains("select"))), Times.Never);
    }

    [Fact]
    public void Should_Draw()
    {
        const string title = "Choose a control";

        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        PickerBoxControl control = CreateBox(terminal);

        control.ShowPickerBox();

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(title))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Cpu"))), Times.AtLeastOnce);
    }
}
