namespace Task.Monitor.System.Services.Process;

// The qualitative power-usage rating Task Manager shows in place of a wattage. Windows' own value
// is uncalibrated, so this is derived from the process's CPU / GPU / disk activity - the same
// class of inputs the Energy Estimation Engine uses.
public enum ProcessPowerBucket
{
    VeryLow,
    Low,
    Moderate,
    High,
    VeryHigh
}
