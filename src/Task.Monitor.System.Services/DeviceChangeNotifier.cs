using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services;

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
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }
}
