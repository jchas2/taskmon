using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.Tests.Common;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;

using System.Drawing;
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
        Assert.Equal(24, CanaryTestHelper.GetPropertyCount<ListViewControl>());
    
    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        ListViewControl listView = new(terminal.Object);
        
        Assert.Equal(ConsolePalette.Black, listView.BackgroundColour);
        Assert.Equal(ConsolePalette.White, listView.BackgroundHighlightColour);
        Assert.Empty(listView.ColumnHeaders);
        Assert.Empty(listView.Controls);
        Assert.Empty(listView.EmptyListViewText);
        Assert.True(listView.EnableRowSelect);
        Assert.True(listView.EnableScroll);
        Assert.Equal(ConsolePalette.White, listView.ForegroundColour);
        Assert.Equal(ConsolePalette.Cyan, listView.ForegroundHighlightColour);
        Assert.Equal(ConsolePalette.Black, listView.HeaderBackgroundColour);
        Assert.Equal(ConsolePalette.White, listView.HeaderForegroundColour);        
        Assert.Equal(0, listView.Height);
        Assert.Empty(listView.Items);
        Assert.NotNull(listView.Name);
        Assert.Null(listView.SelectedItem);
        Assert.Equal(0, listView.SelectedIndex); // TODO: This should be -1.
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
            EmptyListViewText = "No Items",
            EnableRowSelect = false,
            EnableScroll = false,
            ForegroundColour = ConsolePalette.Blue,
            ForegroundHighlightColour = ConsolePalette.DarkGray,
            HeaderBackgroundColour = ConsolePalette.Green,
            HeaderForegroundColour = ConsolePalette.Black,
            Height = 24,
            Visible =  true,
            Width = 80,
            X = 2,
            Y = 2
        };
        
        Assert.Equal(ConsolePalette.Gray, listView.BackgroundColour);
        Assert.Equal(ConsolePalette.DarkGray, listView.BackgroundHighlightColour);
        Assert.Equal("No Items", listView.EmptyListViewText);
        Assert.False(listView.EnableRowSelect);
        Assert.False(listView.EnableScroll);
        Assert.Equal(ConsolePalette.Blue, listView.ForegroundColour);
        Assert.Equal(ConsolePalette.DarkGray, listView.ForegroundHighlightColour);
        Assert.Equal(ConsolePalette.Green, listView.HeaderBackgroundColour);
        Assert.Equal(ConsolePalette.Black, listView.HeaderForegroundColour);
        Assert.Equal(24, listView.Height);
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

    [Fact]
    public void OnDraw_Renders_The_Header_In_Bold()
    {
        ListViewControl listView = CreatePopulatedListView(terminal);
        listView.Draw();

        Assert.Contains(Esc + "[1m", terminal.Output);
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
}
