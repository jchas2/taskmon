#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class KernelHandleScopeTests
{
    private const uint CREATE_NEW                = 1;
    private const uint FILE_FLAG_DELETE_ON_CLOSE = 0x04000000;
    private const uint GENERIC_WRITE             = 0x40000000;

    [Fact]
    public void Dispose_Closes_The_Handle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"kernel-handle-scope-{Guid.NewGuid():N}.tmp");

        // The file is deleted when its last handle closes, which shows the handle really was closed.
        // Probing the handle value itself isn't reliable, as Windows soon reuses a freed value.
        using (KernelHandleScope scope = new(FileApi.CreateFileW(
            path,
            GENERIC_WRITE,
            0,
            nint.Zero,
            CREATE_NEW,
            FILE_FLAG_DELETE_ON_CLOSE,
            nint.Zero))) {

            Assert.False(scope.IsInvalid);
            Assert.True(File.Exists(path));
        }

        Assert.False(File.Exists(path));
    }

    // OpenProcess fails with null, CreateFileW and CreateToolhelp32Snapshot with -1. Neither may be
    // closed: -1 is also the pseudo handle for the current process.
    [Fact]
    public void IsInvalid_Is_True_For_Both_Failure_Values()
    {
        using KernelHandleScope none    = new(nint.Zero);
        using KernelHandleScope invalid = new(Kernel32.INVALID_HANDLE_VALUE);

        Assert.True(none.IsInvalid);
        Assert.True(invalid.IsInvalid);
    }

    [Fact]
    public void Value_Converts_Implicitly_To_The_Handle()
    {
        using KernelHandleScope scope = new(Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0));

        nint value = scope;

        Assert.False(scope.IsInvalid);
        Assert.Equal(scope.Value, value);
    }
}
#endif
