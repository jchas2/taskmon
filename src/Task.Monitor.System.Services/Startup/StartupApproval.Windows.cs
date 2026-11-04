#if __WIN32__
using Microsoft.Win32;

namespace Task.Monitor.System.Services.Startup;

internal readonly record struct StartupApproval(RegistryHive Hive, string KeyPath, string ValueName);
#endif
