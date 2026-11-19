#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System;

public static partial class SystemClipboard
{
    private const int OpenAttempts          = 5;
    private const int OpenRetryMilliseconds = 20;

    // The text goes on as UTF-16 in global memory, with Windows line endings so it pastes as lines
    // everywhere. Once SetClipboardData takes the memory, the clipboard owns it.
    private static bool SetTextInternal(string text)
    {
        string windowsText = text.ReplaceLineEndings("\r\n");

        if (!TryOpen()) {
            return false;
        }

        try {
            if (!ClipboardApi.EmptyClipboard()) {
                return false;
            }

            nint memory = ClipboardApi.GlobalAlloc(ClipboardApi.GMEM_MOVEABLE, (nuint)((windowsText.Length + 1) * sizeof(char)));

            if (memory == 0) {
                return false;
            }

            nint target = ClipboardApi.GlobalLock(memory);

            if (target == 0) {
                ClipboardApi.GlobalFree(memory);
                return false;
            }

            try {
                Marshal.Copy(windowsText.ToCharArray(), 0, target, windowsText.Length);
                Marshal.WriteInt16(target, windowsText.Length * sizeof(char), 0);
            }
            finally {
                ClipboardApi.GlobalUnlock(memory);
            }

            if (ClipboardApi.SetClipboardData(ClipboardApi.CF_UNICODETEXT, memory) == 0) {
                ClipboardApi.GlobalFree(memory);
                return false;
            }

            return true;
        }
        finally {
            ClipboardApi.CloseClipboard();
        }
    }

    // Another program may be holding the clipboard for a moment.
    private static bool TryOpen()
    {
        for (int attempt = 0; attempt < OpenAttempts; attempt++) {
            if (ClipboardApi.OpenClipboard(0)) {
                return true;
            }

            Thread.Sleep(OpenRetryMilliseconds);
        }

        return false;
    }
}
#endif
