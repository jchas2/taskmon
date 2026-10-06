namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __APPLE__
    private void OnStartProcessSpecs(ProcessSpecs specs)
    {
        specs.LogicalProcessorCount = Environment.ProcessorCount;
        specs.IrixMode = IrixMode;
    }
#endif
}