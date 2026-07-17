using System.Drawing;

namespace Task.Monitor.System;

public interface ISystemTerminal
{
    Color BackgroundColor { get; set; }
    int CursorLeft { set; }
    int CursorTop { set; }
    bool CursorVisible { get; set; }
    void EnableAnsiTerminalCodes();
    Color ForegroundColor { get; set; }
    bool KeyAvailable { get; }
    TextWriter StdError { get; }
    TextReader StdIn { get; }
    TextWriter StdOut { get; }
    void Clear();
    ConsoleKeyInfo ReadKey();
    void SetCursorPosition(int left, int top);
    int WindowWidth { get; }
    int WindowHeight { get; }
    void Write(char ch);
    void Write(char ch, int count);
    void Write(ReadOnlySpan<char> chars);
    void Write(string message);
    void WriteEmptyLine();
    void WriteEmptyLineTo(int x);
    void WriteLine(char ch);
    void WriteLine(string message);
}