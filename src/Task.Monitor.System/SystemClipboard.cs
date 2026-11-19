namespace Task.Monitor.System;

// Puts text on the system clipboard for the user to paste elsewhere. False when it couldn't.
public static partial class SystemClipboard
{
    public static bool SetText(string text) => SetTextInternal(text);
}
