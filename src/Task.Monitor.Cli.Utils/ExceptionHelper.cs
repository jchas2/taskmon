using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static class ExceptionHelper
{
    public static void LogException(Exception ex) => LogException(ex, string.Empty);
    
    public static void LogException(Exception ex, string message)
    {
        Trace.WriteLine($"--- EXCEPTION [{ex.GetType()}]");
        
        if (!string.IsNullOrEmpty(message)) {
            Trace.WriteLine($"Context = {message}");
        }

        TraceException(ex);
        Exception? innerEx = ex.InnerException;

        int maxExceptions = 64;
        int count = 0;
        
        while (innerEx != null && ++count < maxExceptions) {
            Trace.WriteLine($"--- INNER EXCEPTION [{ex.GetType()}]");
            TraceException(innerEx);
            innerEx = innerEx.InnerException;
        }
    }

    public static void HandleWaitAllException(AggregateException aggEx)
    {
        foreach (Exception ex in aggEx.InnerExceptions) {
            if (ex is not OperationCanceledException) {
                LogException(ex, "An exception occurred while stopping worker Tasks");
            }
        }
    }

    public static void HandleUnhandledException(UnhandledExceptionEventArgs ev)
    {
        if (ev.IsTerminating) {
            string error = "Runtime has encountered a fatal unhandled Exception.";
            OutputWriter.Error.WriteLine(Environment.NewLine + error.ToRed());
            Trace.WriteLine(error);
        }

        if (ev.ExceptionObject is Exception ex) {
            List<Exception> exceptions = new();
            
            if (ex is AggregateException) {
                exceptions.AddRange(((AggregateException)ex).Flatten().InnerExceptions);
            }
            else {
                exceptions.Add(ex);
            }
            
            foreach (Exception e in exceptions) {
                OutputWriter.Error.WriteLine(e.GetType().ToString().ToRed());
                OutputWriter.Error.WriteLine(e.Message.ToRed());
                OutputWriter.Error.WriteLine(e.StackTrace?? e.ToString().ToYellow());
                LogException(e);
            }

            return;
        }
        
        string unhandledError = $"Unhandled error: {ev.ExceptionObject.GetType().Name}";
        OutputWriter.Error.WriteLine(unhandledError.ToRed());
        Trace.WriteLine(unhandledError);
    }
    
    
    private static void TraceException(Exception ex)
    {
        Trace.WriteLine($"Message = {ex.Message}");
        Trace.WriteLine($"Source = {ex.Source ?? string.Empty}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Trace.WriteLine($"HResult = {ex.HResult}");
        }
        
        Trace.WriteLine($"StackTrace = {Environment.NewLine} {ex.StackTrace}");
        Trace.WriteLine(string.Empty);
    }
}
