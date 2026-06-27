using Moq;
using Task.Monitor.Commands;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

using System.Drawing;
using Task.Monitor.Cli.Utils;
namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CommandControlTests
{
    private class TestCommand(string text) : AbstractCommand(text)
    {
        public override void Execute() { }
        public override bool IsEnabled => true;
    }

    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public CommandControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }
    
    [Fact]                                                                                                                                        
    public void Should_Construct_Default()                                                                                                        
    {                                                                                                                                             
        CommandControl control = new(runContext.Terminal, runContext.AppConfig);                                                                  
                                                                                                                                                
        Assert.Equal(ConsolePalette.Black, control.BackgroundColour);                                                                               
        Assert.Equal(0, control.Controls.Count);                                                                                                    
        Assert.Empty(control.Controls);                                                                                                           
        Assert.Equal(ConsolePalette.White, control.ForegroundColour);                                                                               
        Assert.Equal(0, control.Height);                                                                                                          
        Assert.True(control.Visible);                                                                                                             
        Assert.Equal(0, control.Width);                                                                                                           
        Assert.Equal(0, control.X);                                                                                                               
        Assert.Equal(0, control.Y);                                                                                                               
    }                 
    
    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        CommandControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new CommandControl(null!, runContext.AppConfig));

    [Fact]
    public void Should_Draw_Command()
    {
        CommandControl ctrl = new(runContext.Terminal, runContext.AppConfig) {
            Width = 48,
            Height = 1
        };
        
        ctrl.AddCommand(ConsoleKey.F1, () => new TestCommand("Test"));
        ctrl.Draw();
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("F1 "))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Test   "))), Times.Once);
        
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }
}
