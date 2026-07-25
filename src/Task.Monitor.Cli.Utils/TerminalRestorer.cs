using System.Security;
using System.Text;

namespace Task.Monitor.Cli.Utils;

public sealed class TerminalRestorer : IDisposable
{
    private readonly Encoding? defaultInputEncoding;
    private readonly Encoding? defaultOutputEncoding;

    public TerminalRestorer()
    {
        try {
            defaultInputEncoding = Console.InputEncoding;
            defaultOutputEncoding = Console.OutputEncoding;
        }
        catch (Exception ex) when (ex is IOException || ex is SecurityException) { }
    }
    
    public void Dispose()
    {
        // Restore encodings.
        try {
            if (defaultInputEncoding != null) {
                Console.InputEncoding = defaultInputEncoding;
            }

            if (defaultOutputEncoding != null) {
                Console.OutputEncoding = defaultOutputEncoding;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is SecurityException) { }

        // Restore terminal colours to their default state.
        Console.Out.Write(AnsiConsoleStringExtensions.Reset);   
    }
}
