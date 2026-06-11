using System.Drawing;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ViewPortTests
{
    [Fact]
    public void Should_Construct_ViewPort()
    {
        ViewPort viewPort = new();
    
        Assert.Equal(new Rectangle(0, 0, 0, 0), viewPort.Bounds);
        Assert.Equal(0, viewPort.CurrentIndex);
        Assert.Equal(0, viewPort.RowCount);
        Assert.Equal(0, viewPort.SelectedIndex);
        Assert.Equal(0, viewPort.PreviousSelectedIndex);
    }

    [Fact]
    public void Should_Reset_ViewPort()
    {
        ViewPort viewPort = new() {
            Bounds = new Rectangle(1, 1, 80, 24),
            CurrentIndex = 1,
            PreviousSelectedIndex = 2,
            RowCount = 24,
            SelectedIndex = 1
        };
        
        viewPort.Reset();
        
        Assert.Equal(new Rectangle(0, 0, 0, 0), viewPort.Bounds);
        Assert.Equal(0, viewPort.CurrentIndex);
        Assert.Equal(0, viewPort.RowCount);
        Assert.Equal(0, viewPort.SelectedIndex);
        Assert.Equal(0, viewPort.PreviousSelectedIndex);
    }
}