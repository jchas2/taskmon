#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

internal sealed class SmcThermalSampler : IDisposable
{
    private const string FanCountKey = "FNum";
    private const int    MaximumFans = 10;

    private readonly Lock @lock = new();

    private readonly List<(string Key, SmcThermalKeys.Group Group)> temperatureKeys = new();

    private Smc? smc;
    private int  fanCount;
    private bool openFailed;

    public void Dispose()
    {
        lock (@lock) {
            smc?.Dispose();
            smc        = null;
            fanCount   = 0;
            openFailed = false;
            temperatureKeys.Clear();
        }
    }

    public bool EnsureOpen()
    {
        lock (@lock) {
            if (smc is not null) {
                return true;
            }

            if (openFailed) {
                return false;
            }

            smc = Smc.Open();

            if (smc is null) {
                openFailed = true;
                return false;
            }

            bool appleSilicon = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

            foreach (string key in smc.ReadKeys()) {
                if (SmcThermalKeys.Classify(key, appleSilicon) is { } group) {
                    temperatureKeys.Add((key, group));
                }
            }

            fanCount = Math.Clamp((int)(smc.ReadValue(FanCountKey) ?? 0), 0, MaximumFans);
            return true;
        }
    }

    public bool HasTemperatures
    {
        get {
            lock (@lock) {
                return temperatureKeys.Count > 0;
            }
        }
    }

    public List<FanReading> ReadFans()
    {
        lock (@lock) {
            List<FanReading> fans = new();

            for (int i = 0; smc is not null && i < fanCount; i++) {
                if (smc.ReadValue($"F{i}Ac") is not { } rpm) {
                    continue;
                }

                fans.Add(new FanReading {
                    Name   = $"Fan {i + 1}",
                    Rpm    = rpm,
                    MinRpm = smc.ReadValue($"F{i}Mn") ?? 0,
                    MaxRpm = smc.ReadValue($"F{i}Mx") ?? 0
                });
            }

            return fans;
        }
    }

    public List<(SmcThermalKeys.Group Group, double Celsius)> ReadTemperatures()
    {
        lock (@lock) {
            List<(SmcThermalKeys.Group Group, double Celsius)> readings = new();

            foreach ((string key, SmcThermalKeys.Group group) in temperatureKeys) {
                if (smc?.ReadValue(key) is { } celsius) {
                    readings.Add((group, celsius));
                }
            }

            return readings;
        }
    }
}
#endif
