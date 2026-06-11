using Moq;

namespace Task.Monitor.System.Tests.Controls;

public static class TerminalMock
{
    public static Mock<ISystemTerminal> Setup() =>
        Setup(width: 80, height: 24);
    
    public static Mock<ISystemTerminal> Setup(int width, int height)
    {
        Mock<ISystemTerminal> terminal = new();
        terminal.Setup(t => t.WindowWidth).Returns(width);
        terminal.Setup(t => t.WindowHeight).Returns(height);
        
        return terminal;
    }
}
