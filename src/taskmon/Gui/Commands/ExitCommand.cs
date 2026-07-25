namespace Task.Monitor.Gui.Commands;

public sealed class ExitCommand(string text) : AbstractCommand(text)
{
    // This command is just a placeholder to be added to the CommandControl for painting the 
    // function key commands at the base of the main screen. The ScreenApplication main loop
    // handles the function key processing.
    public override void Execute() { }
    public override bool IsEnabled => true;
}