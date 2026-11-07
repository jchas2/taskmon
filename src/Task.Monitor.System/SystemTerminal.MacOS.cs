using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System;

public partial class SystemTerminal : ISystemTerminal
{
#if __APPLE__
    private const string AnsiHideCursor = "\x1b[?25l";
    private const string AnsiShowCursor = "\x1b[?25h";

    private const string AppleTerminalProgram    = "Apple_Terminal";
    private const string AppleTerminalExecutable = "/Terminal.app/Contents/MacOS/Terminal";
    private const int    LaunchdPid              = 1;
    private const int    MaxAncestors            = 32;

    private bool cursorVisible;

    private bool CursorVisibleInternal
    {
        get => cursorVisible;
        set {
            cursorVisible = value;
            Console.Out.Write(cursorVisible ? AnsiShowCursor : AnsiHideCursor);
        }
    }

    private static DrawingCapabilities DetectDrawingCapabilitiesInternal() =>
        IsAppleTerminal(Environment.GetEnvironmentVariable("TERM_PROGRAM"), EnumerateAncestorPaths)
            ? DrawingCapabilities.All & ~DrawingCapabilities.SeamlessBlockGlyphs
            : DrawingCapabilities.All;

    private void EnableAnsiTerminalCodesInternal() { }

    internal static IEnumerable<string> EnumerateAncestorPaths()
    {
        int pid = Environment.ProcessId;

        for (int depth = 0; depth < MaxAncestors; depth++) {
            pid = GetParentPid(pid);

            if (pid <= LaunchdPid) {
                yield break;
            }

            yield return GetPath(pid);
        }
    }

    private static unsafe int GetParentPid(int pid)
    {
        ProcInfo.proc_bsdinfo info = default;
        int size = sizeof(ProcInfo.proc_bsdinfo);

        return ProcInfo.proc_pidinfo(pid, ProcInfo.PROC_PIDTBSDINFO, 0, &info, size) == size
            ? (int)info.pbi_ppid
            : 0;
    }

    private static unsafe string GetPath(int pid)
    {
        byte* buffer = stackalloc byte[ProcInfo.MAXPATHLEN * 4];
        int   length = LibProc.proc_pidpath(pid, buffer, ProcInfo.MAXPATHLEN * 4);

        return length > 0 ? Encoding.UTF8.GetString(buffer, length) : string.Empty;
    }

    // sudo drops TERM_PROGRAM, so without it look for Terminal.app among the parent processes.
    internal static bool IsAppleTerminal(string? termProgram, Func<IEnumerable<string>> ancestorPaths) =>
        string.IsNullOrEmpty(termProgram)
            ? ancestorPaths().Any(path => path.EndsWith(AppleTerminalExecutable, StringComparison.Ordinal))
            : termProgram == AppleTerminalProgram;

    private static bool IsLegacyConsoleHostInternal() => false;
#endif
}
