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

    // Seeds the buffer with an initial value - e.g. a default path a caller wants the user to be
    // able to accept or edit rather than type from scratch. Places the cursor at the end.
    public void SetText(string text)
    {
        buffer.Clear();
        buffer.Append(text);
        cursorBufferPosition = buffer.Length;
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
