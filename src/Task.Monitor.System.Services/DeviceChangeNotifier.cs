namespace Task.Monitor.System.Services;

public enum DeviceCategory
{
    Storage,
    Network,
    Gpu,
}

/// <summary>
/// Watches for plug and play device interface arrival and removal and raises
/// <see cref="DeviceChanged"/> within milliseconds, so a service can re-enumerate its hardware
/// immediately instead of waiting for the next poll. Windows only; a no-op on other platforms,
/// where services fall back to their normal sampling cadence.
/// </summary>
public sealed partial class DeviceChangeNotifier : IDisposable
{
    public event Action<DeviceCategory>? DeviceChanged;

    public void Start() => OnStartPlatform();

    public void Dispose() => OnStopPlatform();

    partial void OnStartPlatform();

    partial void OnStopPlatform();

    private void RaiseDeviceChanged(DeviceCategory category)
    {
        try {
            DeviceChanged?.Invoke(category);
        }
        catch {
            // A subscriber must never throw back across the native notification callback.
        }
    }
}
