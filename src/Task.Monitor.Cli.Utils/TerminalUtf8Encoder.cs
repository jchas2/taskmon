using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;

namespace Task.Monitor.Cli.Utils;

public sealed class TerminalUtf8Encoder : IDisposable
{
    private Encoding? origOutputEncoding;
    private Encoding? origInputEncoding;

    public TerminalUtf8Encoder()
    {
        UseTryCatch(() => {
            origOutputEncoding = Console.OutputEncoding;
            origInputEncoding = Console.InputEncoding;
        });
    }

    public void Dispose()
    {
        UseTryCatch(() => {
            Console.OutputEncoding = origOutputEncoding ?? Console.OutputEncoding;
            Console.InputEncoding = origInputEncoding ?? Console.InputEncoding;
        });
    }

    private void UseTryCatch(Action action)
    {
        try {
            action();
        }
        catch (Exception ex) when (ex is IOException || ex is SecurityException) {
            ExceptionHelper.HandleException(ex);
        }
    }
}
