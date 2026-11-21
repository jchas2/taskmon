namespace Task.Monitor.System.Services.Common;

internal interface IReadingProvider<T> : IDisposable
{
    string Diagnostic => string.Empty;

    void IDisposable.Dispose() { }

    string Name { get; }

    IEnumerable<T> Read();

    bool TryInitialise() => true;
}
