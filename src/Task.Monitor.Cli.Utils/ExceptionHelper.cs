using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Task.Monitor.Cli.Utils;

public static class ExceptionHelper
{
    // Log diagnostic information for Release builds and use Asserts for Debug builds.
    public static void HandleException(Exception ex) => HandleException(ex, string.Empty);
    
    public static void HandleException(Exception ex, string message)
    {
        if (string.IsNullOrEmpty(message)) {
            message = ex.Message;
        }
        
        Trace.TraceError($"An exception occurred. Message: {message}");
        Trace.TraceError($"Exception: {ex}");
    }

    public static void HandleWaitAllException(AggregateException aggEx)
    {
        foreach (Exception ex in aggEx.InnerExceptions) {
            if (ex is not OperationCanceledException) {
                HandleException(ex, "An exception occurred while stopping worker Tasks");
            }
        }
    }
}
