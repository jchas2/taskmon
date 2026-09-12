namespace Task.Monitor.System.Services.DiskSpace;

// A minimal, fakeable shape for the handful of DriveInfo fields ScanRootProvider actually needs.
// DriveInfo itself is sealed with no mockable members, so its filter/sort logic operates on this
// instead - the only place that ever constructs one from a real DriveInfo is ScanRootProvider's
// own enumeration method, keeping the untestable OS call in exactly one place.
public readonly record struct DriveCandidate(string RootPath, bool IsReady, DriveType DriveType);
