using Moq;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewTests
{
    private readonly Mock<ISystemTerminal> terminal;

    public ListViewTests() =>
        terminal = TerminalMock.Setup();
    
    private Monitor.System.Controls.ListView.ListView GetDefaultListView()
    {
        Monitor.System.Controls.ListView.ListView listView = new(terminal.Object) {
            Width = 80,
            Height = 24,
            X = 0,
            Y = 0
        };
 
        return listView;
    }
    
    [Fact]
    public void ListView_Canary_Test() =>
        Assert.Equal(24, CanaryTestHelper.GetPropertyCount<Monitor.System.Controls.ListView.ListView>());
    
    [Fact]
    public void Should_Construct_Default()
    {
        Mock<ISystemTerminal> terminal = TerminalMock.Setup();
        Monitor.System.Controls.ListView.ListView listView = new(terminal.Object);
        
        Assert.Equal(ConsoleColor.Black, listView.BackgroundColour);
        Assert.Equal(ConsoleColor.White, listView.BackgroundHighlightColour);
        Assert.Empty(listView.ColumnHeaders);
        Assert.Empty(listView.Controls);
        Assert.Empty(listView.EmptyListViewText);
        Assert.True(listView.EnableRowSelect);
        Assert.True(listView.EnableScroll);
        Assert.Equal(ConsoleColor.White, listView.ForegroundColour);
        Assert.Equal(ConsoleColor.Cyan, listView.ForegroundHighlightColour);
        Assert.Equal(ConsoleColor.Black, listView.HeaderBackgroundColour);
        Assert.Equal(ConsoleColor.White, listView.HeaderForegroundColour);        
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
        Monitor.System.Controls.ListView.ListView listView = new(terminal.Object) {
            BackgroundColour = ConsoleColor.Gray,
            BackgroundHighlightColour = ConsoleColor.DarkGray,
            EmptyListViewText = "No Items",
            EnableRowSelect = false,
            EnableScroll = false,
            ForegroundColour = ConsoleColor.Blue,
            ForegroundHighlightColour = ConsoleColor.DarkGray,
            HeaderBackgroundColour = ConsoleColor.Green,
            HeaderForegroundColour = ConsoleColor.Black,
            Height = 24,
            Visible =  true,
            Width = 80,
            X = 2,
            Y = 2
        };
        
        Assert.Equal(ConsoleColor.Gray, listView.BackgroundColour);
        Assert.Equal(ConsoleColor.DarkGray, listView.BackgroundHighlightColour);
        Assert.Equal("No Items", listView.EmptyListViewText);
        Assert.False(listView.EnableRowSelect);
        Assert.False(listView.EnableScroll);
        Assert.Equal(ConsoleColor.Blue, listView.ForegroundColour);
        Assert.Equal(ConsoleColor.DarkGray, listView.ForegroundHighlightColour);
        Assert.Equal(ConsoleColor.Green, listView.HeaderBackgroundColour);
        Assert.Equal(ConsoleColor.Black, listView.HeaderForegroundColour);
        Assert.Equal(24, listView.Height);
        Assert.True(listView.Visible);
        Assert.Equal(80, listView.Width);
        Assert.Equal(2, listView.X);
        Assert.Equal(2, listView.Y);
    }
    
    [Fact]
    public void SelectedIndex_Throws_ArgumentOutOfRangeException_For_Invalid_Index()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        ListViewItem item = new ("Item 0");
        listView.Items.Add(item);

        Assert.Throws<ArgumentOutOfRangeException>(() => listView.SelectedIndex = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.SelectedIndex = 1);
    }
    
    [Fact]
    public void SelectedIndex_Sets_Correctly()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        listView.Items.Add(new ListViewItem("Item 0"));
        listView.Items.Add(new ListViewItem("Item 1"));
        listView.SelectedIndex = 1;

        Assert.Equal(1, listView.SelectedIndex);
    }
    
    [Fact]
     public void SelectedItem_Returns_Correct_Item()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();

        Assert.Equal(0, listView.ItemCount);
        
        listView.Items.Add(new ListViewItem("Item 0"));
        
        Assert.Equal(1, listView.ItemCount);
    }
    
    [Fact]
    public void Item_Remove_Should_Update_Item_Count()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        ListViewItem item = new("Item 0");
        listView.Items.Add(item);
        
        Assert.Equal(1, listView.ItemCount);
        
        listView.Items.Remove(item);
        
        Assert.Equal(0, listView.ItemCount);
    }

    [Fact]
    public void Get_Item_By_Index_Should_Return_Item()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        listView.Items.Add(new ListViewItem("Item 0"));
        
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.GetItemByIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => listView.GetItemByIndex(1));
    }
    
    [Fact]
    public void InsertItem_Inserts_At_Correct_Index()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 0"));
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 1"));
        listView.ClearColumnHeaders();

        Assert.Equal(0, listView.ColumnHeaderCount);
    }
    
    [Fact]
    public void ColumnHeaders_Add_Should_Update_ColumnHeader_Count()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 0"));
        listView.ColumnHeaders.Add(new ListViewColumnHeader("Header 1"));

        Assert.Equal(2, listView.ColumnHeaderCount);
    }

    [Fact]
    public void OnKeyPressed_Should_Return_False_With_No_Items()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();

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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
    
    [Fact]
    public void Should_Draw_Header_And_Items()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();

        ListViewColumnHeader header0 = new("Header 0") {
            Width = 16
        };
        ListViewColumnHeader header1 = new("Header 1") {
            Width = 32
        };
        
        listView.ColumnHeaders.AddRange(new[] { header0, header1 });
        
        listView.Items.AddRange(new[] {
            new ListViewItem("Item 0"),
            new ListViewItem("Item 1")
        });

        listView.Items[0].SubItems.Add(new ListViewSubItem(listView.Items[0], "0 SubItem1"));
        listView.Items[1].SubItems.Add(new ListViewSubItem(listView.Items[1], "1 SubItem1"));
        
        listView.Draw();        
        
        terminal.Verify(t => t.SetCursorPosition(0, 0), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Header 0"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Header 1"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Item 0"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("0 SubItem1"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Item 1"))), Times.Once);
        terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("1 SubItem1"))), Times.Once);
    }

    [Fact]
    public void Should_Raise_ItemSelected_EventHandler()
    {
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
        Monitor.System.Controls.ListView.ListView listView = GetDefaultListView();
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
