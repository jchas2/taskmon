using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Mascot;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public sealed class AboutScreen : Screen
{
    private const string Title = "ABOUT TASK MONITOR";

    private const int ReservedRows = 2;

    private readonly AnsiScreenBuffer frame = new();
    private readonly MascotControl    mascot;
    private readonly RunContext       runContext;

    public AboutScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        mascot = new MascotControl(runContext.Terminal, MascotAnimation.LoadEmbedded()) {
            Caption = ["Task Monitor", $"Version {AssemblyVersionInfo.GetVersion()}"],
            Visible = true
        };

        Controls.Add(mascot);
    }

    protected override void OnDraw()
    {
        Theme theme  = runContext.AppConfig.Theme;
        int   offset = Math.Max(0, (Width - Title.Length) / 2);

        frame.Clear();
        frame.MoveTo(X, Y);
        frame.SetColour(theme.MenubarForeground, theme.MenubarBackground);
        frame.Append(' ', offset);
        frame.Append(Title.AsSpan(0, Math.Min(Title.Length, Math.Max(0, Width - offset))));
        frame.Append(' ', Width - offset - Title.Length);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());

        mascot.Draw();

        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Y + Height - 1,
            10,
            theme,
            enabled: true,
            Terminal);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (mascot.IsIntroPlaying && keyInfo.Key is not (ConsoleKey.Escape or ConsoleKey.F10)) {
            mascot.Skip();
            handled = true;
            return;
        }

        base.OnKeyPressed(keyInfo, ref handled);
    }

    protected override void OnLoad()
    {
        Theme theme = runContext.AppConfig.Theme;

        BackgroundColour        = theme.Background;
        ForegroundColour        = theme.Foreground;
        mascot.BackgroundColour = theme.Background;
        mascot.ForegroundColour = theme.Foreground;

        runContext.Terminal.CursorVisible = false;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;

        int available = Math.Max(0, Height - ReservedRows);

        mascot.X      = X;
        mascot.Width  = Width;
        mascot.Height = Math.Min(mascot.PreferredHeight, available);
        mascot.Y      = Y + 1 + (available - mascot.Height) / 2;

        base.OnResize();
    }

    protected override void OnShown() => mascot.Play();

    protected override void OnUnload()
    {
        runContext.Terminal.CursorVisible = true;

        base.OnUnload();
    }
}
