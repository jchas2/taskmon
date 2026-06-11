namespace Task.Monitor.Commands;

public abstract class AbstractCommand(string text) : ICommand
{
    public virtual void Execute() => throw new NotImplementedException();
    public virtual bool IsEnabled { get; } = false;

    public string Text
    {
        get => text;
        protected set { text = value; }
    }
}

