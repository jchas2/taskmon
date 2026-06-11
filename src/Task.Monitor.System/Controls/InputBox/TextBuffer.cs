using System.Text;

namespace Task.Monitor.System.Controls.InputBox;

public class TextBuffer
{
    private StringBuilder buffer = new();
    private int cursorBufferPosition = 0;
    
    public int CursorBufferPosition => cursorBufferPosition;
    
    public int Length => buffer.Length;
    
    public string Text => buffer.ToString();
    
    public void Clear()
    {
        buffer.Clear();
        cursorBufferPosition = 0;
    }

    public bool MoveBackwards()
    {
        if (cursorBufferPosition == 0) {
            return false;
        }
        
        buffer.Remove(cursorBufferPosition - 1, 1);
        cursorBufferPosition--;
        
        return true;
    }

    public bool Delete()
    {
        if (cursorBufferPosition == buffer.Length) {
            return false;
        }

        buffer.Remove(cursorBufferPosition, 1);
        
        return true;
    }

    public bool MoveLeft()
    {
        if (cursorBufferPosition == 0) {
            return false;
        }
        
        cursorBufferPosition--;

        return true;
    }

    public bool MoveRight()
    {
        if (cursorBufferPosition == buffer.Length) {
            return false;
        }

        cursorBufferPosition++;
        
        return true;
    }

    public bool InsertMode { get; set; } = true;

    public bool Add(char ch)
    {
        if (char.IsControl(ch)) {
            return false;
        }
        
        if (InsertMode) {
            buffer.Insert(cursorBufferPosition, ch);
            cursorBufferPosition++;
        }
        else {
            if (cursorBufferPosition < buffer.Length) {
                buffer[cursorBufferPosition] = ch;
                cursorBufferPosition++; 
            }
            else {
                // Overwrite at the end behaves like an insert.
                buffer.Append(ch);
                cursorBufferPosition++;
            }
        }

        return true;
    }
}
