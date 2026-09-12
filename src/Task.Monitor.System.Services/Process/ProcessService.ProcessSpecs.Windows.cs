namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private void OnStartProcessSpecs(ProcessSpecs specs)
    {
        specs.LogicalProcessorCount = Environment.ProcessorCount;
        specs.IrixMode = IrixMode;
    }
#endif
}
