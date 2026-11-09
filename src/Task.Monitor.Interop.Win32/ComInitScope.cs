namespace Task.Monitor.Interop.Win32;

// Balances CoInitializeEx with CoUninitialize on the calling thread. Every successful call needs
// one, including S_FALSE for a thread that was already initialised. A failed call, such as
// RPC_E_CHANGED_MODE for a thread in the other apartment model, must not get one. A default scope
// owns nothing, so it never uninitialises a thread it didn't initialise.
public readonly ref struct ComInitScope
{
    private readonly bool initialised;

    private ComInitScope(int hResult)
    {
        HResult     = hResult;
        initialised = hResult >= 0;
    }

    public void Dispose()
    {
        if (initialised) {
            Ole32.CoUninitialize();
        }
    }

    public int HResult { get; }

    public static ComInitScope Initialise(uint coInit) => new(Ole32.CoInitializeEx(nint.Zero, coInit));
}
