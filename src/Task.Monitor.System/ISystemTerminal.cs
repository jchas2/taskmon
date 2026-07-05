using System.Drawing;

namespace Task.Monitor.System;

public interface ISystemTerminal
{
    Color BackgroundColor { get; set; }
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
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
    // NOTE: ReadOnlySpan<char> is a ref struct, so Moq/Castle cannot generate a valid proxy for this
    // overload (it produces invalid IL that throws InvalidProgramException when first called). Never hand a
    // raw Mock<ISystemTerminal>.Object to drawing code; wrap it in ForwardingTerminal (or use RecordingTerminal).
    void Write(ReadOnlySpan<char> chars);
    void Write(string message);
    void WriteEmptyLine();
    void WriteEmptyLineTo(int x);
    void WriteLine(char ch);
    void WriteLine(string message);
}