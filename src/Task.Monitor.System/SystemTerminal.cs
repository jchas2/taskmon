using System.Text;

namespace Task.Monitor.System;

public partial class SystemTerminal : ISystemTerminal
{
    private const int MaxStackChars = 256;
    
    public SystemTerminal()
    {
        Console.OutputEncoding = Encoding.UTF8;
        EnableAnsiTerminalCodesInternal();
    }

    public ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set => Console.BackgroundColor = value;
    }

    public int CursorLeft
    {
        get => Console.CursorLeft;
        set => Console.CursorLeft = value;
    }

    public int CursorTop
    {
        get => Console.CursorTop;
        set => Console.CursorTop = value;
    }

    public bool CursorVisible
    {
        get => CursorVisibleInternal;
        set => CursorVisibleInternal = value;
    }

    public void EnableAnsiTerminalCodes() => EnableAnsiTerminalCodesInternal();
    
    public ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }

    public bool KeyAvailable => Console.KeyAvailable;

    public TextWriter StdError => Console.Error;
    public TextReader StdIn => Console.In;
    public TextWriter StdOut => Console.Out;
    public void Clear() => Console.Clear();
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    public void SetCursorPosition(int left, int top)
    {
        // During Resize events this will throw on windows if we don't mitigate the difference
        // between window size and buffer size. Ensure left, top are within buffer range.
        left = Math.Clamp(left, 0, Math.Max(0, Console.BufferWidth - 1));
        top = Math.Clamp(top, 0, Math.Max(0, Console.BufferHeight - 1));

        try {
            Console.SetCursorPosition(left, top);
        }
        catch (ArgumentOutOfRangeException) { }
    }
    public int WindowWidth => Console.WindowWidth;
    public int WindowHeight => Console.WindowHeight;
    public void Write(char ch) => Console.Out.Write(ch);
    public void Write(ReadOnlySpan<char> chars) => Console.Out.Write(chars);
    public void Write(string message) => Console.Out.Write(message);
    public void WriteEmptyLine() => WriteEmptyLineTo(Console.WindowWidth);

    public void WriteEmptyLineTo(int x)
    {
        switch (x) {
            case <= 0:
                return;
            case <= MaxStackChars: {
                Span<char> buffer = stackalloc char[x];
                buffer.Fill(' ');
                Write(buffer);
                break;
            }
            default:
                Write(
                    string.Create(
                        x, 
                        x, 
                        static (span, _) => span.Fill(' ')));
                break;
        }
    }
    
    public void WriteLine(char ch) => Console.Out.WriteLine(ch);
    public void WriteLine(string message) => Console.Out.WriteLine(message);   
}