using System.Drawing;
using Task.Monitor.System;

using Task.Monitor.Cli.Utils;
namespace Task.Monitor.System.Tests.Controls;

public sealed class ForwardingTerminal(ISystemTerminal inner) : ISystemTerminal
{
    // ReadOnlySpan<T> can only exist on the stack; the Moq .Setup() functions will not support the below
    // Write() function. This class will simply convert the chars to a string and pass to the inner mock Write(string). 
    public void Write(ReadOnlySpan<char> chars) => inner.Write(chars.ToString());

    public Color BackgroundColor { get => inner.BackgroundColor; set => inner.BackgroundColor = value; }
    public int CursorLeft { get => inner.CursorLeft; set => inner.CursorLeft = value; }
    public int CursorTop { get => inner.CursorTop; set => inner.CursorTop = value; }
    public bool CursorVisible { get => inner.CursorVisible; set => inner.CursorVisible = value; }
    public Color ForegroundColor { get => inner.ForegroundColor; set => inner.ForegroundColor = value; }
    public bool KeyAvailable => inner.KeyAvailable;
    public TextWriter StdError => inner.StdError;
    public TextReader StdIn => inner.StdIn;
    public TextWriter StdOut => inner.StdOut;
    public int WindowWidth => inner.WindowWidth;
    public int WindowHeight => inner.WindowHeight;

    public void EnableAnsiTerminalCodes() => inner.EnableAnsiTerminalCodes();
    public void Clear() => inner.Clear();
    public ConsoleKeyInfo ReadKey() => inner.ReadKey();
    public void SetCursorPosition(int left, int top) => inner.SetCursorPosition(left, top);
    public void Write(char ch) => inner.Write(ch);
    public void Write(string message) => inner.Write(message);
    public void WriteEmptyLine() => inner.WriteEmptyLine();
    public void WriteEmptyLineTo(int x) => inner.WriteEmptyLineTo(x);
    public void WriteLine(char ch) => inner.WriteLine(ch);
    public void WriteLine(string message) => inner.WriteLine(message);
}
