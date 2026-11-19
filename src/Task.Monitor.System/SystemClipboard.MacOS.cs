#if __APPLE__
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Task.Monitor.System;

public static partial class SystemClipboard
{
    private const string PbCopy             = "/usr/bin/pbcopy";
    private const int    PbCopyMilliseconds = 2000;

    // pbcopy reads its input in the locale's encoding, which a bare environment leaves unset, so it
    // is told the text is UTF-8.
    private static bool SetTextInternal(string text)
    {
        ProcessStartInfo startInfo = new(PbCopy) {
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            UseShellExecute       = false
        };

        startInfo.Environment["LANG"] = "en_US.UTF-8";

        try {
            using Process? process = Process.Start(startInfo);

            if (process is null) {
                return false;
            }

            process.StandardInput.Write(text);
            process.StandardInput.Close();

            return process.WaitForExit(PbCopyMilliseconds) && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException) {
            return false;
        }
    }
}
#endif
