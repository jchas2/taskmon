namespace Task.Monitor.System.Services;

// One service's identity and its current lifecycle state, captured at snapshot time. The name is the
// short service name (the runtime type name with its "Service" suffix removed): "Cpu", "Memory", ...
public readonly record struct ServiceHealth(string Name, ServiceStatus Status);
