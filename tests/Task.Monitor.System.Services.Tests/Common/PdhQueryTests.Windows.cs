#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class PdhQueryTests
{
    // Present on every Windows machine: an instance per logical processor (in the first processor
    // group, on machines with more than 64) plus _Total.
    private const string ProcessorTimeCounterPath = @"\Processor(*)\% Processor Time";

    [Fact]
    public void Open_Returns_Null_For_A_Counter_That_Does_Not_Exist()
    {
        Assert.Null(PdhQuery.Open(nameof(PdhQueryTests), @"\No Such Object(*)\No Such Counter"));
    }

    [Fact]
    public unsafe void TryReadFormattedDoubles_Reads_Every_Instance_Into_The_Buffer()
    {
        using PdhQuery? query = PdhQuery.Open(nameof(PdhQueryTests), ProcessorTimeCounterPath);

        Assert.NotNull(query);

        // A rate counter needs two samples before it can format a value.
        Assert.True(query.Collect());
        Thread.Sleep(100);
        Assert.True(query.Collect());

        Assert.True(query.TryReadFormattedDoubles(query.Counters[0], out uint itemCount));
        Assert.True(itemCount >= 2, $"expected a processor and _Total, read {itemCount} instances");

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)query.Buffer;

        for (uint i = 0; i < itemCount; i++) {
            Assert.InRange(items[i].doubleValue, 0.0, 100.0);
        }
    }

    [Fact]
    public void TryReadRaw_Reads_Every_Instance_Into_The_Buffer()
    {
        using PdhQuery? query = PdhQuery.Open(nameof(PdhQueryTests), ProcessorTimeCounterPath);

        Assert.NotNull(query);
        Assert.True(query.Collect());

        Assert.True(query.TryReadRaw(query.Counters[0], out uint itemCount));
        Assert.True(itemCount >= 2, $"expected a processor and _Total, read {itemCount} instances");
    }
}
#endif
