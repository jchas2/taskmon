using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class FilterControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public FilterControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }
    
    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        FilterControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new FilterControl(null!, runContext.AppConfig));

    [Fact]
    public void Should_Draw_Control()
    {
        FilterControl ctrl = new(runContext.Terminal, runContext.AppConfig) {
            Width = 48,
            Height = 1
        };
        
        ctrl.Draw();
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("ENTER"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Done"))), Times.Once);
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("ESC"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Clear"))), Times.Once);
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Filter: "))), Times.Once);
        
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }
}
