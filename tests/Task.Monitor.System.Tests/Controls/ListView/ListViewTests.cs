using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.Tests.Common;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;

using System.Drawing;
using System.Text.RegularExpressions;
namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewTests
{
    private readonly RecordingTerminal terminal = new();

    private ListViewControl GetDefaultListView()
    {
        ListViewControl listView = new(terminal) {
            Width = 80,
            Height = 24,
            X = 0,
            Y = 0
        };
 
        return listView;
    }
    
    [Fact]
    public void ListView_Canary_Test() =>
        Assert.Equal(28, CanaryTestHelper.GetPropertyCount<ListViewControl>());
    
    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        ListViewControl listView = new(terminal.Object);
        
        Assert.Equal(ConsolePalette.Black, listView.BackgroundColour);
        Assert.Equal(ConsolePalette.White, listView.BackgroundHighlightColour);
        Assert.Equal(ConsolePalette.White, listView.BorderColour);
        Assert.Empty(listView.ColumnHeaders);
        Assert.Empty(listView.Controls);
        Assert.Empty(listView.EmptyListViewText);
        Assert.True(listView.EnableRowSelect);
        Assert.True(listView.EnableScroll);
        Assert.Empty(listView.FooterText);
        Assert.Equal(ConsolePalette.White, listView.ForegroundColour);
        Assert.Equal(ConsolePalette.Cyan, listView.ForegroundHighlightColour);
        Assert.Equal(ConsolePalette.Black, listView.HeaderBackgroundColour);
        Assert.Equal(ConsolePalette.White, listView.HeaderForegroundColour);
        Assert.Equal(0, listView.Height);
        Assert.Empty(listView.HeaderText);
        Assert.Empty(listView.Items);
        Assert.NotNull(listView.Name);
        Assert.Null(listView.SelectedItem);
        Assert.Equal(0, listView.SelectedIndex); // TODO: This should be -1.
        Assert.True(listView.ShowBorder);
        Assert.False(listView.ShowCheckboxes);
        Assert.True(listView.ShowColumnHeaders);
        Assert.True(0 == listView.TabIndex);
        Assert.False(listView.TabStop);
        Assert.True(listView.Visible);
        Assert.Equal(0, listView.Width);
        Assert.Equal(0, listView.X);
        Assert.Equal(0, listView.Y);
    }

    [Fact]
    public void Should_Set_Initial_Properties()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        ListViewControl listView = new(terminal.Object) {
            BackgroundColour = ConsolePalette.Gray,
            BackgroundHighlightColour = ConsolePalette.DarkGray,
            BorderColour = ConsolePalette.Red,
            EmptyListViewText = "No Items",
            EnableRowSelect = false,
            EnableScroll = false,
            FooterText = "Footer",
            ForegroundColour = ConsolePalette.Blue,
            ForegroundHighlightColour = ConsolePalette.DarkGray,
            HeaderBackgroundColour = ConsolePalette.Green,
            HeaderForegroundColour = ConsolePalette.Black,
            HeaderText = "Header",
            Height = 24,
            ShowBorder = false,
            Visible =  true,
            Width = 80,
            X = 2,
            Y = 2
        };

        Assert.Equal(ConsolePalette.Gray, listView.BackgroundColour);
        Assert.Equal(ConsolePalette.DarkGray, listView.BackgroundHighlightColour);
        Assert.Equal(ConsolePalette.Red, listView.BorderColour);
        Assert.Equal("No Items", listView.EmptyListViewText);
        Assert.False(listView.EnableRowSelect);
        Assert.False(listView.EnableScroll);
        Assert.Equal("Footer", listView.FooterText);
        Assert.Equal(ConsolePalette.Blue, listView.ForegroundColour);
        Assert.Equal(ConsolePalette.DarkGray, listView.ForegroundHighlightColour);
        Assert.Equal(ConsolePalette.Green, listView.HeaderBackgroundColour);
        Assert.Equal(ConsolePalette.Black, listView.HeaderForegroundColour);
        Assert.Equal("Header", listView.HeaderText);
        Assert.Equal(24, listView.Height);
        Assert.False(listView.ShowBorder);
        Assert.True(listView.Visible);
        Assert.Equal(80, listView.Width);
        Assert.Equal(2, listView.X);
        Assert.Equal(2, listView.Y);
    }
    
    [Fact]
    public void SelectedIndex_Throws_ArgumentOutOfRangeException_For_Invalid_Index()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item = new ("Item 0");
        listView.Items.Add(item);

        Assert.Throws<ArgumentOutOfRangeException>(() => listView.SelectedIndex = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.SelectedIndex = 1);
    }
    
    [Fact]
    public void SelectedIndex_Sets_Correctly()
    {
        ListViewControl listView = GetDefaultListView();
        listView.Items.Add(new ListViewItem("Item 0"));
        listView.Items.Add(new ListViewItem("Item 1"));
        listView.SelectedIndex = 1;

        Assert.Equal(1, listView.SelectedIndex);
    }
    
    [Fact]
     public void SelectedItem_Returns_Correct_Item()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item0 = new("Item 0");
        ListViewItem item1 = new("Item 1");
        listView.Items.Add(item0);
        listView.Items.Add(item1);
        listView.SelectedIndex = 1;

        Assert.Same(item1, listView.SelectedItem);
    }

    [Fact]
    public void Item_Add_Should_Update_Item_Count()
    {
        ListViewControl listView = GetDefaultListView();

        Assert.Equal(0, listView.ItemCount);
        
        listView.Items.Add(new ListViewItem("Item 0"));
        
        Assert.Equal(1, listView.ItemCount);
    }
    
    [Fact]
    public void Item_Remove_Should_Update_Item_Count()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item = new("Item 0");
        listView.Items.Add(item);
        
        Assert.Equal(1, listView.ItemCount);
        
        listView.Items.Remove(item);
        
        Assert.Equal(0, listView.ItemCount);
    }

    [Fact]
    public void Get_Item_By_Index_Should_Return_Item()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item0 = new("Item 0");
        ListViewItem item1 = new("Item 1");
        listView.Items.Add(item0);
        listView.Items.Add(item1);
        ListViewItem result = listView.GetItemByIndex(1);
        
        Assert.Same(item1, result);
    }

    [Fact]
    public void Get_Item_By_Index_Throws_ArgumentOutOfRangeException_For_Invalid_Index()
    {
        ListViewControl listView = GetDefaultListView();
        listView.Items.Add(new ListViewItem("Item 0"));
        
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.GetItemByIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.GetItemByIndex(1));
    }
    
    [Fact]
    public void InsertItem_Inserts_At_Correct_Index()
    {
        ListViewControl listView = GetDefaultListView();
        listView.Items.Add(new ListViewItem("Item 0"));
        listView.Items.Add(new ListViewItem("Item 2"));
        ListViewItem newItem = new("Item 1");
        listView.InsertItem(1, newItem);

        Assert.Equal(3, listView.ItemCount);
        Assert.Same(newItem, listView.GetItemByIndex(1));
    }
    
    [Fact]
    public void ClearColumnHeaders_Removes_All_Headers_From_List()
    {
        ListViewControl listView = GetDefaultListView();
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 0"));
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 1"));
        listView.ClearColumnHeaders();

        Assert.Equal(0, listView.ColumnHeaderCount);
    }
    
    [Fact]
    public void ColumnHeaders_Add_Should_Update_ColumnHeader_Count()
    {
        ListViewControl listView = GetDefaultListView();
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 0"));
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 1"));

        Assert.Equal(2, listView.ColumnHeaderCount);
    }

    // Regression coverage for a column cell whose text contains East Asian wide characters (each
    // rendered as two terminal columns): sizing the cell by string.Length instead of terminal
    // display width let the cell overflow its column, shifting everything drawn after it in the
    // row - reported as a corrupted StartupControl row when a vendor's CompanyName was in Chinese.
    [Fact]
    public void FormatColumnCell_Fits_Ascii_Text_Exactly_In_The_Column_Width()
    {
        string cell = ListViewControl.FormatColumnCell("Header 0", 16, rightAligned: false);

        Assert.Equal(16, cell.TerminalWidth());
        Assert.StartsWith("Header 0", cell);
    }

    [Fact]
    public void FormatColumnCell_Fits_Wide_Character_Text_In_The_Column_Width()
    {
        string cell = ListViewControl.FormatColumnCell("TODO: <公司名稱>", 24, rightAligned: false);

        Assert.Equal(24, cell.TerminalWidth());
    }

    [Fact]
    public void FormatColumnCell_Truncates_Wide_Character_Text_That_Does_Not_Fit()
    {
        string cell = ListViewControl.FormatColumnCell("公司名稱公司名稱公司名稱", 10, rightAligned: false);

        Assert.Equal(10, cell.TerminalWidth());
    }

    [Fact]
    public void FormatColumnCell_Right_Aligns_Wide_Character_Text_Within_The_Column_Width()
    {
        string cell = ListViewControl.FormatColumnCell("公司", 10, rightAligned: true);

        Assert.Equal(10, cell.TerminalWidth());
        Assert.EndsWith("公司 ", cell);
    }

    // Note: a corrupted row still contains every column's text verbatim in the raw output - the
    // corruption is the *rendered* width once a real terminal interprets the wide characters, which
    // does not show up in a plain Contains() check. This strips the ANSI escapes DrawItem emits
    // (cursor moves, colour/bold sets) and measures what is left with the same TerminalWidth logic
    // a real terminal would apply, to catch the actual overflow.
    private static readonly Regex AnsiEscape = new(@"\x1b\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex MoveToEscape = new(@"\x1b\[\d+;\d+H", RegexOptions.Compiled);

    [Fact]
    public void Draw_Keeps_A_Row_At_The_Configured_Width_When_A_Column_Has_Wide_Characters()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.ShowBorder = false;

        // The first column is 16 columns wide; this publisher-style string is short in char count
        // but, at 2 columns per CJK char, would occupy far more than 16 display columns if sized
        // by string.Length the way the column layout used to.
        listView.Items[0].SubItems[0].Text = "TODO: <公司名稱>";
        listView.Draw();

        string[] rowSegments = MoveToEscape.Split(terminal.Output);
        string row = Assert.Single(rowSegments, segment => segment.Contains("TODO:"));
        string visibleRow = AnsiEscape.Replace(row, string.Empty);

        Assert.Equal(listView.Width, visibleRow.TerminalWidth());
    }

    [Fact]
    public void OnKeyPressed_Should_Return_False_With_No_Items()
    {
        ListViewControl listView = GetDefaultListView();
        bool handled = false;
        listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.A), ref handled);
        
        Assert.False(handled);
    }

    public static TheoryData<ConsoleKeyInfo, int, int> ArrowKeyScrollData()
        => new()
        {
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.UpArrow),   0, 0 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.UpArrow),   1, 0 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.UpArrow),   4, 3 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), 0, 1 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), 1, 2 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), 4, 4 },
        };
    
    [Theory]
    [MemberData(nameof(ArrowKeyScrollData))]
    public void Should_Scroll_On_Arrow_Keys(ConsoleKeyInfo keyInfo, int selectIndex, int selectedIndex)
    {
        ListViewControl listView = GetDefaultListView();

        string[] items = new string[] { "Item 0", "Item 1", "Item 2", "Item 3", "Item 4" };
        foreach (var item in items) {
            listView.Items.Add(new ListViewItem(item));
        }
        
        // Move the selection focus to the nominated item by selectIndex.
        listView.SelectedIndex = selectIndex;
        Assert.Equal(selectIndex, listView.SelectedIndex);

        // Send a key press and confirm selection focus has moved to the nominated item by selectedIndex.
        bool handled = false;
        listView.KeyPressed(keyInfo, ref handled);
        
        Assert.Equal(selectedIndex, listView.SelectedIndex);
    }

    public static TheoryData<ConsoleKeyInfo, int> ArrowKeyNoScrollData()
        => new()
        {
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.UpArrow),   0 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.UpArrow),   1 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), 0 },
            { ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), 1 },
        };
    
    [Theory]
    [MemberData(nameof(ArrowKeyNoScrollData))]
    public void Should_Not_Scroll_When_EnableScroll_Is_False(ConsoleKeyInfo keyInfo, int selectIndex)
    {
        ListViewControl listView = GetDefaultListView();
        listView.EnableScroll = false;
        listView.Items.Add(new ListViewItem("Item 0"));
        listView.Items.Add(new ListViewItem("Item 1"));
        
        // Move the selection focus to the nominated item by selectIndex.
        listView.SelectedIndex = selectIndex;
        Assert.Equal(selectIndex, listView.SelectedIndex);

        // Send a key press and confirm selection focus has NOT moved.
        bool handled = false;
        listView.KeyPressed(keyInfo, ref handled);
        
        Assert.Equal(selectIndex, listView.SelectedIndex);
    }
    
    private const char Esc = (char)27;

    private static string Fg(Color c) => ConsolePalette.ForegroundSgr(c);
    private static string Bg(Color c) => ConsolePalette.BackgroundSgr(c);

    private static ListViewControl CreatePopulatedListView(RecordingTerminal terminal)
    {
        ListViewControl listView = new(terminal) {
            Width = 80,
            Height = 24,
            X = 0,
            Y = 0
        };

        listView.ColumnHeaders.AddRange(new[] {
            new ListViewColumnHeader("Header 0") { Width = 16 },
            new ListViewColumnHeader("Header 1") { Width = 32 }
        });

        listView.Items.AddRange(new[] {
            new ListViewItem("Item 0"),
            new ListViewItem("Item 1")
        });

        listView.Items[0].SubItems.Add(new ListViewSubItem(listView.Items[0], "0 SubItem1"));
        listView.Items[1].SubItems.Add(new ListViewSubItem(listView.Items[1], "1 SubItem1"));

        return listView;
    }

        [Fact]
    public void OnDraw_Blits_The_Frame_With_A_Single_Span_Write()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        // Confirm double buffering is used. 
        Assert.Equal(1, terminal.WriteSpanCalls);
        Assert.Equal(0, terminal.WriteCharCalls);
        Assert.Equal(0, terminal.WriteStringCalls);
    }

    [Fact]
    public void OnDraw_Does_Not_Use_The_Console_Colour_Or_Cursor_Apis()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        // Confirm double buffering is used. 
        Assert.Equal(0, terminal.SetCursorPositionCalls);
        Assert.Equal(0, terminal.ForegroundColorSets);
        Assert.Equal(0, terminal.BackgroundColorSets);
    }

    [Fact]
    public void OnDraw_Emits_The_Header_Cursor_Move_Header_Colour_And_Trailing_Reset()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        string output = terminal.Output;

        // The header is rendered one row above the scrollable region, at the top-left cell.
        Assert.Contains(Esc + "[1;1H", output);
        Assert.Contains(Fg(listView.HeaderForegroundColour), output);
        Assert.Contains(Bg(listView.HeaderBackgroundColour), output);
        Assert.EndsWith(AnsiConsoleStringExtensions.Reset, output);
    }

    // Bold is deliberately not used for the header: terminals commonly desaturate a bold
    // foreground colour, which can make it unreadable against some header colour pairs.
    [Fact]
    public void OnDraw_Does_Not_Render_The_Header_In_Bold()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        Assert.DoesNotContain(Esc + "[1m", terminal.Output);
    }

    [Fact]
    public void OnDraw_Highlights_The_Selected_Row()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();
        
        string output = terminal.Output;
        
        // The default selection is row 0. Unfocused, the highlight background is Gray
        // and the highlight foreground is Black.
        Assert.Contains(Fg(ConsolePalette.Black), output);
        Assert.Contains(Bg(ConsolePalette.Gray), output);
    }
    
    [Fact]
    public void OnDraw_Renders_The_Border_With_Header_And_Footer_Text()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.ShowBorder = true;
        listView.BorderColour = ConsolePalette.Red;
        listView.HeaderText = "Processes";
        listView.FooterText = "2 items";
        listView.Draw();

        string output = terminal.Output;

        // Border glyphs (corners) are emitted in the border colour.
        Assert.Contains(Fg(ConsolePalette.Red), output);
        Assert.Contains("╭", output); // top-left corner
        Assert.Contains("╯", output); // bottom-right corner

        // Header and footer labels are drawn into the border.
        Assert.Contains("Processes", output);
        Assert.Contains("2 items", output);
    }

    [Fact]
    public void OnDraw_Omits_The_Border_When_ShowBorder_Is_False()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.ShowBorder = false;
        listView.HeaderText = "Processes";
        listView.FooterText = "2 items";
        listView.Draw();

        string output = terminal.Output;

        Assert.DoesNotContain("╭", output); // no top-left corner
        Assert.DoesNotContain("Processes", output);
        Assert.DoesNotContain("2 items", output);
    }

    [Fact]
    public void Should_Draw_Header_And_Items()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        // Confirm double buffering is used. 
        Assert.Equal(1, terminal.WriteSpanCalls);
        Assert.Equal(0, terminal.WriteCharCalls);
        Assert.Equal(0, terminal.WriteStringCalls);
        Assert.Equal(0, terminal.SetCursorPositionCalls);
        Assert.Equal(0, terminal.ForegroundColorSets);
        Assert.Equal(0, terminal.BackgroundColorSets);

        string output = terminal.Output;
        Assert.Contains("Header 0", output);
        Assert.Contains("Header 1", output);
        Assert.Contains("Item 0", output);
        Assert.Contains("0 SubItem1", output);
        Assert.Contains("Item 1", output);
        Assert.Contains("1 SubItem1", output);
    }

    [Fact]
    public void Should_Raise_ItemSelected_EventHandler()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item0 = new("Item 0");
        ListViewItem item1 = new("Item 1");
        
        listView.Items.Add(item0);
        listView.Items.Add(item1);
        listView.SelectedIndex = 1;

        Mock<EventHandler<ListViewItemEventArgs>> mockHandler = new();
        listView.ItemSelected += mockHandler.Object;        

        // Enter key should raise ItemSelected event.
        bool handled = false;
        listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);
        
        mockHandler.Verify(
            handler => handler(
                It.IsAny<object>(),
                It.Is<ListViewItemEventArgs>(args => args.Item == item1)));
    }
    
    [Fact]
    public void Should_Raise_ItemClicked_EventHandler()
    {
        ListViewControl listView = GetDefaultListView();
        ListViewItem item0 = new("Item 0");
        ListViewItem item1 = new("Item 1");
        
        listView.Items.Add(item0);
        listView.Items.Add(item1);
        listView.SelectedIndex = 0;

        Mock<EventHandler<ListViewItemEventArgs>> mockHandler = new();
        listView.ItemClicked += mockHandler.Object;        

        // Arrow key should raise ItemClicked event.
        bool handled = false;
        listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        
        mockHandler.Verify(
            handler => handler(
                It.IsAny<object>(),
                It.Is<ListViewItemEventArgs>(args => args.Item == item1)));
    }

    // ---- Selection highlight vs. custom sub-item backgrounds --------------------------------

    private static readonly Color RowBackground  = ConsolePalette.Black;
    private static readonly Color RowForeground  = ConsolePalette.White;
    private static readonly Color CellBackground = ConsolePalette.Red;
    private static readonly Color CellForeground = ConsolePalette.Yellow;

    // The slice of the ANSI output for one screen row: from that row's cursor-move to the next.
    private static string RowSegment(string output, int screenRow)
    {
        int start = output.IndexOf($"{Esc}[{screenRow + 1};", StringComparison.Ordinal);
        Assert.True(start >= 0, $"row {screenRow} not found in output");

        int next = output.IndexOf($"{Esc}[{screenRow + 2};", start, StringComparison.Ordinal);
        return next >= 0 ? output[start..next] : output[start..];
    }

    private static ListViewControl BuildColourfulListView(
        RecordingTerminal terminal,
        bool checkboxes = false)
    {
        ListViewControl listView = new(terminal) {
            Width = 40,
            Height = 10,
            X = 0,
            Y = 0,
            BackgroundColour = RowBackground,
            ForegroundColour = RowForeground,
            ShowBorder = false,
            ShowColumnHeaders = false,
            ShowCheckboxes = checkboxes
        };

        listView.ColumnHeaders.AddRange(new[] {
            new ListViewColumnHeader("A") { Width = 10 },
            new ListViewColumnHeader("B") { Width = 10 }
        });

        listView.Items.AddRange(new[] {
            new ListViewItem(new[] { "plain", "hot" }),
            new ListViewItem(new[] { "other", "cool" })
        });

        return listView;
    }

    [Fact]
    public void Selected_Row_Keeps_A_Sub_Item_That_Set_Its_Own_Background()
    {
        ListViewControl listView = BuildColourfulListView(terminal);
        listView.Items[0].SubItems[1].BackgroundColor = CellBackground;
        listView.Items[0].SubItems[1].ForegroundColor = CellForeground;
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 0);

        // The custom cell survives the selection band...
        Assert.Contains(Bg(CellBackground), row);
        Assert.Contains(Fg(CellForeground), row);
        // ...while the untouched cell and the trailing filler take the unfocused highlight.
        Assert.Contains(Bg(ConsolePalette.Gray), row);
    }

    [Fact]
    public void Selected_Row_Does_Not_Preserve_A_Sub_Item_That_Only_Changed_Its_Foreground()
    {
        // The rule is background-only: a cell that kept the list's background but changed its
        // foreground is still folded into the highlight, so the selected row stays legible.
        // Background is set explicitly to the row's own default first, the way every real
        // consumer touches it, so the sub-item's style carries a resolved background rather than
        // falling through to ListViewSubItem's uninitialised style default.
        ListViewControl listView = BuildColourfulListView(terminal);
        ListViewSubItem cell = listView.Items[0].SubItems[1];
        cell.BackgroundColor = RowBackground;
        cell.ForegroundColor = CellForeground;
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 0);

        Assert.DoesNotContain(Fg(CellForeground), row);
        Assert.Contains(Bg(ConsolePalette.Gray), row);
    }

    [Fact]
    public void Selected_Row_Uses_The_Focused_Highlight_For_Its_Default_Cells()
    {
        ListViewControl listView = BuildColourfulListView(terminal);
        listView.BackgroundHighlightColour = ConsolePalette.Blue;
        listView.ForegroundHighlightColour = ConsolePalette.Green;
        listView.Focused = true;
        listView.Items[0].SubItems[1].BackgroundColor = CellBackground;
        listView.Items[0].SubItems[1].ForegroundColor = CellForeground;
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 0);

        Assert.Contains(Bg(ConsolePalette.Blue), row);   // default cell + filler
        Assert.Contains(Fg(ConsolePalette.Green), row);
        Assert.Contains(Bg(CellBackground), row);        // custom cell preserved
        Assert.Contains(Fg(CellForeground), row);
    }

    [Fact]
    public void Selected_Row_With_No_Custom_Cells_Is_Highlighted_End_To_End()
    {
        ListViewControl listView = BuildColourfulListView(terminal);
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 0);

        Assert.Contains(Bg(ConsolePalette.Gray), row);
        Assert.Contains(Fg(ConsolePalette.Black), row);
        // No cell paints itself in the list background: the whole strip is the highlight.
        Assert.DoesNotContain(Bg(RowBackground), row);
    }

    [Fact]
    public void Unselected_Row_Is_Never_Highlighted_Even_With_A_Custom_Cell()
    {
        ListViewControl listView = BuildColourfulListView(terminal);
        listView.Items[1].SubItems[1].BackgroundColor = CellBackground;
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 1);

        Assert.Contains(Bg(CellBackground), row);
        Assert.DoesNotContain(Bg(ConsolePalette.Gray), row);
    }

    [Fact]
    public void Checkbox_Gutter_Follows_The_Selection_Highlight()
    {
        ListViewControl listView = BuildColourfulListView(terminal, checkboxes: true);

        // Every cell sets its own background, so the highlight can only come from the gutter or filler.
        listView.Items[0].SubItems[0].BackgroundColor = CellBackground;
        listView.Items[0].SubItems[1].BackgroundColor = CellBackground;
        listView.Draw();

        string row = RowSegment(terminal.Output, screenRow: 0);

        Assert.Contains(Bg(ConsolePalette.Gray), row);
        Assert.Contains(Bg(CellBackground), row);
    }

    // ---- Right-border scroll indicators ------------------------------------------------------
    //
    // RowCount for this geometry (Width=40, Height=10, ShowBorder=true, ShowColumnHeaders=false):
    // inset=1, Bounds.Height = Height-2 = 8, RowCount = Bounds.Height-1 = 7. Ten items overflow
    // seven visible rows, giving CurrentPageIndex a range of 0..3 to move through while paging.

    private const string Up = "▲";
    private const string Down = "▼";

    private static ListViewControl BuildOverflowingListView(
        RecordingTerminal terminal,
        int itemCount = 10,
        bool showBorder = true,
        bool enableScroll = true)
    {
        ListViewControl listView = new(terminal) {
            Width = 40,
            Height = 10,
            X = 0,
            Y = 0,
            ShowBorder = showBorder,
            ShowColumnHeaders = false,
            EnableScroll = enableScroll
        };

        listView.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty) { Width = 38 });

        for (int i = 0; i < itemCount; i++) {
            listView.Items.Add(new ListViewItem($"Item {i}"));
        }

        return listView;
    }

    [Fact]
    public void Shows_Only_The_Down_Indicator_When_Scrolled_To_The_Top()
    {
        ListViewControl listView = BuildOverflowingListView(terminal);
        listView.Draw();

        string output = terminal.Output;

        Assert.Contains(Down, output);
        Assert.DoesNotContain(Up, output);
    }

    [Fact]
    public void Paints_The_Indicator_Cell_Exactly_Once_Per_Full_Draw()
    {
        // A full redraw must never draw the plain border character and then immediately overwrite
        // it with the arrow glyph at the same cell - that double-write is what caused the flicker.
        ListViewControl listView = BuildOverflowingListView(terminal);
        listView.Draw();

        string output = terminal.Output;
        Assert.Contains(Down, output); // sanity: this draw does show the down arrow

        // Row 8 (1-based), column 40 (1-based): the down-arrow cell for this geometry (Bounds.Y=1,
        // RowCount=7, so bottomRow=7 -> screen row 7 -> 1-based 8; X+Width-1=39 -> 1-based 40).
        string move = $"{Esc}[8;40H";
        int firstIndex = output.IndexOf(move, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, "expected the down-arrow cell to be addressed");

        int secondIndex = output.IndexOf(move, firstIndex + 1, StringComparison.Ordinal);
        Assert.Equal(-1, secondIndex);
    }

    [Fact]
    public void Shows_Both_Indicators_On_A_Middle_Page()
    {
        ListViewControl listView = BuildOverflowingListView(terminal);
        listView.Draw(); // establishes viewPort.RowCount before any key-driven paging

        bool handled = false;
        for (int i = 0; i < 7; i++) {
            listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        }

        terminal.Reset();
        listView.Draw();
        string output = terminal.Output;

        Assert.Contains(Up, output);
        Assert.Contains(Down, output);
    }

    [Fact]
    public void Shows_Only_The_Up_Indicator_When_Scrolled_To_The_Bottom()
    {
        ListViewControl listView = BuildOverflowingListView(terminal);
        listView.Draw(); // establishes viewPort.RowCount before any key-driven paging

        bool handled = false;
        for (int i = 0; i < 9; i++) {
            listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        }

        terminal.Reset();
        listView.Draw();
        string output = terminal.Output;

        Assert.Contains(Up, output);
        Assert.DoesNotContain(Down, output);
    }

    [Fact]
    public void Shows_Neither_Indicator_When_Every_Item_Fits()
    {
        ListViewControl listView = BuildOverflowingListView(terminal, itemCount: 3);
        listView.Draw();

        string output = terminal.Output;

        Assert.DoesNotContain(Up, output);
        Assert.DoesNotContain(Down, output);
    }

    [Fact]
    public void Shows_Neither_Indicator_Without_A_Border()
    {
        ListViewControl listView = BuildOverflowingListView(terminal, showBorder: false);
        listView.Draw();

        string output = terminal.Output;

        Assert.DoesNotContain(Up, output);
        Assert.DoesNotContain(Down, output);
    }

    [Fact]
    public void Shows_Neither_Indicator_When_Scrolling_Is_Disabled()
    {
        ListViewControl listView = BuildOverflowingListView(terminal, enableScroll: false);
        listView.Draw();

        string output = terminal.Output;

        Assert.DoesNotContain(Up, output);
        Assert.DoesNotContain(Down, output);
    }

    [Fact]
    public void Clears_The_Up_Indicator_After_Paging_Back_To_The_Top()
    {
        ListViewControl listView = BuildOverflowingListView(terminal);
        listView.Draw(); // establishes viewPort.RowCount before any key-driven paging

        bool handled = false;

        // Reach the bottom (CurrentPageIndex = 3, only the up indicator showing) ...
        for (int i = 0; i < 9; i++) {
            listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        }

        // ... then page all the way back to the top.
        listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.PageUp), ref handled);
        listView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.PageUp), ref handled);

        terminal.Reset();
        listView.Draw();
        string output = terminal.Output;

        // A buggy implementation that only ever paints an indicator (never repaints the plain
        // border character back over it) would leave the stale ▲ from the bottom-of-list state.
        Assert.DoesNotContain(Up, output);
        Assert.Contains(Down, output);
    }
}
