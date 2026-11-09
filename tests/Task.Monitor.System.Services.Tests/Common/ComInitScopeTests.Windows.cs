#if __WIN32__
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Tests.Common;

// Each test runs on its own thread, as COM initialisation belongs to the thread.
[SupportedOSPlatform("windows")]
public sealed class ComInitScopeTests
{
    private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);
    private const int S_FALSE            = 1;
    private const int S_OK               = 0;

    // A default scope didn't initialise the thread, so it must not uninitialise it.
    [Fact]
    public void Dispose_Of_A_Default_Scope_Leaves_The_Thread_As_It_Was() =>
        RunOnThread(ApartmentState.STA, () => {
            ComInitScope scope = default;
            scope.Dispose();

            Assert.True(IsInitialised(Ole32.COINIT_APARTMENTTHREADED));
        });

    // RPC_E_CHANGED_MODE leaves the thread in the apartment it was already in. Uninitialising after
    // it would take away the runtime's own initialisation.
    [Fact]
    public void Dispose_Skips_A_Failed_Initialise() =>
        RunOnThread(ApartmentState.STA, () => {
            using (ComInitScope scope = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED)) {
                Assert.Equal(RPC_E_CHANGED_MODE, scope.HResult);
            }

            Assert.True(IsInitialised(Ole32.COINIT_APARTMENTTHREADED));
        });

    [Fact]
    public void Dispose_Uninitialises_A_Thread_It_Initialised() =>
        RunOnThread(ApartmentState.MTA, () => {
            bool before = IsInitialised(Ole32.COINIT_MULTITHREADED);

            using (ComInitScope scope = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED)) {
                Assert.True(scope.HResult is S_OK or S_FALSE);
                Assert.True(IsInitialised(Ole32.COINIT_MULTITHREADED));
            }

            Assert.Equal(before, IsInitialised(Ole32.COINIT_MULTITHREADED));
        });

    // Probes without changing the thread: CoInitializeEx returns S_FALSE on a thread already in the
    // apartment, and either result needs its own CoUninitialize.
    private static bool IsInitialised(uint coInit)
    {
        int hResult = Ole32.CoInitializeEx(nint.Zero, coInit);

        if (hResult >= 0) {
            Ole32.CoUninitialize();
        }

        return hResult == S_FALSE;
    }

    private static void RunOnThread(ApartmentState apartment, Action test)
    {
        ExceptionDispatchInfo? failure = null;

        Thread thread = new(() => {
            try {
                test();
            }
            catch (Exception ex) {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(apartment);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }
}
#endif
