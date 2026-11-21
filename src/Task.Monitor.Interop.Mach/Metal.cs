using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class Metal
{
    public const string RecommendedMaxWorkingSetSizeSelector = "recommendedMaxWorkingSetSize";

    [DllImport(Libraries.Metal)]
    public static extern nint MTLCreateSystemDefaultDevice();
}
