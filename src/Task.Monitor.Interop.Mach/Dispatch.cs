using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static unsafe class Dispatch
{
    [DllImport(Libraries.LibSystemDyLib)]
    public static extern nint dispatch_queue_create(string label, nint attributes);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern void dispatch_release(nint obj);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern void dispatch_sync_f(
        nint      queue, 
        nint      context, 
        delegate* unmanaged<nint, void> work);
}
