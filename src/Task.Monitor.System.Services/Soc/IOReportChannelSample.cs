#if __APPLE__
namespace Task.Monitor.System.Services.Soc;

internal sealed record IOReportChannelSample(
    string                                 Group,
    string                                 SubGroup,
    string                                 Name,
    long                                   Value,
    IReadOnlyList<(string Name, long Residency)> States);
#endif
