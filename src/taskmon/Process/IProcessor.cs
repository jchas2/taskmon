namespace Task.Monitor.Process;

public interface IProcessor
{
    event EventHandler<ProcessorEventArgs> ProcessorUpdated;
    public int Delay { get; set; }
    public bool IrixMode { get; set; }
    public bool IsRunning { get; }
    public int IterationLimit { get; set; }
    public int ProcessCount { get; }
    public void Run();
    public void Stop();
    public int ThreadCount { get; }

}