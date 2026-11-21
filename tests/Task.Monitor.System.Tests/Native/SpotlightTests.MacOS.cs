#if __APPLE__
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class SpotlightTests
{
    [SkippableFact]
    public void GetLogicalSize_Reads_A_Bundle_Size_From_Spotlight()
    {
        // Calculator ships with every Mac. CI machines often run with Spotlight indexing off, in
        // which case there is no size to read and the size cache walks the bundle instead.
        long? bytes = Spotlight.GetLogicalSize("/System/Applications/Calculator.app");

        Skip.If(bytes is null, "Spotlight has no size for Calculator (indexing is off or incomplete)");

        Assert.True(bytes > 0);
    }

    [Fact]
    public void GetLogicalSize_Returns_Null_For_A_Missing_Path()
    {
        Assert.Null(Spotlight.GetLogicalSize(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }
}
#endif
