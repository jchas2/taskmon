namespace Task.Monitor.System.Services;

public interface ISystemService
{
    public void Start();

    public ServiceStatus Status { get; }

    public void Stop();
    
}