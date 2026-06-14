using System.Text;

namespace Task.Monitor.System.Tests.Controls;

public sealed class RecordingTerminal : ISystemTerminal
{
    private readonly StringBuilder output = new();

    public string Output => output.ToString();

    public int WriteSpanCalls { get; private set; }
    public int WriteCharCalls { get; private set; }
    public int WriteStringCalls { get; private set; }
    public int SetCursorPositionCalls { get; private set; }
    public int ForegroundColorSets { get; private set; }
    public int BackgroundColorSets { get; private set; }

    public void Reset()
    {
        output.Clear();
        WriteSpanCalls = 0;
        WriteCharCalls = 0;
        WriteStringCalls = 0;
        SetCursorPositionCalls = 0;
        ForegroundColorSets = 0;
        BackgroundColorSets = 0;
    }

    public void Write(ReadOnlySpan<char> chars)
    {
        WriteSpanCalls++;
        output.Append(chars);
    }

    public void Write(char ch)
    {
        WriteCharCalls++;
        output.Append(ch);
    }

    public void Write(string message)
    {
        WriteStringCalls++;
        output.Append(message);
    }

    public void SetCursorPosition(int left, int top) => SetCursorPositionCalls++;

    private ConsoleColor backgroundColor = ConsoleColor.Black;
    private ConsoleColor foregroundColor = ConsoleColor.White;

    public ConsoleColor BackgroundColor
    {
        get => backgroundColor;
        set { backgroundColor = value; BackgroundColorSets++; }
    }

    public ConsoleColor ForegroundColor
    {
        get => foregroundColor;
        set { foregroundColor = value; ForegroundColorSets++; }
    }

    public int CursorLeft { get; set; }
    public int CursorTop { get; set; }
    public bool CursorVisible { get; set; }
    public bool KeyAvailable => false;
    public TextWriter StdError => TextWriter.Null;
    public TextReader StdIn => TextReader.Null;
    public TextWriter StdOut => TextWriter.Null;
    public int WindowWidth => 80;
    public int WindowHeight => 24;

    public void EnableAnsiTerminalCodes() { }
    public void Clear() { }
    public ConsoleKeyInfo ReadKey() => default;
    public void WriteEmptyLine() { }
    public void WriteEmptyLineTo(int x) { }
    public void WriteLine(char ch) { }
    public void WriteLine(string message) { }
}
