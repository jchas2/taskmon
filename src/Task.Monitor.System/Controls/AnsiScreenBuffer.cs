using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls;

public sealed class AnsiScreenBuffer
{
    private const char Escape = '';

    private char[] buffer;
    private int length;
    private ConsoleColor foreground;
    
    private ConsoleColor background;
    private bool colourSet;

    public AnsiScreenBuffer(int capacity = 1024) =>
        buffer = new char[Math.Max(1, capacity)];

    public int Length => length;

    public ReadOnlySpan<char> AsSpan() => buffer.AsSpan(0, length);

    public void Clear()
    {
        length = 0;
        colourSet = false;
    }

    public void MoveTo(int left, int top)
    {
        if (left < 0) {
            left = 0;
        }

        if (top < 0) {
            top = 0;
        }

        Append(Escape);
        Append('[');
        AppendInt(top + 1);
        Append(';');
        AppendInt(left + 1);
        Append('H');
    }

    public void SetColour(ConsoleColor fg, ConsoleColor bg)
    {
        if (colourSet && fg == foreground && bg == background) {
            return;
        }

        Append(AnsiConsoleStringExtensions.GetBackgroundCode(bg));
        Append(AnsiConsoleStringExtensions.GetForegroundCode(fg));

        foreground = fg;
        background = bg;
        colourSet = true;
    }

    public void ResetColour()
    {
        Append(AnsiConsoleStringExtensions.Reset);
        colourSet = false;
    }

    public void Append(char ch)
    {
        EnsureCapacity(length + 1);
        buffer[length++] = ch;
    }

    public void Append(ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty) {
            return;
        }

        EnsureCapacity(length + chars.Length);
        chars.CopyTo(buffer.AsSpan(length));
        length += chars.Length;
    }

    public void Append(char ch, int count)
    {
        if (count <= 0) {
            return;
        }

        EnsureCapacity(length + count);
        buffer.AsSpan(length, count).Fill(ch);
        length += count;
    }

    private void AppendInt(int value)
    {
        if (value < 0) {
            value = 0;
        }

        Span<char> digits = stackalloc char[10];
        int pos = digits.Length;

        do {
            digits[--pos] = (char)('0' + value % 10);
            value /= 10;
        }
        while (value > 0);

        Append(digits[pos..]);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= buffer.Length) {
            return;
        }

        int newSize = buffer.Length * 2;

        while (newSize < required) {
            newSize *= 2;
        }

        Array.Resize(ref buffer, newSize);
    }
}
