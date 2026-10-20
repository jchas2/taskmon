using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
namespace Task.Monitor.System.Tests.Controls;

public sealed class ForwardingTerminal(ISystemTerminal inner) : ISystemTerminal
{
    public Color BackgroundColor { get => inner.BackgroundColor; set => inner.BackgroundColor = value; }

    public void Clear() => inner.Clear();

    public int CursorLeft { set => inner.CursorLeft = value; }
    public int CursorTop { set => inner.CursorTop = value; }
    public bool CursorVisible { get => inner.CursorVisible; set => inner.CursorVisible = value; }

    public void EnableAnsiTerminalCodes() => inner.EnableAnsiTerminalCodes();

    public Color ForegroundColor { get => inner.ForegroundColor; set => inner.ForegroundColor = value; }
    public bool KeyAvailable => inner.KeyAvailable;

    public ConsoleKeyInfo ReadKey() => inner.ReadKey();
    public void SetCursorPosition(int left, int top) => inner.SetCursorPosition(left, top);

    public TextWriter StdError => inner.StdError;
    public TextReader StdIn => inner.StdIn;
    public TextWriter StdOut => inner.StdOut;

    public int WindowHeight => inner.WindowHeight;

    public int WindowWidth => inner.WindowWidth;

    // ReadOnlySpan<T> can only exist on the stack; the Moq .Setup() functions will not support the below
    // Write() function. This class will simply convert the chars to a string and pass to the inner mock Write(string). 
    public void Write(ReadOnlySpan<char> chars) => inner.Write(chars.ToString());

    public void Write(char ch) => inner.Write(ch);
    public void Write(char ch, int count) => inner.Write(ch, count);
    public void Write(string message) => inner.Write(message);
    public void WriteEmptyLine() => inner.WriteEmptyLine();
    public void WriteEmptyLineTo(int x) => inner.WriteEmptyLineTo(x);
    public void WriteLine(char ch) => inner.WriteLine(ch);
    public void WriteLine(string message) => inner.WriteLine(message);
}
