namespace Task.Monitor.Commands;

public interface ICommand
{
    void Execute();
    bool IsEnabled { get; }
}

