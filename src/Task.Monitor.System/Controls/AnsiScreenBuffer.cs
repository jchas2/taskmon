using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls;

public sealed class AnsiScreenBuffer
{
    private const char Escape = '';

    private const string BoldOn = "[1m";
    private const string BoldOff = "[22m";

    private char[] buffer;
    private int length;
    private Color foreground;

    private Color background;
    private bool colourSet;
    private bool bold;

    public AnsiScreenBuffer(int capacity = 1024) =>
        buffer = new char[Math.Max(1, capacity)];

    public int Length => length;

    public ReadOnlySpan<char> AsSpan() => buffer.AsSpan(0, length);

    public void Clear()
    {
        length = 0;
        colourSet = false;
        bold = false;
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

    public void SetColour(Color fg, Color bg)
    {
        if (colourSet && fg.ToArgb() == foreground.ToArgb() && bg.ToArgb() == background.ToArgb()) {
            return;
        }

        AppendBackground(bg);
        AppendForeground(fg);

        foreground = fg;
        background = bg;
        colourSet = true;
    }

    private void AppendBackground(Color colour)
    {
        Append(Escape);
        Append('[');

        // Alpha 0 -> 49 (terminal default / transparent), else 48;2;r;g;b.
        if (colour.A == 0) {
            Append("49");
        }
        else if (ConsolePalette.PreferIndexedColours && ConsolePalette.TryGetAnsiIndex(colour, out int index)) {
            AppendInt(index < 8 ? 40 + index : 100 + (index - 8));
        }
        else {
            Append("48;2;");
            AppendInt(colour.R);
            Append(';');
            AppendInt(colour.G);
            Append(';');
            AppendInt(colour.B);
        }

        Append('m');
    }

    private void AppendForeground(Color colour)
    {
        Append(Escape);
        Append('[');

        // Alpha 0 -> 39 (terminal default foreground), else 38;2;r;g;b.
        if (colour.A == 0) {
            Append("39");
        }
        else if (ConsolePalette.PreferIndexedColours && ConsolePalette.TryGetAnsiIndex(colour, out int index)) {
            AppendInt(index < 8 ? 30 + index : 90 + (index - 8));
        }
        else {
            Append("38;2;");
            AppendInt(colour.R);
            Append(';');
            AppendInt(colour.G);
            Append(';');
            AppendInt(colour.B);
        }

        Append('m');
    }

    public void SetBold(bool enabled)
    {
        if (bold == enabled) {
            return;
        }

        Append(enabled ? BoldOn : BoldOff);
        bold = enabled;
    }

    public void ResetColour()
    {
        Append(AnsiConsoleStringExtensions.Reset);
        colourSet = false;
        bold = false;
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
