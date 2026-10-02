using Moq;
using Task.Monitor.System.Controls.TextInputDialog;
using Task.Monitor.Tests.Common;
using TextInputDialogControl = Task.Monitor.System.Controls.TextInputDialog.TextInputDialog;

namespace Task.Monitor.System.Tests.Controls.TextInputDialog;

public sealed class TextInputDialogTests
{
    private static TextInputDialogControl CreateDialog(
        Mock<ISystemTerminal>? terminal = null,
        Func<char, bool>? filter = null)
    {
        terminal ??= TerminalMock.Setup();

        TextInputDialogControl control = new(new ForwardingTerminal(terminal.Object)) {
            Height = TextInputDialogControl.PreferredHeight,
            Title = "Save Layout As",
            Visible = true,
            Width = 44,
            X = 5,
            Y = 10,
            CharacterFilter = filter
        };

        control.SetText(string.Empty);
        return control;
    }

    private static ConsoleKeyInfo Typed(char ch) =>
        new(ch, char.IsLetter(ch) ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString()) : ConsoleKey.Spacebar,
            shift: char.IsUpper(ch), alt: false, control: false);

    private static void Type(TextInputDialogControl control, string text)
    {
        bool handled = false;

        foreach (char ch in text) {
            control.KeyPressed(Typed(ch), ref handled);
        }
    }

    [Fact]
    public void TextInputDialog_Canary_Test() =>
        Assert.Equal(26, CanaryTestHelper.GetPropertyCount<TextInputDialogControl>());

    [Fact]
    public void Should_Construct_Default()
    {
        TextInputDialogControl control = new(new ForwardingTerminal(TerminalMock.Setup().Object));

        Assert.Empty(control.Title);
        Assert.Empty(control.Text);
        Assert.Equal(TextInputDialogResult.None, control.Result);
        Assert.Null(control.CharacterFilter);
    }

    // Buttons are written one character at a time (so the first letter of the focused one can be
    // highlighted), so assert on everything written, in order, rather than any single Write.
    private static string AllWritten(Mock<ISystemTerminal> terminal) =>
        string.Concat(terminal.Invocations
            .Where(i => i.Method.Name == "Write" && i.Arguments.Count == 1)
            .Select(i => i.Arguments[0]?.ToString() ?? string.Empty));

    [Fact]
    public void Should_Draw_The_Title_And_Both_Buttons()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        TextInputDialogControl control = CreateDialog(terminal);

        control.ShowTextInputDialog();

        string written = AllWritten(terminal);
        Assert.Contains("Save Layout As", written);
        Assert.Contains("OK", written);
        Assert.Contains("Cancel", written);
    }

    [Fact]
    public void Typing_Then_Enter_Confirms_Ok_With_The_Typed_Text()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "My Dashboard");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.True(handled);
        Assert.Equal(TextInputDialogResult.Ok, control.Result);
        Assert.Equal("My Dashboard", control.Text);
    }

    [Fact]
    public void Escape_Cancels()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "abc");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape), ref handled);

        Assert.True(handled);
        Assert.Equal(TextInputDialogResult.Cancel, control.Result);
    }

    [Fact]
    public void Tab_Moves_To_Cancel_So_Enter_Cancels()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "abc");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Tab), ref handled);
        Assert.Equal(TextInputDialogResult.None, control.Result);

        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(TextInputDialogResult.Cancel, control.Result);
    }

    [Fact]
    public void Tab_Twice_Returns_To_Ok()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "abc");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Tab), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Tab), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(TextInputDialogResult.Ok, control.Result);
    }

    // Left/Right edit the text, so they must never be taken for button navigation.
    [Fact]
    public void Left_Arrow_Moves_The_Caret_So_Typing_Inserts_Mid_Text()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "ac");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        Type(control, "b");

        Assert.Equal("abc", control.Text);
        Assert.Equal(TextInputDialogResult.None, control.Result);
    }

    [Fact]
    public void Backspace_Removes_The_Character_Before_The_Caret()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        Type(control, "abcd");

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Backspace), ref handled);

        Assert.Equal("abc", control.Text);
    }

    [Fact]
    public void Characters_Rejected_By_The_Filter_Are_Not_Added()
    {
        TextInputDialogControl control = CreateDialog(filter: ch => char.IsLetterOrDigit(ch) || ch is '-' or ' ');
        control.ShowTextInputDialog();

        bool handled = false;
        foreach (char ch in "a_b.c/d") {
            control.KeyPressed(new ConsoleKeyInfo(ch, ConsoleKey.Oem1, false, false, false), ref handled);
        }

        Assert.Equal("abcd", control.Text);
    }

    [Fact]
    public void Typing_Stops_At_MaxLength()
    {
        TextInputDialogControl control = CreateDialog();
        control.MaxLength = 5;
        control.ShowTextInputDialog();

        Type(control, "abcdefgh");

        Assert.Equal("abcde", control.Text);
    }

    [Fact]
    public void SetText_Resets_The_Result_And_Button_Focus()
    {
        TextInputDialogControl control = CreateDialog();
        control.ShowTextInputDialog();

        bool handled = false;
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Tab), ref handled);
        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);
        Assert.Equal(TextInputDialogResult.Cancel, control.Result);

        control.SetText(string.Empty);
        Assert.Equal(TextInputDialogResult.None, control.Result);

        control.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);
        Assert.Equal(TextInputDialogResult.Ok, control.Result);
    }

    // Matches DriveInputBox/PickerBox: a keystroke redraws only the field row, never the frame -
    // a full repaint per keystroke is what made DriveInputBox flicker.
    [Fact]
    public void Typing_Does_Not_Repaint_The_Frame_Or_Title()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        TextInputDialogControl control = CreateDialog(terminal);
        control.ShowTextInputDialog();

        terminal.Invocations.Clear();

        Type(control, "abc");

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Save Layout As"))), Times.Never);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("confirm"))), Times.Never);
    }

    // Text longer than the field scrolls rather than spilling past the dialog's right border.
    [Fact]
    public void Long_Text_Scrolls_Within_The_Field()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        TextInputDialogControl control = CreateDialog(terminal);
        control.ShowTextInputDialog();

        string longText = new('a', 60);
        Type(control, longText);

        int fieldWidth = control.Width - 4;

        terminal.Verify(t => t.Write(It.Is<string>(s => s.Length > fieldWidth && s.Trim('a').Length == 0)), Times.Never);
        Assert.Equal(longText, control.Text);
    }
}
