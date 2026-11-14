using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.DriveInputBox;
using Task.Monitor.System.Controls.TextInputDialog;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;
using PickerBoxControl = Task.Monitor.System.Controls.PickerBox.PickerBox;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CommonControlTests
{
    private static readonly Color DialogBackground       = ColorTranslator.FromHtml("#101010");
    private static readonly Color DialogBorder           = ColorTranslator.FromHtml("#202020");
    private static readonly Color DialogButtonBackground = ColorTranslator.FromHtml("#303030");
    private static readonly Color DialogButtonForeground = ColorTranslator.FromHtml("#404040");
    private static readonly Color DialogForeground       = ColorTranslator.FromHtml("#505050");

    // Dialog colours that differ from the header and highlight colours they fall back to, so a
    // dialog themed from those instead would fail.
    private static Theme CreateDialogTheme()
    {
        ConfigSection section = new("dialog-theme-test");
        section.Add(Constants.Keys.HeaderBackground, "#aaaaaa");
        section.Add(Constants.Keys.HeaderForeground, "#bbbbbb");
        section.Add(Constants.Keys.BackgroundHighlight, "#cccccc");
        section.Add(Constants.Keys.ForegroundHighlight, "#dddddd");
        section.Add(Constants.Keys.DialogBackground, "#101010");
        section.Add(Constants.Keys.DialogBorder, "#202020");
        section.Add(Constants.Keys.DialogButtonBackground, "#303030");
        section.Add(Constants.Keys.DialogButtonForeground, "#404040");
        section.Add(Constants.Keys.DialogForeground, "#505050");

        return new Theme(section);
    }

    private static Theme CreateTheme()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.HotKey, "#112233");
        section.Add(Constants.Keys.HotKeyName, "#445566");
        section.Add(Constants.Keys.ListViewBorderBackground, "#778899");

        return new Theme(section);
    }

    [Fact]
    public void Dialogs_Take_Their_Colours_From_The_Dialog_Theme_Keys()
    {
        Theme theme = CreateDialogTheme();
        RecordingTerminal terminal = new();

        MessageBoxControl messageBox = new(terminal);
        CommonControl.Theme(messageBox, theme);

        Assert.Equal(DialogBackground, messageBox.DialogBackgroundColour);
        Assert.Equal(DialogBorder, messageBox.DialogBorderColour);
        Assert.Equal(DialogForeground, messageBox.DialogForegroundColour);
        Assert.Equal(DialogButtonBackground, messageBox.DialogButtonBackgroundColour);
        Assert.Equal(DialogButtonForeground, messageBox.DialogButtonForegroundColour);

        DriveInputBox driveInputBox = new(terminal);
        CommonControl.Theme(driveInputBox, theme);

        Assert.Equal(DialogBackground, driveInputBox.DialogBackgroundColour);
        Assert.Equal(DialogBorder, driveInputBox.DialogBorderColour);
        Assert.Equal(DialogForeground, driveInputBox.DialogForegroundColour);
        Assert.Equal(DialogButtonBackground, driveInputBox.DialogButtonBackgroundColour);
        Assert.Equal(DialogButtonForeground, driveInputBox.DialogButtonForegroundColour);

        PickerBoxControl pickerBox = new(terminal);
        CommonControl.Theme(pickerBox, theme);

        Assert.Equal(DialogBackground, pickerBox.DialogBackgroundColour);
        Assert.Equal(DialogBorder, pickerBox.DialogBorderColour);
        Assert.Equal(DialogForeground, pickerBox.DialogForegroundColour);

        TextInputDialog textInputDialog = new(terminal);
        CommonControl.Theme(textInputDialog, theme);

        Assert.Equal(DialogBackground, textInputDialog.DialogBackgroundColour);
        Assert.Equal(DialogForeground, textInputDialog.DialogForegroundColour);
        Assert.Equal(DialogButtonBackground, textInputDialog.DialogButtonBackgroundColour);
        Assert.Equal(DialogButtonForeground, textInputDialog.DialogButtonForegroundColour);
    }

    // The dialogs in Task.Monitor.System hold no wording of their own; the app supplies it.
    [Fact]
    public void Dialogs_Take_Their_Wording_From_Strings()
    {
        RecordingTerminal terminal = new();

        MessageBoxControl messageBox = new(terminal);
        CommonControl.Text(messageBox);

        Assert.Equal("OK", messageBox.OkText);
        Assert.Equal("Cancel", messageBox.CancelText);
        Assert.Equal("Use ← → and ↵ to select", messageBox.HelpText);

        DriveInputBox driveInputBox = new(terminal);
        CommonControl.Text(driveInputBox);

        Assert.Equal("OK", driveInputBox.OkText);
        Assert.Equal("Cancel", driveInputBox.CancelText);
        Assert.Equal("Custom path...", driveInputBox.CustomPathText);
        Assert.Contains("browse", driveInputBox.HelpText);

        PickerBoxControl pickerBox = new(terminal);
        CommonControl.Text(pickerBox);

        PickerBoxControl multiSelectPickerBox = new(terminal) { MultiSelect = true };
        CommonControl.Text(multiSelectPickerBox);

        Assert.Equal("↑ ↓ browse   ↵ select   Esc cancel", pickerBox.HelpText);
        Assert.Equal("↑ ↓ browse   Space toggle   ↵ confirm   Esc cancel", multiSelectPickerBox.HelpText);

        TextInputDialog textInputDialog = new(terminal);
        CommonControl.Text(textInputDialog);

        Assert.Equal("OK", textInputDialog.OkText);
        Assert.Equal("Cancel", textInputDialog.CancelText);
        Assert.Equal("Tab select button   ↵ confirm   Esc cancel", textInputDialog.HelpText);
    }

    [Fact]
    public void FooterText_Colours_Keys_And_Labels_From_The_Theme()
    {
        Theme theme = CreateTheme();

        string text = CommonControl.FooterText(theme, ("s", "Sort"), ("k", "End Task"));

        string expected =
            ConsolePalette.BackgroundSgr(ColorTranslator.FromHtml("#778899")) +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#112233")) + "s" +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#445566")) + " Sort" +
            CommonControl.FooterSeparator +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#112233")) + "k" +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#445566")) + " End Task";

        Assert.Equal(expected, text);
    }

    [Fact]
    public void FooterText_Is_Empty_Without_HotKeys() =>
        Assert.Equal(string.Empty, CommonControl.FooterText(CreateTheme()));

    [Fact]
    public void FooterText_Keeps_The_Given_Order_And_Formats_Every_Key()
    {
        string text = CommonControl.FooterText(
            CreateTheme(),
            CommonControl.SortHotKey,
            CommonControl.FilterHotKey,
            CommonControl.ClearFilterHotKey,
            CommonControl.ScrollHotKey,
            ("F1", "Help"));

        Assert.Equal(
            "s Sort   f Filter   esc Clear Filter   ↑ ↓ pgup pgdn Scroll   F1 Help",
            AnsiTestHelper.StripColours(text));
    }

    [Theory]
    [InlineData("s",   "s")]
    [InlineData("S",   "s")]
    [InlineData("Esc", "esc")]
    [InlineData("↑ ↓ PgUp PgDn", "↑ ↓ pgup pgdn")]
    [InlineData("F1",  "F1")]
    [InlineData("f10", "F10")]
    [InlineData("F24", "F24")]
    [InlineData("F25", "f25")]
    [InlineData("F",   "f")]
    public void FormatHotKey_Lower_Cases_All_But_Function_Keys(string key, string expected) =>
        Assert.Equal(expected, CommonControl.FormatHotKey(key));
}
