#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class AttrTests
{
    [Fact]
    public void GetAddedTime_Is_Recorded_When_An_Item_Is_Created()
    {
        string folder = Directory.CreateTempSubdirectory("taskmon-attr-").FullName;

        try {
            DateTime? added = Attr.GetAddedTime(folder);

            Assert.NotNull(added);
            Assert.InRange(added.Value, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
        }
        finally {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void GetAddedTime_Returns_Null_For_A_Missing_Path()
    {
        Assert.Null(Attr.GetAddedTime(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }
}
#endif
