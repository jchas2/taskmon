using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls;

public sealed class BannerControl(ISystemTerminal terminal, AppConfig appConfig) : Control(terminal)
{
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void OnDrawInternal()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = appConfig.DefaultTheme.MenubarBackground;
        Terminal.ForegroundColor = appConfig.DefaultTheme.MenubarForeground;

        Terminal.Write(Text);
        Terminal.WriteEmptyLineTo(Width - Text.Length);
    }
    
    public string Text { get; set; } = string.Empty;
}