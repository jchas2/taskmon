namespace Task.Monitor.System.Services;

public interface ISystemService
{
    public ServiceStatus Status { get; }
    public void Start();
    public void Stop();
    
}