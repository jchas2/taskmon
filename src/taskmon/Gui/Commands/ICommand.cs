namespace Task.Monitor.Gui.Commands;

public interface ICommand
{
    void Execute();
    bool IsEnabled { get; }
}

