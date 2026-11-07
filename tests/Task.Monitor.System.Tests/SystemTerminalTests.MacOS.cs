#if __APPLE__
namespace Task.Monitor.System.Tests;

public sealed partial class SystemTerminalTests
{
    private const string AppleTerminalPath = "/System/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal";

    [Fact]
    public void EnumerateAncestorPaths_Finds_This_Processes_Parent()
    {
        Assert.NotEmpty(SystemTerminal.EnumerateAncestorPaths().Take(1));
    }

    [Fact]
    public void IsAppleTerminal_Does_Not_Mistake_Another_App_Named_Terminal()
    {
        Assert.False(SystemTerminal.IsAppleTerminal(null, () => ["/Applications/MyTerminal.app/Contents/MacOS/Terminal"]));
    }

    [Fact]
    public void IsAppleTerminal_Finds_Terminal_Among_The_Parents_When_Sudo_Dropped_TERM_PROGRAM()
    {
        Assert.True(SystemTerminal.IsAppleTerminal(null, () => ["/usr/bin/sudo", "/bin/zsh", "/usr/bin/login", AppleTerminalPath]));
        Assert.True(SystemTerminal.IsAppleTerminal("",   () => [AppleTerminalPath]));
    }

    [Fact]
    public void IsAppleTerminal_Is_False_Without_Terminal_Among_The_Parents()
    {
        Assert.False(SystemTerminal.IsAppleTerminal(null, () => ["/bin/zsh", "/Applications/Ghostty.app/Contents/MacOS/ghostty"]));
    }

    [Theory]
    [InlineData("Apple_Terminal", true)]
    [InlineData("ghostty",        false)]
    [InlineData("WarpTerminal",   false)]
    [InlineData("tmux",           false)]
    public void IsAppleTerminal_Trusts_TERM_PROGRAM_When_Set(string termProgram, bool expected)
    {
        Assert.Equal(expected, SystemTerminal.IsAppleTerminal(termProgram, NoAncestorsExpected));
    }

    private static IEnumerable<string> NoAncestorsExpected() =>
        throw new InvalidOperationException("TERM_PROGRAM was set, so the parents shouldn't be read");
}
#endif
