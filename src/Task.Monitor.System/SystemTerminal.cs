using System.Drawing;
using System.Text;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System;

// Simple Console wrapper that helps manage cross-platform issues with terminal support.
public partial class SystemTerminal : ISystemTerminal
{
    private const int MaxStackChars = 256;

    // Truecolor can't be read back from the terminal, so the current colours are
    // tracked here. They default to transparent (the terminal's own default).
    private Color backgroundColour = ConsolePalette.Transparent;
    private Color foregroundColour = ConsolePalette.Transparent;

    public SystemTerminal()
    {
        Console.OutputEncoding = Encoding.UTF8;
        EnableAnsiTerminalCodesInternal();
    }

    public Color BackgroundColor
    {
        get => backgroundColour;
        set
        {
            backgroundColour = value;
            Console.Out.Write(ConsolePalette.BackgroundSgr(value));
        }
    }

    public int CursorLeft
    {
        // See SetCursorPosition comments.
        set => Console.Out.Write($"\u001b[{value + 1}G");
    }

    public int CursorTop
    {
        // See SetCursorPosition comments.
        set => Console.Out.Write($"\u001b[{value + 1}d");
    }

    public bool CursorVisible
    {
        get => CursorVisibleInternal;
        set => CursorVisibleInternal = value;
    }

    public void EnableAnsiTerminalCodes() => EnableAnsiTerminalCodesInternal();
    
    public Color ForegroundColor
    {
        get => foregroundColour;
        set
        {
            foregroundColour = value;
            Console.Out.Write(ConsolePalette.ForegroundSgr(value));
        }
    }

    public bool KeyAvailable => Console.KeyAvailable;

    public TextWriter StdError => Console.Error;
    public TextReader StdIn => Console.In;
    public TextWriter StdOut => Console.Out;
    public void Clear() => Console.Clear();
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    public void SetCursorPosition(int left, int top)
    {
        if (left < 0) {
            left = 0;
        }

        if (top < 0) {
            top = 0;
        }

        // Note: Some terminals (like ghostty) rely on env var TERMINFO which gets stripped undo sudo on Unix
        // and the underlying Console.SetCursorPosition implementation cannot resolve terminal capabilities,
        // (using the terminfo db) causing addressing to misfire. This can cause the screen to "jump around".
        // We emit the raw ANSI codes here as we are only supporting ANSI aware terminals.
        Console.Out.Write($"\u001b[{top + 1};{left + 1}H");
    }
    public int WindowWidth => Console.WindowWidth;
    public int WindowHeight => Console.WindowHeight;
    public void Write(char ch) => Console.Out.Write(ch);

    public void Write(char ch, int count)
    {
        Span<char> buffer = stackalloc char[count];
        buffer.Fill(ch);
        Write(buffer);
    }
    
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