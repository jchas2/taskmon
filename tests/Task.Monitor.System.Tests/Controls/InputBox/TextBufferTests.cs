using Task.Monitor.System.Controls.InputBox;

namespace Task.Monitor.System.Tests.Controls.InputBox;

public sealed class TextBufferTests
{
    [Fact]
    public void TextBuffer_Initial_State_Correct()
    {
        TextBuffer buffer = new();
        
        Assert.Equal(0, buffer.Length);
        Assert.Equal(0, buffer.CursorBufferPosition);
        Assert.Equal(string.Empty, buffer.Text);
        Assert.True(buffer.InsertMode);
    }

    [Fact]
    public void Clear_Should_Reset_Buffer_And_Cursor_Position()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        buffer.Add('b');
        buffer.MoveLeft();
        buffer.Clear();

        Assert.Equal(0, buffer.Length);
        Assert.Equal(0, buffer.CursorBufferPosition);
        Assert.Equal(string.Empty, buffer.Text);
    }
    
    [Fact]
    public void Add_In_Insert_Mode_InsertsCharacterAndMovesCursor()
    {
        TextBuffer buffer = new();
        buffer.Add('h');
        buffer.Add('l');
        buffer.MoveLeft();
        buffer.Add('e');

        Assert.Equal(3, buffer.Length);
        Assert.Equal(2, buffer.CursorBufferPosition);
        Assert.Equal("hel", buffer.Text);
    }

    [Fact]
    public void Add_In_Overwrite_Mode_Overwrites_Character_And_Moves_Cursor()
    {
        TextBuffer buffer = new();
        buffer.Add('h');
        buffer.Add('e');
        buffer.Add('l');
        buffer.Add('o');
        buffer.MoveLeft();
        buffer.MoveLeft();
        buffer.InsertMode = false;
        buffer.Add('l');

        Assert.Equal(4, buffer.Length);
        Assert.Equal(3, buffer.CursorBufferPosition);
        Assert.Equal("helo", buffer.Text);
    }

    [Fact]
    public void Add_In_Overwrite_Mode_At_End_Appends_Character()
    {
        TextBuffer buffer = new();
        buffer.Add('h');
        buffer.Add('e');
        buffer.InsertMode = false;
        buffer.Add('l');

        Assert.Equal(3, buffer.Length);
        Assert.Equal(3, buffer.CursorBufferPosition);
        Assert.Equal("hel", buffer.Text);
    }

    [Fact]
    public void Add_Control_Character_Returns_False_And_Does_Not_Modify_Buffer()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        int initialLength = buffer.Length;
        int initialCursor = buffer.CursorBufferPosition;
        bool result = buffer.Add('\n');

        Assert.False(result);
        Assert.Equal(initialLength, buffer.Length);
        Assert.Equal(initialCursor, buffer.CursorBufferPosition);
    }

    [Fact]
    public void Move_Backwards_From_Non_Zero_Position_Removes_Character_And_Moves_Cursor()
    {
        TextBuffer buffer = new();
        buffer.Add('H');
        buffer.Add('i');
        bool result = buffer.MoveBackwards();

        Assert.True(result);
        Assert.Equal(1, buffer.Length);
        Assert.Equal(1, buffer.CursorBufferPosition);
        Assert.Equal("H", buffer.Text);
    }

    [Fact]
    public void Move_Backwards_From_Zero_Position_Returns_False_And_Does_Nothing()
    {
        TextBuffer buffer = new();
        buffer.Add('H');
        buffer.MoveLeft();
        bool result = buffer.MoveBackwards();

        Assert.False(result);
        Assert.Equal(1, buffer.Length);
        Assert.Equal(0, buffer.CursorBufferPosition);
        Assert.Equal("H", buffer.Text);
    }

    [Fact]
    public void Delete_From_Middle_Removes_Character()
    {
        TextBuffer buffer = new();
        buffer.Add('h');
        buffer.Add('e');
        buffer.Add('l');
        buffer.Add('l');
        buffer.Add('o');
        buffer.MoveLeft();
        buffer.MoveLeft();
        bool result = buffer.Delete();

        Assert.True(result);
        Assert.Equal(4, buffer.Length);
        Assert.Equal("helo", buffer.Text);
        Assert.Equal(3, buffer.CursorBufferPosition);
    }

    [Fact]
    public void Delete_From_End_Of_Buffer_Returns_False_And_Does_Nothing()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        buffer.Add('b');
        bool result = buffer.Delete();

        Assert.False(result);
        Assert.Equal(2, buffer.Length);
        Assert.Equal(2, buffer.CursorBufferPosition);
        Assert.Equal("ab", buffer.Text);
    }

    [Fact]
    public void Move_Left_From_Non_Zero_Position_Moves_Cursor()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        buffer.Add('b');
        bool result = buffer.MoveLeft();

        Assert.True(result);
        Assert.Equal(1, buffer.CursorBufferPosition);
    }

    [Fact]
    public void Move_Left_From_Zero_Position_Returns_False_And_Does_Nothing()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        buffer.MoveLeft();
        int initialCursor = buffer.CursorBufferPosition;
        bool result = buffer.MoveLeft();

        Assert.False(result);
        Assert.Equal(initialCursor, buffer.CursorBufferPosition);
    }

    [Fact]
    public void Move_Right_From_Non_End_Position_Moves_Cursor()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        buffer.Add('b');
        buffer.MoveLeft();
        bool result = buffer.MoveRight();

        Assert.True(result);
        Assert.Equal(2, buffer.CursorBufferPosition);
    }

    [Fact]
    public void Move_Right_From_End_Position_Returns_False_And_Does_Nothing()
    {
        TextBuffer buffer = new();
        buffer.Add('a');
        int initialCursor = buffer.CursorBufferPosition;
        bool result = buffer.MoveRight();

        Assert.False(result);
        Assert.Equal(initialCursor, buffer.CursorBufferPosition);
    }
}