using System.Runtime.Versioning;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Tests;

public sealed class SystemTerminalTests
{
    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Set_Streams()
    {
        var terminal = new SystemTerminal();
        Assert.True(terminal.StdError != null);
        Assert.True(terminal.StdOut != null);
        Assert.True(terminal.StdIn != null);
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Set_Colours()
    {
        var terminal = new SystemTerminal();
        var background = terminal.BackgroundColor;
        var foreground = terminal.ForegroundColor;
        
        // Switch up the values for a simple getter/setter test.
        terminal.BackgroundColor = foreground;
        Assert.True(terminal.BackgroundColor == foreground);
        
        terminal.ForegroundColor = background;
        Assert.True(terminal.ForegroundColor == background);
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Encode_Ansi_Colour_Codes()
    {
        var terminal = new SystemTerminal();

        // Initially just test no error is thrown.
        terminal.WriteLine("This should be Red".ToRed());
        terminal.WriteLine("This should be Green".ToGreen());
        terminal.WriteLine("This should be Blue".ToBlue());
    }
    
    // TODO: Writing test for the various Write@ functions will require setting the TextWriter on the Console.
}
