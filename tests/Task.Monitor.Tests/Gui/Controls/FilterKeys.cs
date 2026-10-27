using Task.Monitor.System.Controls;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

internal static class FilterKeys
{
    public static void Apply(Control ctrl, string text)
    {
        Press(ctrl, new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false));

        foreach (char ch in text) {
            Press(ctrl, new ConsoleKeyInfo(ch, 0, false, false, false));
        }

        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));
    }

    public static bool Press(Control ctrl, ConsoleKeyInfo keyInfo)
    {
        bool handled = false;
        ctrl.KeyPressed(keyInfo, ref handled);
        return handled;
    }

    public static bool Press(Control ctrl, ConsoleKey key) => Press(ctrl, ControlHelper.GetConsoleKeyInfo(key));
}
